#nullable enable

using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;

namespace OilfieldDays.Screens;

/// <summary>
/// Pause (plan 08 §6.6).
///
/// <para>It pauses the <em>host</em>. The engine has no notion of being paused —
/// it advances when it is told to and not otherwise — so all this does is stop
/// the controller asking for months (plan 08 §6.6: "do not advance the engine
/// while paused").</para>
/// </summary>
public sealed partial class PauseMenu : Control
{
    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ScreenChrome.Backdrop(0.72f));

        SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);

        KitPanel panel = ScreenChrome.Panel("PAUSED", new Vector2(420, 380), LayoutPreset.Center, new Vector2(-210, -190));
        AddChild(panel);

        var column = new VBoxContainer
        {
            Position = new Vector2(24, 56),
            CustomMinimumSize = new Vector2(372, 0),
        };

        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        Add(column, "Resume", ScreenChrome.Good, () => SceneRouter.Instance.CloseOverlay());
        Add(column, "The dispatch board", ScreenChrome.Wood, () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard));
        Add(column, "The yard", ScreenChrome.Wood, () => SceneRouter.Instance.OpenOverlay(SceneRouter.FleetBoard));
        Add(column, "Give up and score the run", ScreenChrome.Bad, () => SceneRouter.Instance.Go(SceneRouter.Result));
        Add(column, "Main menu", ScreenChrome.Faded, () => SceneRouter.Instance.Go(SceneRouter.MainMenu));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(World.GameInput.Cancel))
        {
            SceneRouter.Instance.CloseOverlay();
            GetViewport().SetInputAsHandled();
        }
    }

    private static void Add(Container parent, string text, Color accent, System.Action action)
    {
        Button button = ScreenChrome.Action(text, accent, new Vector2(372, 48));
        button.Pressed += () => action();
        parent.AddChild(button);
    }
}
