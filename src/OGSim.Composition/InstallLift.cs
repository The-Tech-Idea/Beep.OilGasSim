// R12b.2 — a well can be given a pump (SDD-003 §6.2's amendment, finding 255).
//
// FOUR LIFT METHODS HAVE WORKED SINCE R7 AND NEVER REACHED A WELL. Every
// completion opens on natural flow (`lift: null`) and stays there — the same
// "real mechanism, joined to nothing" shape as findings 149, 200, 207, 252,
// 253 and 254, on the other half of what a well can be given. All four ship:
// the split between them is entirely in the datasheet (§6.2's own "each
// method fills the fields it uses"), so one activity per method, sharing the
// refusal logic and the tubing-geometry reconstruction the way five install
// activities already share `RungGate.Buyable`.
//
// NO RIG. A workover crew is not the drilling rig, the same call R20d.20 and
// R12b.7 both made for a wellsite intervention that is not a rig job.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Company;

namespace OGSim.Composition;

/// <summary>
/// Whether a well can be given ANY lift method, and why not — shared by all
/// four install activities the way <see cref="RungGate"/> is shared by the
/// five equipment installs (SDD-003 §6.2's R12b.2 amendment, finding 255).
/// </summary>
internal static class LiftGate
{
    public static IReadOnlyList<RejectionReason> Buyable(
        FieldControl field, EntityId<ICompletion> well)
    {
        if (field.WellNamed(well) is not OGSim.Wells.Completion completion)
            return
            [
                new RejectionReason(
                    "$loc:reject.no-such-well",
                    $"there is no well {well.Value} to fit a pump to"),
            ];

        if (field.IsWellAbandoned(well))
            return
            [
                new RejectionReason(
                    "$loc:reject.already-abandoned",
                    $"well {well.Value} is plugged; a pump would lift nothing"),
            ];

        // ALREADY LIFTED. A second pump on the same string is not a decision
        // this mechanic models — the player who wants a bigger one is asking
        // for the ladder R12b.2's own amendment named as out of scope.
        if (completion.Lift is not null)
            return
            [
                new RejectionReason(
                    "$loc:reject.already-lifted",
                    $"well {well.Value} already carries a lift method"),
            ];

        return [];
    }

    /// <summary>
    /// THE TUBING GEOMETRY A NEW OUTFLOW MODEL NEEDS is not a fact lost
    /// between drilling and installing lift — it is exactly what
    /// `FieldControl.WellboreNamed` already reconstructs from the
    /// completion's own perforations (`CompletionFor`'s
    /// `(totalDepth, totalDepth, 0.0889 m, 4.6e-5)`), read through the same
    /// door a host asking for this well's geometry would use, rather than a
    /// second derivation of the same fact (law L5).
    /// </summary>
    public static OGSim.Wells.HydrostaticFrictionOutflowModel OutflowFor(
        FieldControl field, EntityId<ICompletion> well, ILiftMethod lift)
    {
        if (field.WellboreNamed(new EntityId<IWellbore>(well.Value)) is not IWellbore wellbore)
            throw new InvariantFault("R1 §2.5", null,
                $"well {well.Value} has a completion but no wellbore at completion");

        Length totalDepth = wellbore.Path.Stations[^1].Md;

        return new OGSim.Wells.HydrostaticFrictionOutflowModel(
            new OGSim.Wells.TubingGeometry(totalDepth, totalDepth, new Length(0.0889), 4.6e-5),
            Defaults.SurfaceOilDensity, lift);
    }

    public static OGSim.Wells.Completion CompletionOf(FieldControl field, EntityRef target)
    {
        var well = new EntityId<ICompletion>(target.Value);

        // Refused before it could run (OwnRefusals), so a null here would be
        // a composition defect rather than a player outcome (R1 §2.5).
        return field.WellNamed(well) as OGSim.Wells.Completion
            ?? throw new InvariantFault("R1 §2.5", null,
                $"well {target.Value} passed validation and no longer exists at completion");
    }

