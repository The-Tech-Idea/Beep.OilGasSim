// Plans 27 — the one place a composition-time condition is answered.
//
// THE MODULE REVIEW (plans/modules/90_CONDITIONS.md) found ten places a module
// decided for itself whether a mechanic exists or what a value is — two of them
// decided TWICE, once in a manifest and once in `Compose`, kept in step by
// nothing but a comment. This class is the extraction the review asked for:
// every entry carries the engine's own default, a style departs through its
// terms, and a module ASKS rather than deciding.
//
// NOT A MECHANICSET. That has been tried and removed once already (see
// `StyleTerms`' remarks): booleans threaded beside the terms they described,
// asking "which game is this". The difference here is ownership — the manager
// REPLACES the decision sites rather than riding alongside them. A module never
// asks which style it is in; it asks for its own dependency, and the answer was
// fixed before any module was constructed.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// The composition-time questions modules used to answer for themselves —
/// plans 27 §3, one entry per condition or static the module review found.
/// </summary>
public enum DependencyId
{
    /// <summary>C1 — does the company open holding a commissioned plant?
    /// Presence only.</summary>
    OpensHoldingAPlant,

    /// <summary>C7 — the opening balance. Value: <see cref="Money"/>.</summary>
    OpeningCash,

    /// <summary>C8/C9 — does tenure govern work at all? Presence only: the
    /// licence OBJECT is composed in every build (the read model promises one
    /// licence, always); presence switches the commitment stage and the
    /// tenure refusal.</summary>
    Tenure,

    /// <summary>Found in the plan revision — are <c>Borrow</c> and <c>Repay</c>
    /// offered at all? Presence only.</summary>
    Banking,

    /// <summary>Found by the balance measurement — does the company open under
    /// an offtake commitment? Presence only.</summary>
    TakeOrPay,

    /// <summary>C2/C3 — the price collar.
    /// Value: <c>OGSim.Company.HedgeTerms</c>.</summary>
    Hedging,

    /// <summary>C4/C5/C6 — the cover.
    /// Value: <c>OGSim.Company.InsuranceTerms</c>.</summary>
    Insurance,

    /// <summary>C10 — which registered fluid model binds.
    /// Value: <see cref="ContentId"/>.</summary>
    FluidModel,

    /// <summary>S1 — the fiscal take. Value: <see cref="IFiscalRegime"/>.</summary>
    FiscalRegime,

    /// <summary>S2 — how often equipment fails.
    /// Value: <c>OGSim.Integrity.ExponentialHazardModel</c>.</summary>
    HazardRate,

    /// <summary>S5 — the solver's numbers.
    /// Value: <c>OGSim.Flow.SolverSettings</c>.</summary>
    SolverSettings,
}

/// <summary>
/// Which mechanics this build carries, and at what values — plans 27 §2.
/// </summary>
/// <remarks>
/// <para>Three rules. <b>Complete by construction</b>: every entry is resolved
/// here, so a dependency added next year is ON everywhere and nobody has to
/// remember it. <b>A module asks; it never decides</b>: the facilities module
/// asks whether the company opens holding a plant — it does not know what a
/// starting state is. <b>Presence and value are separate questions</b>: five
/// validators refuse zeroed content (<c>WorkingInterest</c>, <c>Hedge</c>,
/// <c>BowTie</c>, <c>ClimateProfile</c>, <c>CrewState</c> — §C of the module
/// review), so "off" means the entry is ABSENT and its stage never claimed,
/// never a harmless number.</para>
/// </remarks>
public sealed class DependencyManager
{
    private readonly IReadOnlyDictionary<DependencyId, object> _held;

    private DependencyManager(IReadOnlyDictionary<DependencyId, object> held) => _held = held;

    /// <summary>
    /// The build's dependency set, resolved once from the style's terms, the
    /// starting state and the reality profile — the three axes that previously
    /// reached into module bodies as raw parameters.
    /// </summary>
    public static DependencyManager For(
        StyleTerms terms, StartingStateDefinition startingState, RealityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(startingState);
        ArgumentNullException.ThrowIfNull(profile);

        var held = new Dictionary<DependencyId, object>
        {
            // From content (plans 28): the opening balance is a designer
            // number, and `content/starting-states/` is its one owner. An
            // unknown starting state was already refused by name when the
            // definition was resolved from the loaded catalogue.
            [DependencyId.OpeningCash] = Money.FromMillions(startingState.OpeningCashMillions),

            // The profile's selection, or the module's own black-oil choice
            // when the slot is unnamed — the fallback the materials module used
            // to make silently (C10), made once, here, where it can be seen.
            [DependencyId.FluidModel] =
                profile.Selected(Defaults.FluidSlot) is ContentId chosen
                    ? chosen
                    : Defaults.BlackOilCorrelations,

            // S1/S2/S5 of the review — constructed fresh per build (the regime
            // carries a loss carryforward, and finding 261 names the static
            // leak that sharing would be), from values `Defaults` now names
            // where module bodies used to hold them as unreachable literals.
            [DependencyId.FiscalRegime] = new OGSim.Company.RoyaltyTaxRegime(
                new ContentId("concession"),
                royaltyRate: Defaults.RoyaltyRate, taxRate: Defaults.TaxRate),

            [DependencyId.HazardRate] = new OGSim.Integrity.ExponentialHazardModel(
                new ContentId("standard"),
                Defaults.HazardBaseRatePerYear, Defaults.HazardConditionExponent),

            [DependencyId.SolverSettings] = OGSim.Flow.SolverSettings.Pinned,
        };

        if (startingState.HoldsPlant)
            held[DependencyId.OpensHoldingAPlant] = true;

        if (terms.Tenure)
            held[DependencyId.Tenure] = true;

        if (terms.Banking)
            held[DependencyId.Banking] = true;

        if (terms.TakeOrPay)
            held[DependencyId.TakeOrPay] = true;

        if (terms.Hedge is OGSim.Company.HedgeTerms collar)
            held[DependencyId.Hedging] = collar;

        if (terms.Insurance is OGSim.Company.InsuranceTerms cover)
            held[DependencyId.Insurance] = cover;

        return new DependencyManager(held);
    }

    /// <summary>Does this build carry the mechanic at all?</summary>
    public bool Has(DependencyId id) => _held.ContainsKey(id);

    /// <summary>
    /// The value the entry carries. Refuses when absent: asking for the value
    /// of a mechanic the build does not carry is a wiring defect, never a
    /// question with a default answer (law L2).
    /// </summary>
    public T ValueOf<T>(DependencyId id)
    {
        if (!_held.TryGetValue(id, out object? value))
            throw new InvariantFault("plans 27 §2", null,
                $"'{id}' is not a mechanic this build carries; a module must ask " +
                "Has before ValueOf, and a caller that did not is wired wrongly");

        if (value is not T typed)
            throw new InvariantFault("plans 27 §2", null,
                $"'{id}' carries a {value.GetType().Name}, not the " +
                $"{typeof(T).Name} asked for");

        return typed;
    }
}
