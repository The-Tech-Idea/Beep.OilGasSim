#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// What one kind of plant structure is: its art, the ground it stands on, and
/// the clearance it needs around it.
///
/// <para>A <c>Resource</c> per plans 21 §P1, and it replaces two <c>switch</c>
/// statements on a display id — one choosing art, one choosing a draw height —
/// which is exactly the shape §P2 exists to prevent. Adding the next structure
/// is a <c>.tres</c> and a sprite.</para>
///
/// <para><b>Footprint and clearance are why this exists at all.</b> The plant was
/// laid out on one fixed spacing whatever was being placed, so a storage tank and
/// a metering station got the same slot and everything ended up touching. A
/// refinery is mostly the gaps: access for a crane, room to drop a vessel, a fire
/// break. Structures that share a wall read as a shelf of icons rather than a
/// site.</para>
///
/// <para><b>None of this reaches the simulation.</b> The engine's chain is an
/// ordered list with no geometry; where a separator stands and how much room it
/// is given are the host's, and nothing here changes a throughput or a
/// capacity.</para>
/// </summary>
[GlobalClass]
public partial class StructureKind : Resource
{
    /// <summary>
    /// The fragment of a chain element's display id this kind answers to.
    /// </summary>
    /// <remarks>
    /// Matched by longest fragment first, so <c>water-disposal</c> beats
    /// <c>water</c> and <c>gathering-1</c> is caught by <c>gathering</c>. The
    /// engine numbers its elements, and a kind that had to name each one would be
    /// back to a switch.
    /// </remarks>
    [Export] public string Match { get; set; } = string.Empty;

    [Export] public Texture2D? Art { get; set; }

    /// <summary>How tall the sprite is drawn, in pixels.</summary>
    [Export] public float DrawHeight { get; set; } = 100.0f;

    /// <summary>
    /// The animation strip played while the structure is running, if it has one.
    /// </summary>
    /// <remarks>
    /// Horizontal, square frames, frame zero matching the still. Half the
    /// supplied structures have one and half do not, and that is fine: a
    /// separator sitting still looks like a separator, while a flare that never
    /// lights looks broken.
    /// </remarks>
    [Export] public Texture2D? Working { get; set; }

    /// <summary>How many frames the strip holds. Written down, not guessed at.</summary>
    [Export] public int WorkingFrames { get; set; } = 1;

    /// <summary>The ground it stands on, in tiles.</summary>
    [Export] public Vector2I Footprint { get; set; } = new(2, 2);

    /// <summary>
    /// Tiles of empty ground kept around it on every side.
    /// </summary>
    /// <remarks>
    /// Larger for the things that need it in life: a flare wants a burn radius,
    /// a tank wants a bund and a fire break. It is a look rather than a rule the
    /// engine enforces, and it is honest as a look — the engine models no
    /// spacing at all.
    /// </remarks>
    [Export] public int Clearance { get; set; } = 1;

    /// <summary>The whole plot: what it stands on plus what it keeps clear.</summary>
    public Vector2I Plot => new(
        Footprint.X + (Clearance * 2),
        Footprint.Y + (Clearance * 2));
}
