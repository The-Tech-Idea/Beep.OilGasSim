// R21b — drilling as a decision (design 20's D-catalogue, SDD-007 §4).
//
// Until now a well appeared the instant it was ordered and always produced.
// That is not a decision: there is exactly one right answer and no reason ever
// to hesitate. Three things make it one.
//
// IT TAKES TIME. Money leaves now and oil arrives months later, so the choice is
// about when as well as whether, and a company can be committed to a well it can
// no longer afford to wait for.
//
// IT CAN BE DRY. The outcome is drawn ONCE, at the moment the well is ordered
// (SDD-007 §4), from the Exploration stream — and the draw is recorded then even
// though the result is not revealed until the rig finishes. Drawing at
// completion instead would let a player reload the month before it landed and
// try again, which turns a probability into a slot machine.
//
// IT COSTS THE SAME EITHER WAY. A dry hole is paid for in full. That is the
// whole of exploration economics in one sentence.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Composition;

/// <summary>What a well costs, how long it takes, and how often it finds oil.</summary>
public sealed record DrillingTerms(
    Money CostPerWell,
    Length MaximumDepth,
    int DurationTicks,
    double ProbabilityOfSuccess);

/// <summary>
/// A well being drilled: committed to, paid for, outcome already decided, and
/// not yet known to the player.
/// </summary>
internal sealed record WellUnderConstruction(
    EntityId<IReservoirCompartmentEntity> Target,
    Length TotalDepth,
    Tick Completes,
    bool WillProduce,
    AuditId Cause);

/// <summary>
/// Owner of <c>field.drilling</c> — the rigs currently turning.
///
/// <para>Its own state key because it is a fact that outlives a tick and must
/// survive a save: a company that reloads mid-well has to still be committed to
/// it, still be waiting, and still get the same answer.</para>
/// </summary>
internal sealed class DrillingState(DrillingTerms terms) : IStateOwner
{
    private readonly List<WellUnderConstruction> _drilling = [];

    public StateKey Key { get; } = new("field.drilling");

    public int SchemaVersion => 1;

    public int InProgress => _drilling.Count;

    public void Begin(WellUnderConstruction well) => _drilling.Add(well);

    /// <summary>
    /// Everything that finishes this tick, removed as it is returned. A well is
    /// completed exactly once, however many ticks pass in one call.
    /// </summary>
    public IReadOnlyList<WellUnderConstruction> CompletedBy(Tick now)
    {
        var done = new List<WellUnderConstruction>();

        for (int i = _drilling.Count - 1; i >= 0; i--)
            if (_drilling[i].Completes.Value <= now.Value)
            {
                done.Add(_drilling[i]);
                _drilling.RemoveAt(i);
            }

        // Removal walked backwards, so the results are in reverse order; the
        // reverse puts them back into the order they were started, which is what
        // makes two runs of one seed complete wells identically.
        done.Reverse();
        return done;
    }

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _drilling.Count);

        for (int i = 0; i < _drilling.Count; i++)
        {
            WellUnderConstruction well = _drilling[i];
            string at = "drilling." +
                i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";

            writer.WriteInt64(at + "target", (long)well.Target.Value);
            writer.WriteDouble(at + "depth", well.TotalDepth.Metres);
            writer.WriteInt64(at + "completes", well.Completes.Value);
            writer.WriteInt64(at + "cause", (long)well.Cause.Value);

            // The OUTCOME is saved, because it was already drawn. A save that
            // omitted it would let a reload re-roll the well, which is the exact
            // exploit drawing once at commitment exists to prevent.
            writer.WriteInt64(at + "will-produce", well.WillProduce ? 1 : 0);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _drilling.Clear();

        long count = reader.ReadInt64("count");

        for (long i = 0; i < count; i++)
        {
            string at = "drilling." +
                i.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";

            _drilling.Add(new WellUnderConstruction(
                new EntityId<IReservoirCompartmentEntity>((ulong)reader.ReadInt64(at + "target")),
                new Length(reader.ReadDouble(at + "depth")),
                new Tick((int)reader.ReadInt64(at + "completes")),
                reader.ReadInt64(at + "will-produce") == 1,
                new AuditId((ulong)reader.ReadInt64(at + "cause"))));
        }
    }

    public DrillingTerms Terms => terms;
}

