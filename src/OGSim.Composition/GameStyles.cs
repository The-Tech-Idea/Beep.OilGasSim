// Plans 25 — the two products, as two styles over one engine.
//
// THE NEUTRAL TERMS LIVE HERE, not in Defaults. The engine keeps the real
// licence, the real cover, the real collar and the real farm-down; a style that
// wants one of them to do nothing says so itself. That is what "OGSim remains
// full-featured" means in code: read this file top to bottom and the engine has
// lost nothing, it has only been asked for less.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// <b>Oilfield Days</b> — a builder in the Settlers / Factorio / Foundation
/// family.
/// </summary>
/// <remarks>
/// <para>A yard, a crew and a balance. A dark map and prospecting from
/// Settlers, a production chain whose throughput and bottlenecks are the skill
/// from Factorio, and siting things where the ground suits them from
/// Foundation.</para>
///
/// <para><b>What it does not have, and why</b> (plans 25 §4.3, scored against
/// design 20 §1's decision test): a licence work commitment, a farm-down, a
/// takeover, a price collar, insurance and demurrage. Each fails at least two of
/// T1-T4 for a builder — no decision, no information to reason with, no
/// observable consequence, or no coupling to anything else. They are not made
/// gentler; they are handed terms under which they do nothing.</para>
///
/// <para>Everything a builder <i>does</i> want stays real and untouched:
/// exploration and the dark map, drilling, the chain, wear and repair, weather,
/// depletion, the market, technology eras, crews, rivals, ESG, take-or-pay and
/// the bank.</para>
/// </remarks>
public sealed class DaysStyle : IGameStyle
{
    public ContentId Id { get; } = new("days");

    public string Title => "Oilfield Days";

    public string Premise =>
        "A yard, a crew and a balance. Find something, then build what it takes to sell it.";

    public StyleTerms Terms { get; } = new(
        Licence: NoLicence,
        Insurance: null,
        Hedge: null,
        WorkingInterest: Defaults.WorkingInterest,
        DemurrageRate: 0.0,
        // A THRESHOLD NO RUN REACHES. Belt and braces beside the note above:
        // the takeover cannot fire in a game where nothing is ever sold, and
        // this says so in the style rather than relying on it.
        TakeoverAfterAmortisingTicks: int.MaxValue,

        // THE TWO MECHANICS DAYS DOES NOT CARRY (plans 26 §4, W5). Not
        // neutralised — ABSENT: no commitment stage, no tenure refusal, no
        // Borrow or Repay on offer. The licence object itself is still
        // composed, with the unlosable terms above, because the read model
        // promises one licence, always. Measured beside the change: the W9
        // winnability runs.
        Tenure: false,
        Banking: false,
        TakeOrPay: false);

    public ContentId RealityProfile { get; } = new("arcade");

    public ContentId StartingState => StartingStates.BareGround;

    public ContentId Rules => RuleSets.Frontier.Id;

    public EngineSettings Compose(EngineSettings baseline) => Style.Write(this, baseline);

    /// <summary>
    /// A licence that cannot be lost.
    /// </summary>
    /// <remarks>
    /// <para>No work commitment, no bond and a term no run reaches. The stage
    /// still runs, <c>DrillWellActivity</c> still asks whether the licence is
    /// live, and the read model still carries a licence view — nothing learns
    /// which game it is in.</para>
    ///
    /// <para><b>The fiscal regime is kept.</b> Tax is not part of the licence
    /// mechanic: a company still pays what it owes on what it sells, in every
    /// build. Switching a licence off is not switching off the state.</para>
    ///
    /// <para>A thousand years rather than <c>int.MaxValue</c> months: the expiry
    /// is compared against a tick, and a term that overflowed on arithmetic
    /// nobody re-reads would be a trap rather than a neutral value.</para>
    /// </remarks>
    private static LicenceTerms NoLicence { get; } = new(
        TermMonths: 12_000,
        WorkCommitment: [],
        Bond: Money.Zero,
        Relinquishment: [],
        FiscalRegime: new ContentId("concession"),
        HseRegime: new ContentId("standard"));



