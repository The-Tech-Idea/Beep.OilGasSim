// R20d.22 — the conditions themselves (SDD-012 §1–§2, finding 180).
//
// THE MODELS WERE ALWAYS RIGHT AND JOINED TO NOTHING. `SeverityWeightedDegradation`
// and `ExponentialHazardModel` have shipped and been tested since R18; the module
// that composes them declared `ownsState: nothing` and `stages: none`, so no tick
// could reach them. Equipment in a running game never aged and never failed. This
// is the state that was missing — one condition per piece of equipment, carried
// from tick to tick, which is the thing a pure model cannot hold for itself.
//
// EVERY REGISTERED FLOW ELEMENT (SDD-012 §2's R20d.22 amendment). The set is not
// fixed at composition: wells are drilled and a treater is bought years in, so
// this enrols whatever the registry has grown since last tick rather than being
// handed a list once. New equipment starts at full condition, which is what
// buying new equipment means.
//
// THE PASS DRAWS AND THIS REMEMBERS. `IntegrityPass` owns the arithmetic and the
// `hazard` stream; nothing here touches an RNG, so what a component's condition
// is and what it does about it stay two answerable questions.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Integrity;

/// <summary>
/// Every piece of equipment's condition, and which of them have failed
/// (SDD-012 §1).
/// </summary>
public sealed class AssetIntegrity : IStateOwner
{
    private readonly IntegrityPass _pass;
    private readonly Func<IFlowElement, ContentId> _classOf;

    // The field's cut, as of the call that is running. Held for the length of
    // one Advance rather than fetched: the severity function is called once per
    // component and the number is the same for all of them, so reading it once
    // is both cheaper and the only way they are guaranteed to agree.
    private double _waterCut;

    // The reservoir's own H2S, normalised (SDD-012 §5). Held the same way and
    // for the same reason: one number, read once, so every component in a pass
    // is charged the same service.
    private double _sourFraction;

    // Enrolment order, and never a Dictionary walk (D-5). The index answers
    // "which one is this?" and the list answers "in what order?" — the same
    // split the flow network makes, for the same reason.
    private readonly List<ComponentState> _components = [];
    private readonly Dictionary<EntityId<IFlowElement>, int> _index = [];

    public AssetIntegrity(IntegrityPass pass, Func<IFlowElement, ContentId> classOf)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ArgumentNullException.ThrowIfNull(classOf);