    /// <summary>
    /// Builds the concrete <see cref="ILiftMethod"/> a saved tier id names
    /// (SDD-003 §6's persistence amendment, finding 256) — a RELOAD's own
    /// install, through the same four constructors <c>InstallXxxActivity.
    /// Complete</c> uses, matched against the tier bundle's four ids rather
    /// than a second, hand-maintained kind tag (law L5: `LiftTiers` already
    /// knows which of its four fields is which).
    /// </summary>
    public static ILiftMethod Reconstruct(
        OGSim.Wells.LiftTiers tiers, EntityId<IWellComponent> component,
        ContentId tierId, GameDate installed)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        if (tierId == tiers.RodPump.Id)
            return new OGSim.Wells.RodPump(
                component, tierId, tiers.RodPump.Envelope, installed,
                tiers.RodPump.DisplacementCubicMetresPerSecond);

        if (tierId == tiers.Pcp.Id)
            return new OGSim.Wells.ProgressingCavityPump(
                component, tierId, tiers.Pcp.Envelope, installed,
                tiers.Pcp.DisplacementCubicMetresPerSecond);

        if (tierId == tiers.Esp.Id)
            return new OGSim.Wells.ElectricSubmersiblePump(
                component, tierId, tiers.Esp.Envelope, installed,
                tiers.Esp.HeadCurve, tiers.Esp.Efficiency);

        if (tierId == tiers.GasLift.Id)
            return new OGSim.Wells.GasLift(
                component, tierId, tiers.GasLift.Envelope, installed,
                tiers.GasLift.InjectionRateCubicMetresPerSecond,
                tiers.GasLift.GasDensityKgPerM3);

        throw new SaveDataFault("SDD-013 §2", null,
            $"the save names lift tier '{tierId.Value}' on well {component.Value}; this " +
            "build's catalogue ships no tier with that id, so the well it lifted cannot be " +
            "rebuilt as it was saved");
    }
}

/// <summary>Install a rod pump on a well that has none.</summary>
public sealed record InstallRodPumpCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class InstallRodPumpActivity(
    ActivityTerms terms,
    FieldControl field,
    OGSim.Wells.DisplacementPumpTier tier,
    SimulationClock clock) : Activity<InstallRodPumpCommand>(terms)
{
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallRodPumpCommand command) =>
        (new EntityRef(EntityKind.Completion, command.Well.Value), NoDepth);

    public override string QuantityUnit => "metre";

    /// <summary>The hole the lift is run into (finding 289): rods, cable or
    /// mandrels to depth, so a deep completion costs and takes more.</summary>
    public override double Quantity(InstallRodPumpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return field.WellDepthOf(Aim(command).Target).Metres;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallRodPumpCommand command) =>
        LiftGate.Buyable(field, command.Well);

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);
        if (!done.Succeeded) return;

        OGSim.Wells.Completion completion = LiftGate.CompletionOf(field, done.Target);
        var well = new EntityId<ICompletion>(done.Target.Value);

        var lift = new OGSim.Wells.RodPump(
            new EntityId<IWellComponent>(well.Value), tier.Id, tier.Envelope, clock.Date,
            tier.DisplacementCubicMetresPerSecond);

        completion.InstallLift(lift, LiftGate.OutflowFor(field, well, lift));
    }
}

