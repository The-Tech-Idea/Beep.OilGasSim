// R12b.8 — install / construct (SDD-006 §0c, SDD-007 §5, catalogue C06/C07).
//
// THE VERB THAT ANSWERS A BOTTLENECK. Every other activity either learns
// something or makes a hole; this is the one that changes what the field can
// carry. A player watches the separator refuse production on the read model,
// reads how much it is costing them a month, and decides whether a bigger vessel
// is worth its price and the months it takes.
//
// That loop — see the constraint, name it, price the fix, wait for it — is the
// whole of an operations game. Until the chain was wired it could not exist: an
// installed unit would have been paid for and bypassed (finding 153).
//
// A REFIT, NOT A REPLACEMENT. The element is a socket and keeps its identity;
// what is fitted into it changes (SDD-006 §0c). The flow registry is write-once
// with no removal, so a vessel that could be swapped out would take its tie-ins
// with it — and on a site the foundations, the pipework and the permit stay
// exactly where they are.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>
/// Fit the next vessel up into the separator's socket.
///
/// <para>It names no tier, and that is deliberate rather than a simplification.
/// The catalogue sheets describe a LADDER (C06's "fixed → test-header", C07's
/// separator rungs), so "upgrade the separator" has one meaning; letting a
/// command carry a chosen tier would need the per-template parameter block
/// R12b.17 exists to specify, and inventing one at this call site is what F-4
/// forbids. A player who wants to skip a rung climbs two.</para>
/// </summary>
public sealed record InstallSeparatorCommand() : Command(Subject: null);

