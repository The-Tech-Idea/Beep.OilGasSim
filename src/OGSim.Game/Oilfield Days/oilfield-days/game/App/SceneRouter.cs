#nullable enable

using Godot;

namespace OilfieldDays.App;

/// <summary>
/// The scene flow of plan 08 §2, in one place.
///
/// <para>Main menu → new game → gameplay → result, with the boards and the
/// pause menu opening <em>over</em> gameplay rather than replacing it. A screen
/// never loads another screen by path: it asks here, so the flow is readable in
/// one file instead of scattered across every button.</para>
///
/// <para>An autoload, so it outlives the scene it is changing — and so the
/// engine, which is also an autoload, is never rebuilt by a screen transition.</para>
/// </summary>
public sealed partial class SceneRouter : Node
{
    public static SceneRouter Instance { get; private set; } = null!;

    public const string Splash = "res://scenes/Splash.tscn";
    public const string MainMenu = "res://scenes/MainMenu.tscn";
    public const string Options = "res://scenes/Options.tscn";
    public const string LoadGame = "res://scenes/Load.tscn";
    public const string NewGame = "res://scenes/NewGame.tscn";
    public const string Gameplay = "res://scenes/Gameplay.tscn";
    public const string Result = "res://scenes/Result.tscn";

    public const string DispatchBoard = "res://scenes/Dispatch.tscn";
    public const string LeaseBoard = "res://scenes/Lease.tscn";
    public const string FleetBoard = "res://scenes/Fleet.tscn";
    public const string PauseMenu = "res://scenes/Pause.tscn";

    private CanvasLayer _overlays = null!;
    private Control? _open;

    /// <summary>Whether a board or the pause menu is covering the game.</summary>
    public bool OverlayOpen => _open is not null && IsInstanceValid(_open);

    public override void _EnterTree()
    {
        Instance = this;

        // The kit's widgets read their colours from the theme, so the theme has
        // to exist before the first screen draws. An autoload's _EnterTree is the
        // earliest point at which the window root exists to hang it on.
        KitTheme.Install(this);

        // A development knife, before anything draws: cut the supplied UI atlases
        // into pieces and quit. It writes art into the project, so it never runs
        // in a game.
        if (DevAtlasSlice.RunIfRequested())
        {
            GetTree().Quit();
            return;
        }

        // One layer above everything the gameplay scene draws, created once so a
        // board can survive the frame its opener was disposed in.
        _overlays = new CanvasLayer { Name = "Overlays", Layer = 40 };
        AddChild(_overlays);

        DevScreenshot.ArmIfRequested(this);
    }

    public override void _Ready()
    {
        // A development switch can open any top-level scene straight away, so a
        // screen can be looked at without clicking through the flow to reach it.
        // Deferred: the main scene is still being added when an autoload becomes
        // ready, and swapping it out mid-add is refused by the tree.
        if (DevOptions.Screen is not null)
            CallDeferred(MethodName.OpenDevScreen);
    }

    private void OpenDevScreen()
    {
        switch (DevOptions.Screen)
        {
            case "splash":
                Go(Splash);
                break;

            case "menu":
                Go(MainMenu);
                break;

            case "options":
                Go(Options);
                break;

            case "load":
                Go(LoadGame);
                break;

            case "newgame":
                Go(NewGame);
                break;

            // Everything else names a board, and a board opens over the running
            // game — so the game is what gets loaded, and it opens the board.
            default:
                Go(Gameplay);
                break;
        }
    }

    public void Go(string scenePath)
    {
        CloseOverlay();
        GetTree().ChangeSceneToFile(scenePath);
    }

    /// <summary>Open a board over the running game. Only one at a time.</summary>
    public void OpenOverlay(string scenePath)
    {
        CloseOverlay();

        var packed = GD.Load<PackedScene>(scenePath);

        if (packed is null)
        {
            GD.PushError($"[router] missing scene: {scenePath}");
            return;
        }

        _open = packed.Instantiate<Control>();
        _overlays.AddChild(_open);
    }

    public void CloseOverlay()
    {
        if (_open is not null && IsInstanceValid(_open))
            _open.QueueFree();

        _open = null;
    }

    /// <summary>Close the board if one is open, and say whether that happened.</summary>
    public bool CloseIfOpen()
    {
        if (!OverlayOpen)
            return false;

        CloseOverlay();
        return true;
    }
}
