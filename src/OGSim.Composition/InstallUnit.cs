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
    OGSim.Facilities.Separator separator,
    IReadOnlyList<OGSim.Facilities.SeparatorTier> ladder) : Activity<InstallSeparatorCommand>(terms)
{
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
        (new EntityRef(EntityKind.FlowElement, separator.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallSeparatorCommand command)
    {
        if (NextRung() is not null) return [];

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
        if (NextRung() is OGSim.Facilities.SeparatorTier next) separator.Fit(next);
    }

    /// <summary>
    /// The rung above what is fitted, or null at the top of the ladder.
    ///
    /// <para>Walked in the ladder's declared order (D-5) rather than sorted by
    /// capacity: the catalogue's order is the progression a designer authored,
    /// and a bigger-is-later rule would silently reorder a ladder whose rungs
    /// trade capacity against something else.</para>
    /// </summary>
    private OGSim.Facilities.SeparatorTier? NextRung()
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
    OGSim.Facilities.GasCapture plant,
    IReadOnlyList<OGSim.Facilities.GasPlantTier> ladder) : Activity<InstallGasPlantCommand>(terms)
{
    /// <summary>A plant is PP&amp;E (SDD-009 §1) — and it now depreciates by the
    /// barrel like everything else the company owns.</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallGasPlantCommand command) =>
        (new EntityRef(EntityKind.FlowElement, plant.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallGasPlantCommand command)
    {
        if (NextRung() is not null) return [];

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

        if (NextRung() is OGSim.Facilities.GasPlantTier next) plant.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.GasPlantTier? NextRung()
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
    OGSim.Facilities.Manifold header,
    IReadOnlyList<OGSim.Facilities.ManifoldTier> ladder) : Activity<InstallManifoldCommand>(terms)
{
    /// <summary>Steel on a site: PP&amp;E (SDD-009 §1).</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallManifoldCommand command) =>
        (new EntityRef(EntityKind.FlowElement, header.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallManifoldCommand command)
    {
        if (NextRung() is not null) return [];

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

        if (NextRung() is OGSim.Facilities.ManifoldTier next) header.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.ManifoldTier? NextRung()
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
    OGSim.Facilities.Tank tank,
    IReadOnlyList<OGSim.Facilities.TankTier> ladder) : Activity<InstallTankCommand>(terms)
{
    /// <summary>Civil work a company still owns next month: PP&amp;E
    /// (SDD-009 §1).</summary>
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallTankCommand command) =>
        (new EntityRef(EntityKind.FlowElement, tank.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallTankCommand command)
    {
        if (NextRung() is not null) return [];

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

        if (NextRung() is OGSim.Facilities.TankTier next) tank.Fit(next);
    }

    /// <summary>The rung above what is fitted, in the ladder's declared order
    /// (D-5).</summary>
    private OGSim.Facilities.TankTier? NextRung()
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
    OGSim.Facilities.Treater treater,
    IReadOnlyList<OGSim.Facilities.TreaterTier> ladder) : Activity<InstallTreaterCommand>(terms)
{
    /// <summary>A rung on the surface ladder (finding 225).</summary>
    public override MovementCategory Spend => MovementCategory.Development;

    public override bool LeavesAnAsset => true;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallTreaterCommand command) =>
        (new EntityRef(EntityKind.FlowElement, treater.Id.Value), NoDepth);

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallTreaterCommand command)
    {
        if (NextRung() is not null) return [];

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

        if (NextRung() is OGSim.Facilities.TreaterTier next) treater.Fit(next);
    }

    private OGSim.Facilities.TreaterTier? NextRung()
    {
        for (int i = 0; i < ladder.Count - 1; i++)
            if (ladder[i].Id == treater.Tier.Id) return ladder[i + 1];

        return null;
    }
}
