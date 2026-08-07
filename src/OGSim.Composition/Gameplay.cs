// R21 (first slice) — what makes this a game rather than a simulation that runs:
// the player can act, can see, and can lose.
//
// AGENCY. Drilling a well is a command, validated then applied, and the
// validation is where the decision has weight: a company that cannot afford the
// well is told so in a reason it can render, not silently allowed to go
// bankrupt. Every rejection is domain-typed (SDD-001 §7) so the host never
// invents an explanation.
//
// VISIBILITY. The read model is rebuilt at stage 13 from what the player is
// entitled to know. It is not a view onto engine state — it is a copy taken at
// the close, so nothing a host holds can change under it mid-tick, and nothing
// truth-side can leak through it.
//
// CONSEQUENCE. A company whose cash runs out is finished. Without that the
// player's decisions cost nothing and none of the rest matters.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Composition;

// ------------------------------------------------------------------ commands

/// <summary>
/// Drill and complete a well on a known compartment (design 20's D-catalogue —
/// the first decision a player makes and the one every other decision waits on).
/// </summary>
public sealed record DrillWellCommand(
    EntityId<IReservoirCompartmentEntity> Target,
    Length TotalDepth) : Command(Subject: null);

// ------------------------------------------------------------- the read model

/// <summary>
/// What the player can see, rebuilt at the close of every tick.
///
/// <para>Deliberately NOT the full SDD-017 read model — that is R21's whole
/// phase, sixteen projections wide. This is the subset the current loop can
/// honestly fill, and every field in it is a number the player is entitled to:
/// their own cash, their own well count, what they sold. Reservoir pressure is
/// absent because it is truth, and it reaches a host through beliefs or not at
/// all.</para>
/// </summary>
public sealed record FieldReadModel(
    Tick Tick,
    GameDate Date,
    Money Cash,
    int Wells,
    int WellsDrilling,
    SurfaceVolume ProducedThisTick,
    bool Insolvent);

// -------------------------------------------------------------- losing

// Design 09s failure condition, at its simplest true form: a company that
// cannot pay is finished. It is recorded as an AUDIT entry and surfaced on the
// read model rather than as an EngineEvent — an event carries a loop role and a
// player-visibility flag (SDD-001 §8) and those are R21s to decide, not
// something to guess at here.

/// <summary>
/// Stage 13. Publishes the read model and decides whether the company is still
/// playing.
/// </summary>
internal sealed class CloseStage(
    ProductionLoop loop,
    CompanyState company,
    FieldControl field,
    DrillingState drilling,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Close;

    /// <summary>The tick just closed, as the host reads it.</summary>
    public FieldReadModel? Published { get; private set; }

    /// <summary>Once true, always true: a company does not recover from having
    /// been wound up, and a later month's revenue must not quietly un-fail
    /// it.</summary>
    public bool Insolvent { get; private set; }

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!Insolvent && company.Ledger.Cash.Cents < 0)
        {
            Insolvent = true;

            audit.Record(
                AuditCategory.StateTransition, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["outcome"] = new("insolvent"),
                    ["cash-cents"] = new(company.Ledger.Cash.Cents.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                });
        }

        Published = new FieldReadModel(
            context.Tick,
            context.Date,
            company.Ledger.Cash,
            field.WellCount,
            drilling.InProgress,
            loop.ProducedThisTick,
            Insolvent);
    }
}
