// R5.4 — the six shipped drive mechanisms (design 02 §2.2, SDD-003 §4.2/§4.2b).
//
// A plugin deliberately. Recovery factor is never stored anywhere: it is
// whatever the material balance and the mechanism produce together (R5 G2),
// which is what makes identifying your drive worth doing, and what makes
// waterflood (R10) and gas injection (R9) ADDITIONS rather than edits to these.
//
// What distinguishes them is WHICH TERMS OF §3.1's BALANCE THEY ADMIT (§4.2b).
// That is not a label: a mechanism refuses a compartment that contradicts it, so
// a solution-gas drive handed a gas cap is caught when the compartment is built
// rather than surfacing two hundred ticks later as a recovery factor nobody can
// account for. The root-find itself is shared — six copies of one bisection
// would be six chances for them to drift apart.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

internal abstract class DriveMechanism : IDriveMechanism
{
    /// <summary>Content default, SDD-003 §3.1.</summary>
    protected const double DefaultMaxTickVoidageFraction = 0.25;

    /// <summary>
    /// Injectants are a CONSTRUCTOR argument rather than an overridable property:
    /// a derived class hiding the base one with `new` would still hand the base's
    /// empty list to anything holding an IDriveMechanism, which is how every
    /// caller holds it.
    ///
    /// <para>REQUIRED, not defaulted (law L2). "This drive takes no injectant" is
    /// a statement each mechanism should make, and a default would let a new
    /// mechanism silently accept nothing because its author forgot to say.</para>
    /// </summary>
    protected DriveMechanism(
        string id, AdmittedTerms admits, IReadOnlyList<ContentId> acceptedInjectants)
    {
        ArgumentNullException.ThrowIfNull(admits);
        ArgumentNullException.ThrowIfNull(acceptedInjectants);

        Id = new ContentId(id);
        Admits = admits;
        AcceptedInjectants = acceptedInjectants;
    }

    public ContentId Id { get; }

    public AdmittedTerms Admits { get; }

    public IReadOnlyList<ContentId> AcceptedInjectants { get; }

    protected virtual double MaxTickVoidageFraction => DefaultMaxTickVoidageFraction;

    public Pressure SolveEndPressure(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fluid);

