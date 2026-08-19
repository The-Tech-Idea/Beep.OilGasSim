#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// The game's input actions, declared in code.
///
/// <para>They are registered here rather than in <c>project.godot</c> so the
/// binding and the code that reads it sit in one place and cannot drift apart —
/// an action renamed in the editor and not in a script fails silently at runtime,
/// which is the one failure mode input maps reliably produce. Registering is
/// idempotent, so re-entering the scene costs nothing.</para>
/// </summary>
public static class GameInput
{
    public const string DriveLeft = "od_drive_left";
    public const string DriveRight = "od_drive_right";
    public const string DriveUp = "od_drive_up";
    public const string DriveDown = "od_drive_down";
    public const string Interact = "od_interact";
    public const string AdvanceMonth = "od_advance_month";
    public const string TogglePause = "od_toggle_pause";
    public const string Cancel = "od_cancel";
    public const string OpenDispatch = "od_open_dispatch";
    public const string OpenLease = "od_open_lease";
    public const string OpenFleet = "od_open_fleet";

    public static void Configure()
    {
        Bind(DriveLeft, Key.A, Key.Left);
        Bind(DriveRight, Key.D, Key.Right);
        Bind(DriveUp, Key.W, Key.Up);
        Bind(DriveDown, Key.S, Key.Down);
        Bind(Interact, Key.E);
        Bind(AdvanceMonth, Key.Space, Key.Enter);
        Bind(TogglePause, Key.P);
        Bind(Cancel, Key.Escape);
        Bind(OpenDispatch, Key.J);
        Bind(OpenLease, Key.L);
        Bind(OpenFleet, Key.G);

        // The action bar's hotkeys: one per offered command, in the order it
        // lists them, so a decision is one key rather than a mouse trip.
        Bind("od_action_1", Key.Key1);
        Bind("od_action_2", Key.Key2);
        Bind("od_action_3", Key.Key3);
        Bind("od_action_4", Key.Key4);
        Bind("od_action_5", Key.Key5);
        Bind("od_action_6", Key.Key6);
    }

    /// <summary>The drive vector, -1..1 on each axis.</summary>
    public static Vector2 DriveVector() =>
        Input.GetVector(DriveLeft, DriveRight, DriveUp, DriveDown);

    private static void Bind(string action, params Key[] keys)
    {
        if (InputMap.HasAction(action))
            InputMap.EraseAction(action);

        InputMap.AddAction(action);

        foreach (Key key in keys)
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