        _pass = pass;
        _classOf = classOf;
    }

    /// <summary>What condition a piece of equipment is in, 0..1.</summary>
    public double ConditionOf(EntityId<IFlowElement> element) =>
        _index.TryGetValue(element, out int at) ? _components[at].Condition : FullCondition;

    /// <summary>Whether it is out of service.</summary>
    public bool HasFailed(EntityId<IFlowElement> element) =>
        _index.TryGetValue(element, out int at) && _components[at].Failed;

    /// <summary>How many pieces of equipment are down. What a host puts on a
    /// panel, and what a test asks when it wants to know the mechanic ran.</summary>
    public int FailedCount { get; private set; }

    /// <summary>
    /// A completed repair (SDD-012 §2). Condition back to new and the service
    /// clock reset — §1's "never restored implicitly" says only this may do it.
    /// </summary>
    public void Repair(EntityId<IFlowElement> element)
    {
        if (!_index.TryGetValue(element, out int at)) return;

        if (_components[at].Failed) FailedCount--;

        _components[at] = _components[at] with
        {
            Condition = FullCondition,
            Failed = false,
            TicksSinceService = 0,
        };
    }

    /// <summary>Whether there is anything to repair — asked by the command that
    /// proposes one, so a player is told rather than invoiced.</summary>
    public bool NeedsRepair(EntityId<IFlowElement> element) =>
        _index.TryGetValue(element, out int at)
        && (_components[at].Failed || _components[at].Condition < FullCondition);

    /// <summary>
    /// Stage 4: enrol whatever is new, age everything, and roll.
    ///
    /// <para>Enrolment before ageing, so a well drilled this tick is under the
    /// pass from its first month rather than its second — and at full condition,
    /// so it does not inherit the field's wear.</para>
    /// </summary>
    public IReadOnlyList<FailureOutcome> Advance(
        IReadOnlyList<IFlowElement> registered, double waterCut, double sourFraction, Duration dt)
    {
        ArgumentNullException.ThrowIfNull(registered);

        if (waterCut is < 0.0 or > 1.0 || double.IsNaN(waterCut))
            throw new ModelFault("SDD-012 §1", null,
                "a water cut is a fraction of the liquid produced");

        if (sourFraction is < 0.0 or > 1.0 || double.IsNaN(sourFraction))
            throw new ModelFault("SDD-012 §5", null,
                "a sour fraction is normalised against the souring reference and clamped, " +
                "so it is a fraction of one like every other term in the severity sum");

        _waterCut = waterCut;
        _sourFraction = sourFraction;

        for (int i = 0; i < registered.Count; i++)
        {
            IFlowElement element = registered[i];
            if (_index.ContainsKey(element.Id)) continue;

            _index.Add(element.Id, _components.Count);
            _components.Add(new ComponentState(
                element.Id, _classOf(element), FullCondition,
                Failed: false, TicksSinceService: 0));
        }

        IReadOnlyList<FailureOutcome> failures =
            _pass.Advance(_components, SeverityOf, dt, out IReadOnlyList<ComponentState> aged);

        // `aged` comes back in ASCENDING ID (the pass sorts, so the draw order
        // cannot depend on enrolment). The index is rebuilt from it rather than
        // assumed unchanged — assuming would silently mis-key every condition
        // the first time a well was drilled out of id order.
        _components.Clear();
        _index.Clear();
        FailedCount = 0;

        for (int i = 0; i < aged.Count; i++)
        {
            _index.Add(aged[i].Id, i);
            _components.Add(aged[i]);
            if (aged[i].Failed) FailedCount++;
        }

        return failures;
    }

    /// <summary>
    /// SDD-012 §1's severity terms, at what this composition can actually say.
    ///
    /// <para>THREE OF THE FIVE HAVE A SUBJECT TODAY, and the other two are zero
    /// because nothing has happened rather than because a number is missing —
    /// the same honesty SDD-012 §4 applies to the ESG record's unbuilt terms.
    /// Water cut is the field's, measured at what the chain delivered, and it is
    /// the dominant corrosion driver on a watering-out field. Time since service
    /// is each item's own. SOURING joined them at R20d.25, and it needed a
    /// waterflood first: the H2S is a consequence of sea water a company bought
    /// twenty years earlier, and until there was a flood there was no sea
    /// water.</para>
    ///
    /// <para>Nothing in this composition vents or runs hot against a rated
    /// temperature, and duty needs a rated throughput per element that the tier
    /// datasheets do not carry. Each becomes a term the day it has something to
    /// read.</para>
    /// </summary>
    private ServiceSeverity SeverityOf(ComponentState component) =>
        new(WaterCut: _waterCut,
            SourFraction: _sourFraction,
            DutyFraction: 0.0,
            OverTemperature: 0.0,
            TicksSinceService: component.TicksSinceService);

    // ------------------------------------------------------------- the save

    public StateKey Key { get; } = new("integrity.conditions");

    public int SchemaVersion => 1;

    /// <summary>
    /// Conditions survive a save, and they have to: a campaign reloaded with
    /// every item back at new would let a player launder twenty years of
    /// neglect through the save menu, which is the cheapest possible repair.
    ///
    /// <para>The count and then the rows, in enrolment order. The CLASS is
    /// written too even though composition could supply it again — it is what
    /// an audit row from before the save said, and a restore that renamed
    /// equipment would break the trail the fairness record exists to keep.</para>
    /// </summary>
    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _components.Count);

        for (int i = 0; i < _components.Count; i++)
        {
            ComponentState component = _components[i];
            string at = Prefix(i);

            writer.WriteInt64(at + "id", (long)component.Id.Value);
            writer.WriteString(at + "class", component.Tier.Value);
            writer.WriteDouble(at + "condition", component.Condition);
            writer.WriteInt64(at + "failed", component.Failed ? 1L : 0L);
            writer.WriteInt64(at + "unserviced", component.TicksSinceService);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _components.Clear();
        _index.Clear();
        FailedCount = 0;

        var count = (int)reader.ReadInt64("count");

        for (int i = 0; i < count; i++)
        {
            string at = Prefix(i);

            var component = new ComponentState(
                new EntityId<IFlowElement>((ulong)reader.ReadInt64(at + "id")),
                new ContentId(reader.ReadString(at + "class")),
                reader.ReadDouble(at + "condition"),
                Failed: reader.ReadInt64(at + "failed") != 0L,
                TicksSinceService: (int)reader.ReadInt64(at + "unserviced"));

            _index.Add(component.Id, _components.Count);
            _components.Add(component);

            if (component.Failed) FailedCount++;
        }
    }

    private static string Prefix(int index) =>
        "c" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".";

    private const double FullCondition = 1.0;
}
