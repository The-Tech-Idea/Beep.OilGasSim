#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>What a unit is sent out to do.</summary>
/// <remarks>
/// One entry per job the yard can commission. It is an enum rather than a string
/// because the dispatcher switches a command out of it exactly once, at the
/// arrival transition, and a typo in a string would be a job that silently never
/// submitted anything.
/// </remarks>
public enum JobKind
{
    None,

    /// <summary>Shoot 3-D over a structure already found — sharpen it.</summary>
    Survey,

    /// <summary>Shoot 2-D over a block of the licence — find structures at all.</summary>
    SurveyBlock,

    /// <summary>Put up the early production facility a field is brought on with.</summary>
    Commission,

    Drill,
    WellTest,
    WirelineLog,
    CutCore,
    Repair,
    Service,
    FitMonitoring,
    Build,
}

/// <summary>
/// What one kind of unit is: its art, how fast it travels, and the job it
/// carries out to the field.
///
/// <para>A <c>Resource</c> per plans 21 §P1, and the reason there is no
/// <c>WirelineTruck</c> class: a wireline truck is a <see cref="VehicleUnit"/>
/// holding the wireline kind. The moment a subclass exists per kind, adding a
/// kind means writing a class and the data-driven half is dead (§P2).</para>
///
/// <para><b>Nothing here changes a simulation outcome.</b> A unit's speed decides
/// when the command is submitted and nothing else — plans 15 §2b — so this
/// carries look and pacing, never a cost, a duration or a probability. Those are
/// the engine's, and a designer editing a <c>.tres</c> must not be able to reach
/// them.</para>
/// </summary>
[GlobalClass]
public partial class UnitKind : Resource
{
    [Export] public string DisplayName { get; set; } = string.Empty;

    /// <summary>The job this unit carries. One kind, one job.</summary>
    [Export] public JobKind Carries { get; set; } = JobKind.None;

    [Export] public Texture2D? Art { get; set; }

    /// <summary>The strip played while it is under way, if it has one.</summary>
    [Export] public Texture2D? Working { get; set; }

    [Export] public int WorkingFrames { get; set; } = 1;

    /// <summary>How tall it is drawn, in pixels.</summary>
    [Export] public float DrawHeight { get; set; } = 90.0f;

    /// <summary>
    /// Lease pixels a second at normal speed.
    /// </summary>
    /// <remarks>
    /// A rig convoy is slow and a survey crew is quick, which is true and which
    /// is also the only thing separating them in play — since the engine prices
    /// and times the work itself, the difference a player feels between units is
    /// how long they take to get there.
    /// </remarks>
    [Export] public float Speed { get; set; } = 900.0f;

    /// <summary>Where it stands in the yard when it has nothing to do, in tiles from the plant site.</summary>
    [Export] public Vector2 YardStand { get; set; } = Vector2.Zero;
}
