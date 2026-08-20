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
        AddChild(SlateChrome.Backdrop(0.72f));

        SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);

        // Tall enough for what is in it. The save entry and its outcome line were
        // added without growing the panel, so the column ran thirty pixels past
        // the frame — which the audit found and the eye did not.
        KitPanel panel = ScreenChrome.Panel("PAUSED", new Vector2(420, 500), LayoutPreset.Center, new Vector2(-210, -250));
        AddChild(panel);

        var column = new VBoxContainer
        {
            Position = new Vector2(24, 56),
            CustomMinimumSize = new Vector2(372, 0),
        };

        column.AddThemeConstantOverride("separation", 12);
        panel.AddChild(column);

        Add(column, "Resume", KitTheme.Green, () => SceneRouter.Instance.CloseOverlay());

        Label saved = SlateChrome.Text(string.Empty, 14, KitTheme.Muted);
        saved.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        saved.CustomMinimumSize = new Vector2(372, 0);

        Add(column, "Save the run", KitTheme.Green, () =>
        {
            // The outcome is reported either way. A save button that looked the
            // same whether it worked is how a player finds out at the worst
            // possible moment that it did not (L4).
            saved.Text = EngineHost.Instance.Save(out string problem)
                ? "Saved. It will open from Continue on the main menu."
                : problem;

            saved.AddThemeColorOverride(
                "font_color", problem.Length == 0 ? KitTheme.Green : KitTheme.Red);
        });

        column.AddChild(saved);
        Add(column, "The dispatch board", KitTheme.Muted, () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard));
        Add(column, "The yard", KitTheme.Muted, () => SceneRouter.Instance.OpenOverlay(SceneRouter.FleetBoard));
        Add(column, "Give up and score the run", KitTheme.Red, () => SceneRouter.Instance.Go(SceneRouter.Result));
        Add(column, "Main menu", KitTheme.Muted, () => SceneRouter.Instance.Go(SceneRouter.MainMenu));
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
        Button button = SlateChrome.Action(text, accent, new Vector2(372, 48));
        button.Pressed += () => action();
        parent.AddChild(button);
    }
}
