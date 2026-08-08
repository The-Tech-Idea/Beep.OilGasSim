// R25.1 — the arcade fluid model (design 18 §5b.1, SDD-005 §7b).
//
// THE SAME GAME, COMPUTED MORE SIMPLY. Design 18 §5b's fidelity axis is
// per-model plugin selection: arcade, standard and simulation implementations of
// the same slot, chosen by a reality profile. This is the arcade one for fluid
// properties — the "simple flight model" beside the full one.
//
// IT IS NOT A STUB AND NOT A LESSER GAME. Every number it returns is a real
// number a real fluid could have; what it drops is the PRESSURE DEPENDENCE that
// a player cannot perceive. Standing's correlation says a 35° API oil at 200 bar
// holds a different Rs than at 100 — true, load-bearing for an engineer, and
// invisible to someone deciding whether to drill: what they see is barrels and
// a decline curve, and both survive this intact.
//
// WHAT IT DELIBERATELY KEEPS is everything a decision is made on. Oil still
// shrinks on the way to the tank, gas still comes out of solution, the phase
// split is still by material — so the chain, the separator and the meter all
// behave exactly as they do at full fidelity. The arcade dial is under the
// physics, never under the game.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// SDD-005 §7b — design 18 §5b's fidelity axis, as content: a named bundle of
/// model selections applied at composition.
///
/// <para>Not a new mechanism. <see cref="SetModelSelection"/> is the same effect
/// technology issues mid-game (SDD-005 §4) and <c>PluginRegistry</c> is the same
/// registry it resolves through — a profile just makes those selections before
/// the first tick, because fidelity is what a run is PLAYED at rather than
/// something earned during it.</para>
///
/// <para>A profile names slots and never all of them: an unnamed slot keeps what
/// its module composed, which is why <c>standard</c> is legitimately the empty
/// bundle. Only a departure from the shipped set needs stating.</para>
/// </summary>
public sealed record RealityProfile(
    ContentId Id,
    IReadOnlyList<SetModelSelection> Fidelity)
{
    // Finding 131.
    public bool Equals(RealityProfile? other) =>
        other is not null && Id == other.Id && Structural.Equal(Fidelity, other.Fidelity);

    public override int GetHashCode() => HashCode.Combine(Id, Structural.HashOf(Fidelity));

    /// <summary>Which plugin this profile wants in a slot, or null if it does not
    /// care and the module's own choice stands.</summary>
    public ContentId? Selected(ModelSlot slot)
    {
        for (int i = 0; i < Fidelity.Count; i++)
            if (Fidelity[i].Slot == slot) return Fidelity[i].Plugin;

        return null;
    }
}

/// <summary>
/// SDD-005 §7b's arcade implementation of design 03 §3.2's fluid-property slot.
///
/// <para>Constant factors rather than correlations. It is registered under its
/// own name and selected by a reality profile, which is what makes fidelity a
/// mode a player picks rather than a decision the engine made for them.</para>
/// </summary>
internal sealed class ArcadeFluidModel : IFluidPropertyModel
{
    private readonly BlackOilInputs _inputs;
    private readonly FormationVolumeFactor _bo;
    private readonly IMaterialCatalog _materials;

    public ArcadeFluidModel(
        BlackOilInputs inputs,
        FormationVolumeFactor oilFormationVolumeFactor,
        ValidityRange validity,
        IMaterialCatalog materials)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(validity);
        ArgumentNullException.ThrowIfNull(materials);

        _inputs = inputs;
        _bo = oilFormationVolumeFactor;
        _materials = materials;
        Validity = validity;
    }

    public ContentId Id { get; } = new("arcade-fluid");

    public FluidForm Form => _inputs.Form;

    public ValidityRange Validity { get; }

    /// <summary>
    /// Bubble point, kept because it is not decoration: the material balance
    /// branches on whether the reservoir is above or below it, and a player sees
    /// that branch as the moment their GOR starts climbing.
    /// </summary>
    public Pressure Pb { get; } = Pressure.FromBar(50.0);

    /// <summary>
    /// One factor at every pressure — and it is the SAME factor the shipped
    /// completion converts at, which closes finding 160 in this mode: at arcade
    /// fidelity the well's conversion and the engine's agree exactly, because
    /// there is only one number.
    /// </summary>
    public FormationVolumeFactor Bo(Pressure p) => _bo;

    /// <summary>Solution GOR at its bubble-point value everywhere. Gas still
    /// comes out of solution; how much no longer depends on depth.</summary>
    public double Rs(Pressure p) => _inputs.SolutionGorAtBubblePoint;

    /// <summary>Black oil carries no vaporised oil, at any fidelity.</summary>
    public double Rv(Pressure p) => 0.0;

    public GasFormationVolumeFactor Bg(Pressure p) => new(ArcadeGasFormationVolumeFactor);

    public FormationVolumeFactor Bw(Pressure p) => new(1.0);

    public Viscosity MuOil(Pressure p) => new(ArcadeOilViscosityPaS);

    public Viscosity MuGas(Pressure p) => new(ArcadeGasViscosityPaS);

    /// <summary>Ideal gas. The compressibility factor is exactly the kind of
    /// correction a player never sees.</summary>
    public double Z(Pressure p, Temperature t) => 1.0;

    /// <summary>
    /// By the material's standard-conditions phase, exactly as the full model
    /// does — because this is not a simplification, it is what a phase IS. An
    /// arcade separator that sent oil down the gas leg would be a different
    /// game, not a simpler one.
    /// </summary>
    public PhaseSplit SplitAt(
        OGSim.Kernel.Composition composition, Pressure p, Temperature t)
    {
        var fractions =
            new List<(MaterialId, double, double, double)>(composition.Length);

        for (int ordinal = 0; ordinal < composition.Length; ordinal++)
        {
            var id = new MaterialId(ordinal);

            fractions.Add(_materials[id].Phase switch
            {
                PhaseAtStandardConditions.Gas => (id, 1.0, 0.0, 0.0),
                PhaseAtStandardConditions.Aqueous => (id, 0.0, 0.0, 1.0),
                _ => (id, 0.0, 1.0, 0.0),
            });
        }

        return new PhaseSplit(fractions);
    }

    // The three numbers this model does not vary. Named rather than written into
    // the returns, because F-2 does not relax for a simpler model — an arcade
    // constant is still a constant somebody has to be able to find and change.

    /// <summary>Bg, rm³/sm³ — gas at about 200 times its surface volume, a
    /// typical shallow-reservoir figure.</summary>
    private const double ArcadeGasFormationVolumeFactor = 0.005;

    /// <summary>A light crude, Pa·s.</summary>
    private const double ArcadeOilViscosityPaS = 2.0e-3;

    /// <summary>Hydrocarbon gas, Pa·s.</summary>
    private const double ArcadeGasViscosityPaS = 1.5e-5;
}