/// <summary>
/// Stage 3. Rigs that finished this month hand over a well or a dry hole.
/// </summary>
internal sealed class DrillingStage(
    DrillingState drilling,
    FieldControl field,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Operations;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<WellUnderConstruction> finished = drilling.CompletedBy(context.Tick);

        for (int i = 0; i < finished.Count; i++)
        {
            WellUnderConstruction well = finished[i];

            audit.Record(
                AuditCategory.StochasticOutcome, subject: null, cause: well.Cause,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["outcome"] = new(well.WillProduce ? "completed" : "dry-hole"),
                });

            // A dry hole opens nothing. The money is spent, the months are gone,
            // and the player has learned something about the field — which is
            // the only thing they get for it.
            if (!well.WillProduce) continue;

            field.OpenWell(
                Defaults.CompletionFor(field.NextWellId(), well.Target, well.TotalDepth),
                well.Target);
        }
    }
}

/// <summary>
/// Pure (R1 §2.5) and reports EVERY reason: a player told only that a well is
/// too deep, who then finds they could not have afforded it either, has been
/// made to learn the truth in instalments.
/// </summary>
internal sealed class DrillWellValidator(
    CompanyState company, FieldControl field, DrillingTerms terms)
    : ICommandValidator<DrillWellCommand>
{
    public IReadOnlyList<RejectionReason> Validate(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reasons = new List<RejectionReason>();

        if (company.Ledger.Cash < terms.CostPerWell)
            reasons.Add(new RejectionReason(
                "$loc:reject.insufficient-cash",
                $"a well costs {terms.CostPerWell.Cents} cents and the company holds " +
                $"{company.Ledger.Cash.Cents}"));

        if (command.TotalDepth.Metres > terms.MaximumDepth.Metres)
            reasons.Add(new RejectionReason(
                "$loc:reject.beyond-drilling-envelope",
                $"{command.TotalDepth.Metres} m is past the {terms.MaximumDepth.Metres} m " +
                "the company can currently drill"));

        if (command.TotalDepth.Metres <= 0.0)
            reasons.Add(new RejectionReason(
                "$loc:reject.invalid-depth", "a well must have a positive depth"));

        if (field.CompartmentCount == 0)
            reasons.Add(new RejectionReason(
                "$loc:reject.no-target", "there is nothing here to drill into"));

        return reasons;
    }
}

/// <summary>Cannot fail (R1 §2.5) — everything that could refuse already has.</summary>
internal sealed class DrillWellApplier(
    CompanyState company,
    DrillingState drilling,
    SimulationClock clock,
    IRandomStream exploration) : ICommandApplier<DrillWellCommand>
{
    public Applied Apply(DrillWellCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        DrillingTerms terms = drilling.Terms;

        // Capex, not opex: the well is an asset the company now owns, and the
        // distinction is what makes depreciation and abandonment mean anything
        // later (SDD-009 §1). Paid on commitment, whatever the hole turns out
        // to hold.
        company.Ledger.Post(new Movement(
            clock.CurrentTick, Account.Capex_PPE, Account.Cash, terms.CostPerWell,
            MovementCategory.Development, Asset: null, Cause: submission));

        // Drawn ONCE, here (SDD-007 §4). Drawing at completion would let a
        // player reload the month before the rig finished and try again.
        bool willProduce = exploration.NextUnit() < terms.ProbabilityOfSuccess;

        drilling.Begin(new WellUnderConstruction(
            command.Target,
            command.TotalDepth,
            new Tick(clock.CurrentTick.Value + terms.DurationTicks),
            willProduce,
            submission));

        return new Applied(submission, []);
    }
}
