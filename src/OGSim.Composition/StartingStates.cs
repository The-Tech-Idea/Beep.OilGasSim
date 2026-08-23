// What a company opens with (plans 22 §4, S2).

using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// The starting states this build knows, named so a host can choose one.
/// </summary>
/// <remarks>
/// <para><see cref="EngineSettings.StartingState"/> is public and required, so
/// the values it accepts have to be public too — otherwise every host outside
/// this assembly would be typing the string and finding out at run time whether
/// it had spelt it correctly.</para>
///
/// <para>The set is closed on purpose. Composition refuses a state it does not
/// know rather than guessing, because guessing would hand a misspelled scenario
/// a free plant and call it a starting position.</para>
/// </remarks>
public static class StartingStates
{
    /// <summary>
    /// A company that opens holding a commissioned plant — every run before S2,
    /// and what a test about production rather than construction wants.
    /// </summary>
    public static ContentId OpeningPosition { get; } = new("opening-position");

    /// <summary>
    /// A company that opens with a yard, a licence and a bank balance, and has
    /// to build its own processing train.
    /// </summary>
    public static ContentId BareGround { get; } = new("bare-ground");
}