internal sealed class InstallSeparatorActivity(
    ActivityTerms terms,
    SurfaceChain chain,
    IReadOnlyList<OGSim.Facilities.SeparatorTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallSeparatorCommand>(terms)
{
    /// <summary>The vessel this enlarges, or null on ground with no plant.</summary>
    private OGSim.Facilities.Separator? Unit => chain.Separator;

    /// <summary>A vessel is PP&amp;E: the money buys something the company still
    /// owns next month (SDD-009 §1).</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    /// <summary>
    /// One at a time. Two crews fitting two rungs into one socket would race,
    /// and whichever finished second would silently win.
    /// </summary>
    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallSeparatorCommand command) =>
        Unit is OGSim.Facilities.Separator separator
            ? (new EntityRef(EntityKind.FlowElement, separator.Id.Value), NoDepth)
            : (SurfaceChain.TheField, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallSeparatorCommand command)
    {
        if (Unit is not OGSim.Facilities.Separator separator)
            return [SurfaceChain.NothingToUpgrade("separator")];

        if (NextRung(separator) is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{separator.Tier.Id.Value}' is the largest vessel in the catalogue; " +
                "the field cannot be debottlenecked here by installing a bigger one"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        // A failed install leaves the old vessel in place. The money and the
        // months are gone, which is what makes committing to a big capital item a
        // decision rather than a formality — the same shape as a dry hole.
        if (!done.Succeeded) return;

        // Read at COMPLETION, from what is fitted now. An install that finished
        // after another one has to fit the rung above wherever the field
        // actually got to, not the rung above where it was when this was
        // ordered.
        // The vessel can be gone by completion in only one way — a save
        // restored onto a plant that never had one — and fitting a rung to
        // nothing is worse than doing nothing.
        if (Unit is not OGSim.Facilities.Separator separator) return;

        if (NextRung(separator) is OGSim.Facilities.SeparatorTier next) separator.Fit(next);
    }

    /// <summary>
    /// The rung above what is fitted, or null at the top of the ladder.
    ///
    /// <para>Walked in the ladder's declared order (D-5) rather than sorted by
    /// capacity: the catalogue's order is the progression a designer authored,
    /// and a bigger-is-later rule would silently reorder a ladder whose rungs
    /// trade capacity against something else.</para>
    /// </summary>
    private OGSim.Facilities.SeparatorTier? NextRung(OGSim.Facilities.Separator separator)
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == separator.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Fit the next gas plant up (SDD-006 §3b, finding 172).
///
/// <para>THE ANSWER TO A PENALTY. Flaring prices itself into the cost of debt
/// (SDD-012 §4), and until there was a plant to buy that was a tax rather than a
/// decision — a company could be charged for flaring and could do nothing about
/// it but produce less oil.</para>
/// </summary>
public sealed record InstallGasPlantCommand() : Command(Subject: null);

internal sealed class InstallGasPlantActivity(
    ActivityTerms terms,
    SurfaceChain chain,
    IReadOnlyList<OGSim.Facilities.GasPlantTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallGasPlantCommand>(terms)
{
    /// <summary>The vessel this enlarges, or null on ground with no plant.</summary>
    private OGSim.Facilities.GasCapture? Unit => chain.GasPlant;

    /// <summary>A plant is PP&amp;E (SDD-009 §1) — and it now depreciates by the
    /// barrel like everything else the company owns.</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallGasPlantCommand command) =>
        Unit is OGSim.Facilities.GasCapture plant
            ? (new EntityRef(EntityKind.FlowElement, plant.Id.Value), NoDepth)
            : (SurfaceChain.TheField, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallGasPlantCommand command)
    {
        if (Unit is not OGSim.Facilities.GasCapture plant)
            return [SurfaceChain.NothingToUpgrade("gas plant")];

        if (NextRung(plant) is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{plant.Tier.Id.Value}' is the largest gas plant in the catalogue; the " +
                "field cannot handle more gas than it already does"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        // The vessel can be gone by completion in only one way — a save
        // restored onto a plant that never had one — and fitting a rung to
        // nothing is worse than doing nothing.
        if (Unit is not OGSim.Facilities.GasCapture plant) return;

        if (NextRung(plant) is OGSim.Facilities.GasPlantTier next) plant.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.GasPlantTier? NextRung(OGSim.Facilities.GasCapture plant)
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == plant.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Fit a bigger header (SDD-006 §1b, §0c).
///
/// <para>THE ANSWER THE DRILLING REFUSAL ALREADY PROMISED. A well with nowhere
/// to tie in is refused with "a bigger header has to be installed first", and
/// until now nothing could install one — the engine named a remedy it did not
/// have. Eight slots is a long way into a field's life, which is why nobody
/// reached it.</para>
/// </summary>
public sealed record InstallManifoldCommand() : Command(Subject: null);

internal sealed class InstallManifoldActivity(
    ActivityTerms terms,
    SurfaceChain chain,
    IReadOnlyList<OGSim.Facilities.ManifoldTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallManifoldCommand>(terms)
{
    /// <summary>The vessel this enlarges, or null on ground with no plant.</summary>
    private OGSim.Facilities.Manifold? Unit => chain.Manifold;

    /// <summary>Steel on a site: PP&amp;E (SDD-009 §1).</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallManifoldCommand command) =>
        Unit is OGSim.Facilities.Manifold header
            ? (new EntityRef(EntityKind.FlowElement, header.Id.Value), NoDepth)
            : (SurfaceChain.TheField, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallManifoldCommand command)
    {
        if (Unit is not OGSim.Facilities.Manifold header)
            return [SurfaceChain.NothingToUpgrade("header")];

        if (NextRung(header) is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{header.Tier.Id.Value}' is the largest header in the catalogue; the field " +
                "cannot take more wells than it already can"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        // The vessel can be gone by completion in only one way — a save
        // restored onto a plant that never had one — and fitting a rung to
        // nothing is worse than doing nothing.
        if (Unit is not OGSim.Facilities.Manifold header) return;

        if (NextRung(header) is OGSim.Facilities.ManifoldTier next) header.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.ManifoldTier? NextRung(OGSim.Facilities.Manifold header)
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == header.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Build more storage (SDD-006 §5).
///
/// <para>THE THIRD ANSWER TO A FULL TANK. Stage 6's own comment offers "more
/// storage, more export and less production" as the choices a player has when
/// the ullage constraint reaches back down the chain, and storage was the one
/// nothing could buy — the other two shipped and this did not.</para>
///
/// <para>It buys TIME rather than throughput, which is what makes it a
/// different decision from a bigger export line: storage carries a field
/// through a gap, and a pipeline carries it faster for ever.</para>
/// </summary>
public sealed record InstallTankCommand() : Command(Subject: null);

internal sealed class InstallTankActivity(
    ActivityTerms terms,
    SurfaceChain chain,
    IReadOnlyList<OGSim.Facilities.TankTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallTankCommand>(terms)
{
    /// <summary>The vessel this enlarges, or null on ground with no plant.</summary>
    private OGSim.Facilities.Tank? Unit => chain.Tank;

    /// <summary>Civil work a company still owns next month: PP&amp;E
    /// (SDD-009 §1).</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallTankCommand command) =>
        Unit is OGSim.Facilities.Tank tank
            ? (new EntityRef(EntityKind.FlowElement, tank.Id.Value), NoDepth)
            : (SurfaceChain.TheField, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallTankCommand command)
    {
        if (Unit is not OGSim.Facilities.Tank tank)
            return [SurfaceChain.NothingToUpgrade("tank")];

        if (NextRung(tank) is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{tank.Tier.Id.Value}' is the largest tank farm in the catalogue; the field " +
                "cannot hold more than it already can"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        // The vessel can be gone by completion in only one way — a save
        // restored onto a plant that never had one — and fitting a rung to
        // nothing is worse than doing nothing.
        if (Unit is not OGSim.Facilities.Tank tank) return;

        if (NextRung(tank) is OGSim.Facilities.TankTier next) tank.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.TankTier? NextRung(OGSim.Facilities.Tank tank)
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == tank.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Fit a treater (SDD-006 §2, finding 173).
///
/// <para>THE ANSWER TO WET OIL. A field that waters out sells a stream the meter
/// turns away, and until there was a treater to buy that was a tax on getting
/// old rather than a decision. The oil is there and it is worth money; it just
/// cannot be sold with the water still in it.</para>
/// </summary>
public sealed record InstallTreaterCommand() : Command(Subject: null);

internal sealed class InstallTreaterActivity(
    ActivityTerms terms,
    SurfaceChain chain,
    IReadOnlyList<OGSim.Facilities.TreaterTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallTreaterCommand>(terms)
{
    /// <summary>The vessel this enlarges, or null on ground with no plant.</summary>
    private OGSim.Facilities.Treater? Unit => chain.Treater;

    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallTreaterCommand command) =>
        Unit is OGSim.Facilities.Treater treater
            ? (new EntityRef(EntityKind.FlowElement, treater.Id.Value), NoDepth)
            : (SurfaceChain.TheField, NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallTreaterCommand command)
    {
        if (Unit is not OGSim.Facilities.Treater treater)
            return [SurfaceChain.NothingToUpgrade("treater")];

        if (NextRung(treater) is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{treater.Tier.Id.Value}' is the best treating in the catalogue; the field " +
                "cannot dry its oil further than it already does"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        // The vessel can be gone by completion in only one way — a save
        // restored onto a plant that never had one — and fitting a rung to
        // nothing is worse than doing nothing.
        if (Unit is not OGSim.Facilities.Treater treater) return;

        if (NextRung(treater) is OGSim.Facilities.TreaterTier next) treater.Fit(next);
    }

    private OGSim.Facilities.TreaterTier? NextRung(OGSim.Facilities.Treater treater)
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == treater.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Fit a bigger compression train (SDD-006 §3c, R9.1's own composition,
/// finding 257).
///
/// <para>THE SEVENTH SOCKET, and the first the gas leg has had since the
/// plant itself. `GasCapture` has been the field's real sales point since
/// R20d.17; this raises what reaches it, and — through §2.6's heat derating —
/// is R20d.13's own missing consumer, closing the reason a desert field's gas
/// handling was never seasonal.</para>
/// </summary>
public sealed record InstallCompressorCommand() : Command(Subject: null);

internal sealed class InstallCompressorActivity(
    ActivityTerms terms,
    OGSim.Facilities.Compressor compressor,
    IReadOnlyList<OGSim.Facilities.CompressorTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallCompressorCommand>(terms)
{
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallCompressorCommand command) =>
        (new EntityRef(EntityKind.FlowElement, compressor.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallCompressorCommand command)
    {
        if (NextRung() is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{compressor.Tier.Id.Value}' is the largest compression train in the " +
                "catalogue; the field cannot move more gas than it already can"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        if (NextRung() is OGSim.Facilities.CompressorTier next) compressor.Fit(next);
    }

    private OGSim.Facilities.CompressorTier? NextRung()
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == compressor.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Fit a bigger liquid pump station (SDD-006 §3d, R11.2's own composition,
/// finding 259).
///
/// <para>THE EIGHTH SOCKET, the compressor's own shape reused for the oil
/// leg: a separator commonly operates below what downstream treating and
/// export need, and this raises what reaches the treater.</para>
/// </summary>
public sealed record InstallLiquidPumpStationCommand() : Command(Subject: null);

internal sealed class InstallLiquidPumpStationActivity(
    ActivityTerms terms,
    OGSim.Facilities.LiquidPumpStation pumpStation,
    IReadOnlyList<OGSim.Facilities.PumpTier> ladder,
    FacilityLadders ladders,
    OGSim.Capabilities.CapabilityState capabilities,
    OGSim.Capabilities.EraCalendar eras,
    IGatingValidator gate,
    IEffectState effects) : Activity<InstallLiquidPumpStationCommand>(terms)
{
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallLiquidPumpStationCommand command) =>
        (new EntityRef(EntityKind.FlowElement, pumpStation.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallLiquidPumpStationCommand command)
    {
        if (NextRung() is { } next) return RungGate.Buyable(next.Id, ladders, capabilities, eras, gate, effects);

        return
        [
            new RejectionReason(
                "$loc:reject.top-of-the-ladder",
                $"'{pumpStation.Tier.Id.Value}' is the largest pump station in the " +
                "catalogue; the field cannot move more liquid than it already can"),
        ];
    }

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);

        if (!done.Succeeded) return;

        if (NextRung() is OGSim.Facilities.PumpTier next) pumpStation.Fit(next);
    }

    private OGSim.Facilities.PumpTier? NextRung()
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == pumpStation.Tier.Id) return ladder[i + 1];

        return null;
    }
}

/// <summary>
/// Whether a rung can be bought, and why not (SDD-005 §2's R20d.10b amendment).
///
/// <para>TWO CHECKS AND TWO KINDS OF FACT. The era is a CALENDAR comparison made
/// here, because an era that has not arrived is not something a company can go
/// and get and would be a "missing item" with no remedy. The technology is a
/// REQUIREMENT and goes through the one validator §2 specifies — the same road
/// activity gating travels, so equipment cannot drift onto a second one.</para>
///
/// <para>Shared by all five install verbs rather than repeated in each: the
/// question is identical and only the noun differs, and five copies would be
/// five chances for one of them to forget a half.</para>
/// </summary>
internal static class RungGate
{

    public static IReadOnlyList<RejectionReason> Buyable(
        ContentId rung,
        FacilityLadders ladders,
        OGSim.Capabilities.CapabilityState capabilities,
        OGSim.Capabilities.EraCalendar eras,
        IGatingValidator gate,
        IEffectState effects)
    {
        ArgumentNullException.ThrowIfNull(ladders);
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(gate);

        if (!ladders.InventedBy(rung, capabilities.Era))
            return [ladders.NotYet(rung, capabilities.Era, eras)];

        // NO RENTALS: a service rental is scoped to one operation (SDD-005 §2)
        // and a vessel bolted to a deck is not an operation — it outlives the
        // crew that fitted it, so renting the knowledge to install it would leave
        // the company owning equipment it cannot run.
        if (gate.Check(ladders.RequirementsOf(rung), capabilities.Technology, [], effects)
            is not GateFail failed)
            return [];

        var reasons = new List<RejectionReason>(failed.Missing.Count);

        // EVERY miss, never just the first (the R3-V2 principle) — and NAMED, so
        // a player is told which technology to go and get rather than that
        // requirements were not met (R17 §2.6b).
        for (int i = 0; i < failed.Missing.Count; i++)
            if (failed.Missing[i] is MissingTechnology missing)
                reasons.Add(new RejectionReason(
                    "$loc:reject.technology-not-held",
                    $"'{rung.Value}' needs {missing.Tech.Value.Value}, which this " +
                    "company has not acquired"));

        return reasons;
    }
}
