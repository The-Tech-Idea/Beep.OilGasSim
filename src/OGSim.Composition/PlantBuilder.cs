// S2 step 3 — the thing that erects a surface plant (plans 22 §4).
//
// ONE BUILDER, TWO CALLERS. Composition still commissions a plant at startup so
// the shipped scenario behaves as it always has; the activity a company pays for
// calls exactly the same code. Two ways to build a chain would be two chains
// that drifted, and the second one would be the one nobody tested.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// Erects an early production facility: the minimum train a field needs before
/// a barrel can reach a tank, plus the water side a flood needs later.
/// </summary>
/// <remarks>
/// <para><b>Why it is one purchase and not ten.</b> The separator has three
/// outlets — liquid, gas and water — and each needs somewhere to go, so a
/// company building strictly piece by piece would buy ten elements and see no
/// oil until the tenth. Nine purchases with no visible effect is a shopping
/// list, not a decision.</para>
///
/// <para><b>And it is what the industry actually does.</b> An early production
/// facility is a packaged, skid-mounted plant, and it is exactly how a small
/// field is brought on. The individual install commands keep their job: adding
/// capacity and climbing tiers once the field is running.</para>
/// </remarks>
internal sealed class PlantBuilder
{
    private readonly FacilityLadders _ladders;
    private readonly ISeparationModel _separation;
    private readonly IFluidPropertyModel _fluid;
    private readonly IHydraulicModel _hydraulics;
    private readonly IFlowElementRegistry _network;

    public PlantBuilder(
        FacilityLadders ladders,
        ISeparationModel separation,
        IFluidPropertyModel fluid,
        IHydraulicModel hydraulics,
        IFlowElementRegistry network)
    {
        ArgumentNullException.ThrowIfNull(ladders);
        ArgumentNullException.ThrowIfNull(separation);
        ArgumentNullException.ThrowIfNull(fluid);
        ArgumentNullException.ThrowIfNull(hydraulics);
        ArgumentNullException.ThrowIfNull(network);

        _ladders = ladders;
        _separation = separation;
        _fluid = fluid;
        _hydraulics = hydraulics;
        _network = network;
    }

    /// <summary>Whether there is anything left to commission.</summary>
    /// <remarks>
    /// Asked of the manifold because it is the head of the train: nothing else
    /// can be reached without it, and a plant that has one has been through here.
    /// </remarks>
    public static bool Standing(SurfaceChain plant)
    {
        ArgumentNullException.ThrowIfNull(plant);

        return plant.Manifold is not null;
    }