    // THE FARM-DOWN KEEPS ITS REAL TERMS, and it is not an oversight.
    //
    // `WorkingInterest.Validate` refuses a sellable cap of zero — "a sellable
    // cap 0 must be in (0, 1]" — so this mechanic CANNOT be neutralised through
    // its terms, and a cap of 0.001 would be a gentler mechanic rather than an
    // absent one, which plans 24 §2's law forbids. Neutralising it properly
    // means not offering the COMMAND, and a style cannot yet choose a command
    // set (plans 25 §6).
    //
    // It costs nothing to leave on: a builder never sells a share, and with the
    // partner share at zero the takeover's own trigger — partner share at or
    // above the sellable cap — is false forever regardless of the clock below.
}

/// <summary>
/// <b>Oilfield Engineer</b> — the same engine at full fidelity.
/// </summary>
/// <remarks>
/// Every mechanic on, at the terms the engine ships. This style exists to be
/// boring: it is <c>Defaults</c> with a name, and the acceptance criterion for
/// the whole of plans 25 is that composing at it does not move a single test.
/// </remarks>
public sealed class EngineerStyle : IGameStyle
{
    public ContentId Id { get; } = new("engineer");

    public string Title => "Oilfield Engineer";

    public string Premise =>
        "An operating field, at full fidelity. Every refusal is one a real operator would meet.";

    public StyleTerms Terms { get; } = new(
        Licence: Defaults.LicenceTerms,
        Insurance: Defaults.Insurance,
        Hedge: Defaults.Hedge,
        WorkingInterest: Defaults.WorkingInterest,
        DemurrageRate: Defaults.DemurrageRateFraction,
        TakeoverAfterAmortisingTicks: Defaults.TakeoverAfterAmortisingTicks,
        Tenure: true,
        Banking: true,
        TakeOrPay: true);

    public ContentId RealityProfile { get; } = new("simulation");

    public ContentId StartingState => StartingStates.OpeningPosition;

    public ContentId Rules => RuleSets.Realistic.Id;

    public EngineSettings Compose(EngineSettings baseline) => Style.Write(this, baseline);
}

/// <summary>
/// Writing a style's four axes onto a settings baseline, in one place.
/// </summary>
/// <remarks>
/// Shared by every style so none can write three of the four and be plausible.
/// That defect has already happened once, in the Godot host, which spelled the
/// axes out separately on the new-game path and again on the load path.
/// </remarks>
internal static class Style
{
    public static EngineSettings Write(IGameStyle style, EngineSettings baseline)
    {
        ArgumentNullException.ThrowIfNull(style);
        ArgumentNullException.ThrowIfNull(baseline);

        return baseline with
        {
            RealityProfile = style.RealityProfile,
            StartingState = style.StartingState,
            Rules = style.Rules,
            Style = style.Id,
        };
    }
}

/// <summary>The styles this build ships, which are its products.</summary>
/// <remarks>
/// The set is closed and an unknown id is refused rather than guessed at — the
/// same rule <see cref="RuleSets.Named"/> and <c>Defaults.ProfileNamed</c>
/// already follow. Guessing would compose a game nobody asked for and call it
/// the one that was named.
/// </remarks>
public static class GameStyles
{
    public static IGameStyle Days { get; } = new DaysStyle();

    public static IGameStyle Engineer { get; } = new EngineerStyle();

    /// <summary>Every style, in declaration order.</summary>
    public static IReadOnlyList<IGameStyle> All { get; } = [Days, Engineer];

    /// <summary>The style with this id, or a refusal naming what is on offer.</summary>
    public static IGameStyle Named(ContentId id)
    {
        for (int i = 0; i < All.Count; i++)
            if (All[i].Id == id)
                return All[i];

        var known = new string[All.Count];

        for (int i = 0; i < All.Count; i++)
            known[i] = "'" + All[i].Id.Value + "'";

        throw new InvariantFault("plans 25 §2", null,
            $"'{id.Value}' is not a game style this build knows; it ships " +
            string.Join(" and ", known));
    }
}