/// <summary>Install a progressing cavity pump on a well that has none.</summary>
public sealed record InstallPcpCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class InstallPcpActivity(
    ActivityTerms terms,
    FieldControl field,
    OGSim.Wells.DisplacementPumpTier tier,
    SimulationClock clock) : Activity<InstallPcpCommand>(terms)
{
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallPcpCommand command) =>
        (new EntityRef(EntityKind.Completion, command.Well.Value), NoDepth);

    public override string QuantityUnit => "metre";

    /// <summary>The hole the lift is run into (finding 289): rods, cable or
    /// mandrels to depth, so a deep completion costs and takes more.</summary>
    public override double Quantity(InstallPcpCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return field.WellDepthOf(Aim(command).Target).Metres;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallPcpCommand command) =>
        LiftGate.Buyable(field, command.Well);

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);
        if (!done.Succeeded) return;

        OGSim.Wells.Completion completion = LiftGate.CompletionOf(field, done.Target);
        var well = new EntityId<ICompletion>(done.Target.Value);

        var lift = new OGSim.Wells.ProgressingCavityPump(
            new EntityId<IWellComponent>(well.Value), tier.Id, tier.Envelope, clock.Date,
            tier.DisplacementCubicMetresPerSecond);

        completion.InstallLift(lift, LiftGate.OutflowFor(field, well, lift));
    }
}

/// <summary>Install an electric submersible pump on a well that has none.</summary>
public sealed record InstallEspCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class InstallEspActivity(
    ActivityTerms terms,
    FieldControl field,
    OGSim.Wells.EspTier tier,
    SimulationClock clock) : Activity<InstallEspCommand>(terms)
{
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallEspCommand command) =>
        (new EntityRef(EntityKind.Completion, command.Well.Value), NoDepth);

    public override string QuantityUnit => "metre";

    /// <summary>The hole the lift is run into (finding 289): rods, cable or
    /// mandrels to depth, so a deep completion costs and takes more.</summary>
    public override double Quantity(InstallEspCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return field.WellDepthOf(Aim(command).Target).Metres;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallEspCommand command) =>
        LiftGate.Buyable(field, command.Well);

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);
        if (!done.Succeeded) return;

        OGSim.Wells.Completion completion = LiftGate.CompletionOf(field, done.Target);
        var well = new EntityId<ICompletion>(done.Target.Value);

        var lift = new OGSim.Wells.ElectricSubmersiblePump(
            new EntityId<IWellComponent>(well.Value), tier.Id, tier.Envelope, clock.Date,
            tier.HeadCurve, tier.Efficiency);

        completion.InstallLift(lift, LiftGate.OutflowFor(field, well, lift));
    }
}

/// <summary>Install gas lift on a well that has none.</summary>
public sealed record InstallGasLiftCommand(
    EntityId<ICompletion> Well) : Command(Subject: null);

internal sealed class InstallGasLiftActivity(
    ActivityTerms terms,
    FieldControl field,
    OGSim.Wells.GasLiftTier tier,
    SimulationClock clock) : Activity<InstallGasLiftCommand>(terms)
{
    public override bool LeavesAnAsset => true;

    public override MovementCategory Spend => MovementCategory.Development;

    public override bool OnePerTarget => true;

    public override (EntityRef Target, Length Depth) Aim(InstallGasLiftCommand command) =>
        (new EntityRef(EntityKind.Completion, command.Well.Value), NoDepth);

    public override string QuantityUnit => "metre";

    /// <summary>The hole the lift is run into (finding 289): rods, cable or
    /// mandrels to depth, so a deep completion costs and takes more.</summary>
    public override double Quantity(InstallGasLiftCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return field.WellDepthOf(Aim(command).Target).Metres;
    }

    public override IReadOnlyList<RejectionReason> OwnRefusals(InstallGasLiftCommand command) =>
        LiftGate.Buyable(field, command.Well);

    public override void Complete(CompletedActivity done, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(done);
        if (!done.Succeeded) return;

        OGSim.Wells.Completion completion = LiftGate.CompletionOf(field, done.Target);
        var well = new EntityId<ICompletion>(done.Target.Value);

        var lift = new OGSim.Wells.GasLift(
            new EntityId<IWellComponent>(well.Value), tier.Id, tier.Envelope, clock.Date,
            tier.InjectionRateCubicMetresPerSecond, tier.GasDensityKgPerM3);

        completion.InstallLift(lift, LiftGate.OutflowFor(field, well, lift));
    }
}