    /// <summary>
    /// Build the train and wire it.
    /// </summary>
    /// <remarks>
    /// Every element is registered before any edge is made, because
    /// <c>Connect</c> refuses an endpoint it has not seen — and the wiring is
    /// left to the plant, which knows the shape and makes each edge exactly
    /// once.
    /// </remarks>
    public void Commission(SurfaceChain plant)
    {
        ArgumentNullException.ThrowIfNull(plant);

        if (Standing(plant))
            throw new InvariantFault("SDD-006 §0c", null,
                "this field already has a plant standing; a second one would register " +
                "element ids the network has already seen");

        var manifold = new OGSim.Facilities.Manifold(
            Defaults.TheManifold, _ladders.Manifold[0], Defaults.MaterialCount);

        var separator = new OGSim.Facilities.Separator(
            Defaults.TheSeparator, _ladders.Separator[0], _separation,
            _fluid, Defaults.MaterialCount);

        var custody = new OGSim.Facilities.CustodyTransferPoint(
            Defaults.TheCustodyPoint, Defaults.SalesSpec, Defaults.MaterialCount,
            Defaults.MeasureStream);

        // THE GAS LEG GOES TO A FLARE. An E1 field with no gas infrastructure
        // burns its associated gas, which is both the historical answer and the
        // one the ESG mechanics are built to make expensive later (design 13).
        // What it is NOT is an unconnected port: mass leaving the network at one
        // would vanish from the tick's conservation terms silently, and a flare
        // accounts for it — combusted and unburnt, both reported as Disposed.
        var treater = new OGSim.Facilities.Treater(
            Defaults.TheTreater, _ladders.Treater[0],
            Defaults.WaterOrdinal, Defaults.MaterialCount);

        var gasPlant = new OGSim.Facilities.GasCapture(
            Defaults.TheGasPlant, _ladders.GasPlant[0], Defaults.MaterialCount);

        // WHERE A REJECTED STREAM GOES (SDD-006 §7d, finding 252). Custody's
        // Reject port satisfies network-build's "a spec gate must declare a
        // Reject outlet" check on its own; without a sink connected to it, a
        // rejected stream would be read by nothing and vanish from the tick's
        // conservation terms the way the flare exists precisely to stop gas
        // from doing.
        var offSpecSink = new OGSim.Facilities.OffSpecSink(
            Defaults.TheOffSpecSink, Defaults.MaterialCount);

        var flare = new OGSim.Facilities.Flare(
            Defaults.TheFlare, Defaults.FlareCapacity, Defaults.FlareCombustionEfficiency,
            Defaults.MaterialCount);

        // THE WATER LEG GOES TO A DISPOSAL WELL. Its Injectivity constraint is
        // read by the solver and nowhere else (SDD-003 §3.1d's R20d.4
        // amendment), so this is what lets a watered-out field be throttled by
        // disposal and by nothing upstream at all — and the plugging term makes
        // that worse every year.
        var disposal = new OGSim.Wells.Injector(
            Defaults.TheDisposalWell, Defaults.Disposal,
            Defaults.WaterOrdinal.Ordinal, Defaults.MaterialCount);

        // AND SOMEWHERE TO BUY WATER FROM (R20d.24, SDD-003 §3.1d). Reinjecting
        // produced water is disposal that happens to help; a WATERFLOOD needs
        // water the field did not make, and imported water has to cross the
        // network like everything else or stage 6 would be creating mass. It
        // ships commanded at nothing — a field floods when a player says so.
        var intake = new OGSim.Facilities.WaterIntake(
            Defaults.TheWaterIntake, Defaults.WaterOrdinal, Defaults.MaterialCount,

            // Sea water is nobody's production. It never reaches a custody meter
            // — the injector discharges every kilogram — but an allocation must
            // name an owner, so it carries the field's, exactly as the empty
            // tank's opening provenance does.
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)),
            Defaults.DisposalPressure,
            Defaults.SurfaceAmbient);

        // THE GATHERING LINE. Without it a header's downstream demand is the
        // vessel's set point and nothing else, so commingling has no
        // consequence: two wells on one header would not feel each other at all.
        // With it, throughput costs pressure and the trap in design 04 §5 stage
        // 3 is arithmetic rather than a description.
        var flowline = new OGSim.Facilities.Pipeline(
            Defaults.TheFlowline, Defaults.Flowline, Defaults.FlowlineRating,
            new ContentId("flowline-6in"),
            _hydraulics,
            _fluid,
            Defaults.SurfaceOilDensity, Defaults.MaterialCount);

        _network.Add(manifold);
        _network.Add(flowline);
        _network.Add(separator);
        _network.Add(custody);
        _network.Add(treater);
        _network.Add(gasPlant);
        _network.Add(flare);
        _network.Add(offSpecSink);
        // Set once, not refreshed: a DISPOSAL well injects into a disposal
        // formation, not into the producing compartment. Its acceptance
        // therefore depends on that formation's pressure and the pump's, neither
        // of which the field's own depletion moves — which is also why it does
        // not support the reservoir, and why injection-for-pressure is a
        // separate mechanic (SDD-003 §3.1d's R20d.4 amendment).
        disposal.SetInjectionConditions(
            Defaults.DisposalFormationPressure, Defaults.DisposalPressure);

        // STORAGE, after the meter. The oil is metered on its way in — pipeline
        // export metering, a real arrangement — and the tank is what lets a
        // field produce above its export rate for a while instead of being
        // throttled the moment it does.
        var tank = new OGSim.Facilities.Tank(
            Defaults.TheTank, _ladders.Tank[0], Defaults.MaterialCount,
            MaterialInventory.Empty(Defaults.MaterialCount),

            // Empty tanks hold nobody's oil, and an allocation must name at
            // least one compartment — so the opening provenance names the field
            // and is replaced by the first receipt's blend.
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        _network.Add(disposal);
        _network.Add(intake);
        _network.Add(tank);

        // INSTALLED ONE AT A TIME, because that is what the plant now is. The
        // shipped scenario still starts complete; S2 step 4 is where this list
        // shortens to whatever the scenario declares, and the eleven calls below
        // become eleven things a company builds (plans 22 §4).
        plant.Install(manifold);
        plant.Install(flowline);
        plant.Install(separator);
        plant.Install(custody);
        plant.Install(treater);
        plant.Install(gasPlant);
        plant.Install(flare);
        plant.Install(disposal);
        plant.Install(intake);
        plant.Install(tank);
        plant.Install(offSpecSink);

        // AND WIRED FROM WHAT IS THERE. The ten edges used to be typed out here,
        // which was fine while composition built the whole chain in one go and
        // wrong the moment a player builds it a piece at a time — the shape of
        // the plant is a fact about the plant, so it lives on it.
        plant.Wire(_network);
    }
}