        AssertCoherent(input);
        return MaterialBalance.Solve(input, fluid, MaxTickVoidageFraction);
    }

    /// <summary>
    /// The whole history at once, for a restore (SDD-003 §3.1's R20d.12.17
    /// amendment, finding 205). NO STEP LIMIT, because there is no step.
    ///
    /// <para>§3.1's fraction exists because one-step monthly integration is not
    /// honest at a large step size. A restore is not integrating a step — it is
    /// evaluating the balance at a point the engine has ALREADY reached, one
    /// honest month at a time — so measuring the entire depletion against a
    /// per-tick limit refuses a state the engine itself produced. It did: a
    /// compartment that had given up more than a quarter of its initial pressure
    /// could not be loaded at all, which is every field that is either small or
    /// old.</para>
    ///
    /// <para>A SEPARATE MEMBER rather than a flag on the one above. A boolean
    /// would put "skip the guard" one wrong argument away from the per-tick path,
    /// and the guard is the only thing standing between a monthly solve and a
    /// number whose error nobody can bound.</para>
    ///
    /// <para><see cref="AssertCoherent"/> still runs: a compartment carrying
    /// influx its drive does not admit is wrong however it got there (§4.2b).</para>
    /// </summary>
    public Pressure SolveFromInitial(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fluid);

        AssertCoherent(input);
        return MaterialBalance.SolveWithoutStepLimit(input, fluid);
    }

    /// <summary>
    /// §4.2b. The compartment must not carry a term this drive does not admit.
    ///
    /// <para>A model fault, not a silent zeroing. Ignoring the gas cap of a
    /// compartment declared as solution-gas would give an answer — a wrong one,
    /// and one whose wrongness is invisible because the recovery factor it
    /// produces is still a plausible number. The content is what is broken, and
    /// this is where it is still cheap to say so.</para>
    /// </summary>
    private void AssertCoherent(MaterialBalanceInput input)
    {
        if (!Admits.GasCap && input.GasCapRatio != 0.0)
            throw new ModelFault("SDD-003 §4.2b", null,
                $"drive {Id.Value} does not admit a gas cap, but the compartment declares " +
                $"m = {Format(input.GasCapRatio)}. Either the drive or the content is wrong");

        if (!Admits.AquiferInflux && input.CumulativeWaterInflux.CubicMetres != 0.0)
            throw new ModelFault("SDD-003 §4.2b", null,
                $"drive {Id.Value} does not admit aquifer influx, but the compartment has " +
                $"taken {Format(input.CumulativeWaterInflux.CubicMetres)} reservoir m³ of it");

        if (Admits.GasCap && input.GasCapRatio <= 0.0)
            throw new ModelFault("SDD-003 §4.2b", null,
                $"drive {Id.Value} is a gas-cap drive, but the compartment declares no gas cap " +
                "(m = 0) — it would behave as solution gas under a name promising otherwise");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Solution gas drive: the only energy is gas coming out of solution and the
/// rock and connate water expanding. Pressure falls steeply, recovery is poor —
/// band MB2, 5–30% (R5-V4).
///
/// <para>The poor recovery is not a penalty applied to the balance; it is what
/// the balance gives when <c>m = 0</c> and <c>We = 0</c> leave only the oil's own
/// expansion, which runs out quickly.</para>
/// </summary>
internal sealed class SolutionGasDrive()
    : DriveMechanism("solution-gas-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false), []);

/// <summary>
/// Gas cap expansion. The cap does the work, so pressure is better supported
/// than by solution gas alone — provided the cap is not produced. Band 20–40%.
/// </summary>
internal sealed class GasCapExpansionDrive()
    : DriveMechanism("gas-cap-expansion-drive", new AdmittedTerms(GasCap: true, AquiferInflux: false), []);

/// <summary>
/// Water drive. Influx replaces produced volume, so pressure is maintained and
/// recovery is the best of the natural drives — band MB1, 35–75% (R5-V3).
///
/// <para>The DRIVE does not generate water; the aquifer does, and the influx
/// arrives as <c>We</c>. Keeping that separation is what lets R5-V8 test the
/// aquifer independently of the drive at all.</para>
/// </summary>
internal sealed class WaterDrive()
    : DriveMechanism("water-drive", new AdmittedTerms(GasCap: false, AquiferInflux: true), [])
{
    // A supported compartment moves slowly in pressure, so a larger step is
    // still inside the integration's honest range.
    protected override double MaxTickVoidageFraction => 0.4;
}

/// <summary>
/// Compaction drive. The rock itself gives up pore volume as pressure falls.
///
/// <para>It admits the same terms as solution gas and is distinguished by its
/// CONTENT — a large rock compressibility — not by code. Non-negotiable 11:
/// giving it a fabricated term merely to look different in the type system would
/// be inventing physics no design document states.</para>
/// </summary>
internal sealed class CompactionDrive()
    : DriveMechanism("compaction-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false), []);

/// <summary>
/// Gravity drainage. Oil drains downward under its own weight; slow, and its
/// recovery depends on rate discipline rather than on pressure — band 40–70%.
///
/// <para>At tank fidelity there is no vertical dimension to drain along, so what
/// it carries in code is the tighter per-tick limit: a gravity-drainage
/// compartment produced hard loses the segregation that makes it work, and that
/// is a failure the monthly step must not step over.</para>
/// </summary>
internal sealed class GravityDrainageDrive()
    : DriveMechanism("gravity-drainage-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false), [])
{
    protected override double MaxTickVoidageFraction => 0.1;
}

/// <summary>
/// Combination drive: a gas cap above, an aquifer below, solution gas between.
/// The common real case, and the one whose recovery lands between its parts.
///
/// <para>It admits every term, which is exactly why it needs no arithmetic of
/// its own — §3.1's balance already sums them. It exists as a named, selectable
/// thing because the PLAYER's task is to work out which drive they have, and a
/// drive that could not be named could not be guessed at.</para>
/// </summary>
internal sealed class CombinationDrive()
    : DriveMechanism("combination-drive", new AdmittedTerms(GasCap: true, AquiferInflux: true), []);

/// <summary>
/// R10.4 — waterflood. Injected water replaces produced volume, so pressure is
/// supported and recovery rises well above the natural drives (R10-V6).
///
/// <para><b>An ADDITION, not an edit</b> (R10 §2.3, design 02 §2.2). A
/// waterflood compartment has a different pressure response because it has a
/// different drive mechanism, not because a flag was set on an existing one —
/// which is the whole reason `IDriveMechanism` is a plugin slot.</para>
///
/// <para>It admits aquifer influx as well as injection, because the balance
/// cannot tell them apart and neither can the reservoir: water arriving from an
/// aquifer and water arriving from an injector both replace voidage. A flood on
/// a compartment that also has an aquifer is a real and common case, and
/// refusing it would have been an artefact of the type system rather than of
/// the physics.</para>
/// </summary>
/// <para>Water is what it accepts, named by CONTENT ID rather than branched on by
/// material identity — SDD-005 §4.0b and the "one engine" architecture test.</para>
internal sealed class WaterfloodDrive()
    : DriveMechanism("waterflood-drive",
                     new AdmittedTerms(GasCap: false, AquiferInflux: true),
                     [new ContentId("water")])
{
    // A flood holds pressure up, so the compartment moves slowly and a larger
    // step stays inside the integration's honest range — the same reasoning as
    // the natural water drive it replaces.
    protected override double MaxTickVoidageFraction => 0.4;

}

/// <summary>
/// SDD-003 §3.1b's finding-264 amendment — the dry-gas balance, dispatched.
///
/// <para>NOT a <see cref="DriveMechanism"/> subclass: that base class's
/// <c>SolveEndPressure</c> calls §3.1's OIL form unconditionally. A dry-gas
/// compartment needs a different equation entirely
/// (<see cref="GasMaterialBalance"/>, R5.7), not a different set of admitted
/// terms on the same one.</para>
/// </summary>
internal sealed class VolumetricGasDrive : IDriveMechanism
{
    public ContentId Id { get; } = new("volumetric-gas-drive");

    // Water drive bends the p/Z line (05 §3.2) — a real and common case for a
    // gas reservoir, admitted the same way WaterDrive admits it for oil.
    // GasCap is an OIL-ZONE ratio (m); a dry-gas compartment has none.
    public AdmittedTerms Admits { get; } = new(GasCap: false, AquiferInflux: true);

    // Nothing is injected into a gas reservoir by this composition yet — R10's
    // waterflood targets oil compartments, and extending it to gas storage is
    // its own future task, not assumed here.
    public IReadOnlyList<ContentId> AcceptedInjectants { get; } = [];

    public Pressure SolveEndPressure(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fluid);

        return Solve(input, fluid);
    }

    /// <summary>
    /// NO SEPARATE STEP LIMIT, unlike the oil drives' own restore path. §3.1's
    /// per-tick fraction exists because Bo/Bg/Rs integrate nonlinearly across a
    /// large pressure step; the gas balance is LINEAR in cumulative production
    /// by construction (§3.1b) and has no analogous integration error to bound
    /// — so there is only one Solve, honest at any step size.
    /// </summary>
    public Pressure SolveFromInitial(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(fluid);

        return Solve(input, fluid);
    }

    private static Pressure Solve(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        AssertCoherent(input);

        double initialZ = fluid.Z(input.InitialPressure, input.ReservoirTemperature);
        GasFormationVolumeFactor initialBg = fluid.Bg(input.InitialPressure);

        // Water that has invaded the gas-occupied pore volume, whatever its
        // provenance — an aquifer and an injector both replace voidage the
        // same way, and the balance cannot tell them apart (mirroring
        // WaterfloodDrive's own reasoning for the oil form).
        var netWaterInflux = new ReservoirVolume(
            input.CumulativeWaterInflux.CubicMetres + input.CumulativeInjected.CubicMetres);

        return GasMaterialBalance.Solve(
            input.InitialPressure, initialZ, input.GasInPlace, input.CumulativeGasProduced,
            netWaterInflux, initialBg, fluid, input.ReservoirTemperature);
    }

    /// <summary>The gas balance's own coherence, mirroring §4.2b's oil-side check.</summary>
    private static void AssertCoherent(MaterialBalanceInput input)
    {
        if (input.OriginalOilInPlace.CubicMetres != 0.0)
            throw new ModelFault("SDD-003 §3.1b", null,
                "a volumetric gas drive was handed a compartment declaring " +
                $"{Format(input.OriginalOilInPlace.CubicMetres)} m³ of original oil in place; " +
                "the dry-gas form has no term for it");

        if (input.GasInPlace.CubicMetres <= 0.0)
            throw new ModelFault("SDD-003 §3.1b", null,
                "a volumetric gas drive was handed a compartment declaring no gas in place; " +
                "the p/Z line has no denominator without one");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
