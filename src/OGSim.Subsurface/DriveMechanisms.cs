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

    protected DriveMechanism(string id, AdmittedTerms admits)
    {
        ArgumentNullException.ThrowIfNull(admits);

        Id = new ContentId(id);
        Admits = admits;
        AcceptedInjectants = [];      // natural drives take none; R9/R10 add their own
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
    : DriveMechanism("solution-gas-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false));

/// <summary>
/// Gas cap expansion. The cap does the work, so pressure is better supported
/// than by solution gas alone — provided the cap is not produced. Band 20–40%.
/// </summary>
internal sealed class GasCapExpansionDrive()
    : DriveMechanism("gas-cap-expansion-drive", new AdmittedTerms(GasCap: true, AquiferInflux: false));

/// <summary>
/// Water drive. Influx replaces produced volume, so pressure is maintained and
/// recovery is the best of the natural drives — band MB1, 35–75% (R5-V3).
///
/// <para>The DRIVE does not generate water; the aquifer does, and the influx
/// arrives as <c>We</c>. Keeping that separation is what lets R5-V8 test the
/// aquifer independently of the drive at all.</para>
/// </summary>
internal sealed class WaterDrive()
    : DriveMechanism("water-drive", new AdmittedTerms(GasCap: false, AquiferInflux: true))
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
    : DriveMechanism("compaction-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false));

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
    : DriveMechanism("gravity-drainage-drive", new AdmittedTerms(GasCap: false, AquiferInflux: false))
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
    : DriveMechanism("combination-drive", new AdmittedTerms(GasCap: true, AquiferInflux: true));
