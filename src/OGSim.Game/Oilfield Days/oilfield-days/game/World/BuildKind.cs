#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// One thing a construction crew can add to the plant.
///
/// <para>A <c>Resource</c> per plans 21 §P1, so the seventh addition is a
/// <c>.tres</c>, an icon and a sprite rather than a new <c>case</c>.</para>
///
/// <para><b>What it deliberately does not carry: a price, a duration or a
/// capacity.</b> All three are the engine's, and a designer able to edit them
/// here would be editing the simulation from the client. What it carries is what
/// the thing is called, what it looks like, and how to recognise it once the
/// engine has built it.</para>
/// </summary>
[GlobalClass]
public partial class BuildKind : Resource
{
    [Export] public string DisplayName { get; set; } = string.Empty;

    /// <summary>What it unblocks, in the player's language.</summary>
    [Export] public string Explains { get; set; } = string.Empty;

    /// <summary>The icon the catalogue lists it under.</summary>
    [Export] public Texture2D? Icon { get; set; }

    /// <summary>
    /// The display-id fragment the finished element will carry.
    /// </summary>
    /// <remarks>
    /// This is how the host knows the build landed: it counts elements matching
    /// this fragment when the work starts, and the scaffold becomes a unit when
    /// the count goes up. **The host never decides that a build has finished** —
    /// a build that completed on a host timer would drift from the engine the
    /// first time a fault abandoned a tick.
    /// </remarks>
    [Export] public string ChainMatch { get; set; } = string.Empty;

    /// <summary>Which of the engine's install commands this is.</summary>
    [Export] public BuildCommand Orders { get; set; } = BuildCommand.Separator;
}

/// <summary>The engine's install commands, named so a resource can pick one.</summary>
/// <remarks>
/// An enum rather than a type name in a string: the dispatcher switches a command
/// out of it exactly once, and a typo in a string would be a build that silently
/// never submitted anything.
/// </remarks>
public enum BuildCommand
{
    Separator,
    Manifold,
    GasPlant,
    Treater,
    Tank,
    Export,
}
