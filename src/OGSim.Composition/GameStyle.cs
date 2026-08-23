// Plans 25 — a game engine per style, over one OGSim.
//
// OGSIM REMAINS FULL-FEATURED. Nothing here removes a module, a mechanic, a
// command or a stage from the engine: a style is a SELECTION over it, expressed
// as which modules it composes and which terms it hands them. Delete every style
// and the engine is unchanged — that is the test this file has to keep passing.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// The terms a style chooses, wherever the engine offers a choice.
/// </summary>
/// <remarks>
/// <para><b>Why a record rather than four more module parameters.</b> These were
/// briefly threaded through the engine as a <c>MechanicSet</c> the modules asked
/// questions of — three modules grew a parameter, one constructor reached thirty
/// arguments, and a switch shipped with no consumer at all. The modules never
/// wanted to know which game they were in. They want terms, which is what they
/// already took for ladders, contracts, lift tiers and climate.</para>
///
/// <para><b>Two positions, ON and NEUTRAL — there is no third</b> (plans 24 §2).
/// A style may hand a mechanic its real terms or terms under which it does
/// nothing. It may not hand it gentler ones: that is a difficulty slider wearing
/// a mode's clothes, and where two live behaviours are genuinely wanted the
/// answer is a <see cref="RuleSet"/> or a <c>RealityProfile</c>.</para>
/// </remarks>
public sealed record StyleTerms(
    LicenceTerms Licence,

    /// <summary>
    /// Cover, or none at all.
    /// </summary>
    /// <remarks>
    /// <b>Null means the stage is not contributed</b>, not that it is
    /// contributed with harmless numbers. The engine refuses terms describing a
    /// policy that does nothing, and it is right to: within the engine such
    /// content is meaningless. So the honest neutral is absence, and a module
    /// that is handed null does not claim the stage slot either — a claimed slot
    /// must be filled (<c>ModuleComposer.AssertEveryPromiseKept</c>).
    /// </remarks>
    OGSim.Company.InsuranceTerms? Insurance,

    /// <summary>A price collar, or none at all — see <see cref="Insurance"/>.</summary>
    /// <remarks>
    /// <c>Hedge.Validate</c> refuses a hedged fraction of zero ("must be in
    /// (0, 1]"), so a collar over nothing cannot be expressed as terms.
    /// </remarks>
    OGSim.Company.HedgeTerms? Hedge,

    /// <summary>
    /// What may be farmed down.
    /// </summary>
    /// <remarks>
    /// Never null and never zero: <c>WorkingInterest.Validate</c> refuses a
    /// sellable cap of zero, and a tiny cap would be a GENTLER mechanic rather
    /// than an absent one. A style that does not want the farm-down leaves the
    /// real terms in place — a builder simply never sells — and stops the
    /// takeover with its own clock instead.
    /// </remarks>
    OGSim.Company.WorkingInterestTerms WorkingInterest,

    /// <summary>What a late cargo pays per day over laytime; zero is neutral.</summary>
    double DemurrageRate,

    /// <summary>
    /// How long a company may sit in <c>Amortising</c> before its partners take
    /// it over.
    /// </summary>
    /// <remarks>
    /// It needs to be a threshold rather than a flag: the trigger also asks
    /// whether the partner share has reached the sellable maximum, so a maximum
    /// of zero reads <c>0 &gt;= 0</c> and would ARM the takeover rather than
    /// disable it. A threshold no run reaches is the honest neutral.
    /// </remarks>
    int TakeoverAfterAmortisingTicks,

    /// <summary>Whether tenure governs work at all.</summary>
    /// <remarks>
    /// The licence OBJECT is composed in every build — the read model promises
    /// one licence, always (SDD-011 §1), and the client screens read it. What
    /// this switches is the MECHANIC: the per-tick commitment assessment
    /// (<c>LicenceStage</c>) and the tenure refusal in <c>ActivityOrders</c>.
    /// False means those are absent — not vacuously true against terms no run
    /// can breach.
    /// </remarks>
    bool Tenure,

    /// <summary>Whether the treasury offers borrowing at all.</summary>
    /// <remarks>
    /// Presence rather than terms, because this mechanic has no expressible
    /// neutral: the facility is priced off reserves, so there are no terms
    /// under which a drawdown does nothing. What a style declines is the
    /// COMMANDS — <c>Borrow</c> and <c>Repay</c> are simply not offered
    /// (plans 25 §6's command-set choice, arriving as plans 27's
    /// <c>Banking</c> entry). The lender itself stays composed in every build.
    /// </remarks>
    bool Banking,

    /// <summary>Whether the company opens under an offtake commitment.</summary>
    /// <remarks>
    /// Presence, like <see cref="Banking"/>: the engine refuses a committed
    /// volume of zero, so there are no terms under which the contract does
    /// nothing. Measured before this switch existed: a Days company owed
    /// deliveries from tick 0 while its first sale was structurally two to
    /// three years away — behind a survey, a discovery, a plant AND an export
    /// line — and the $6M-per-window shortfall penalties helped kill every
    /// run on every seed.
    /// </remarks>
    bool TakeOrPay);

/// <summary>
/// One product: which physics, what the company opens holding, what it may do,
/// and which modules compose with which terms.
/// </summary>
/// <remarks>
/// <para>Design 03 §3.2's law, one level up from plan 23's rules: a style is a
/// different set of registered models, never a branch. Nothing inside the engine
/// asks which style it is in — the style has already answered by the time a
/// module is constructed.</para>
///
/// <para>Design 18 §5b's three axes are unchanged and stay where they are:
/// FIDELITY is a <c>RealityProfile</c> (a style picks one, it does not redefine
/// one), ASSISTS are the Advisor and live outside the engine entirely, and
/// FORGIVENESS is content. A style is the bundle — which is what §5b.5 calls a
/// preset.</para>
/// </remarks>
public interface IGameStyle
{
    /// <summary>The style's content id, as a save records it.</summary>
    ContentId Id { get; }

    /// <summary>What the product is called.</summary>
    string Title { get; }

    /// <summary>The one sentence a player is choosing between.</summary>
    string Premise { get; }

    /// <summary>The terms this style hands the modules that take a choice.</summary>
    StyleTerms Terms { get; }

    /// <summary>Which physics fills each replaceable slot.</summary>
    ContentId RealityProfile { get; }

    /// <summary>What the company opens holding.</summary>
    ContentId StartingState { get; }

    /// <summary>Which rule set decides what may be done.</summary>
    ContentId Rules { get; }

    /// <summary>
    /// Write this style's composition-time choices onto a settings baseline.
    /// </summary>
    /// <remarks>
    /// The one place the axes are written together, which is what stops a caller
    /// setting two of them and forgetting the third — the defect this replaced,
    /// where the Godot host spelled three ids out on the new-game path and again
    /// on the load path.
    /// </remarks>
    EngineSettings Compose(EngineSettings baseline);
}
