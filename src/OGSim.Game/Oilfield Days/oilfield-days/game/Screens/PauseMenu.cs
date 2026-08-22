#nullable enable

using Beep.ECS.UI;
using Beep.ECS.UI.Kit;
using Godot;
using OilfieldDays.App;
using OilfieldDays.Host;
using System;

namespace OilfieldDays.Screens;

/// <summary>
/// Pause (plan 08 §6.6).
///
/// <para>It pauses the <em>host</em>. The engine has no notion of being paused —
/// it advances when it is told to and not otherwise — so all this does is stop
/// the controller asking for months (plan 08 §6.6: "do not advance the engine
/// while paused").</para>
/// </summary>
[Tool]
public sealed partial class PauseMenu : Control
{
	private Label _saved = null!;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		if (!Engine.IsEditorHint())
			SimulationController.Instance.SetSpeed(SimulationController.Speed.Paused);

		StyleBackdrop();

		PanelContainer panel = RequireNode<PanelContainer>("PausePanel");
		panel.AddThemeStyleboxOverride("panel", SlateChrome.PanelPlate(0));

		VBoxContainer column = RequireNamed<VBoxContainer>(panel, "Actions");
		column.AddThemeConstantOverride("separation", 12);

		Wire(column, "ResumeButton", "Resume", KitTheme.Green, () => SceneRouter.Instance.CloseOverlay());
		Wire(column, "SaveButton", "Save the run", KitTheme.Green, Save);

		_saved = RequireNamed<Label>(column, "SaveStatus");
		_saved.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_saved.CustomMinimumSize = new Vector2(372, 0);
		_saved.AddThemeFontSizeOverride("font_size", 14);
		_saved.AddThemeColorOverride("font_color", KitTheme.Muted);

		Wire(column, "DispatchButton", "The dispatch board", KitTheme.Muted, () => SceneRouter.Instance.OpenOverlay(SceneRouter.DispatchBoard));
		Wire(column, "FleetButton", "The yard", KitTheme.Muted, () => SceneRouter.Instance.OpenOverlay(SceneRouter.FleetBoard));
		Wire(column, "ResultButton", "Give up and score the run", KitTheme.Red, () => SceneRouter.Instance.Go(SceneRouter.Result));
		Wire(column, "MenuButton", "Main menu", KitTheme.Muted, () => SceneRouter.Instance.Go(SceneRouter.MainMenu));
	}

	private void StyleBackdrop()
	{
		ColorRect shade = RequireNode<ColorRect>("Backdrop");
		shade.Color = new Color(0.02f, 0.04f, 0.06f, 0.78f);
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed(World.GameInput.Cancel))
		{
			SceneRouter.Instance.CloseOverlay();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Save()
	{
		// The outcome is reported either way. A save button that looked the
		// same whether it worked is how a player finds out at the worst
		// possible moment that it did not (L4).
		_saved.Text = EngineHost.Instance.Save(out string problem)
			? "Saved. It will open from Continue on the main menu."
			: problem;

		_saved.AddThemeColorOverride(
			"font_color", problem.Length == 0 ? KitTheme.Green : KitTheme.Red);
	}

	private static void Wire(Container parent, string name, string text, Color accent, System.Action action)
	{
		Button button = RequireNamed<Button>(parent, name);
		SlateChrome.ApplyChunk(button, text.ToUpperInvariant(), RoleOf(accent), new Vector2(372, 48));
		button.Pressed += () => action();
	}

	private static UiSurface.Role RoleOf(Color accent) =>
		accent == KitTheme.Green ? UiSurface.Role.Success
		: accent == KitTheme.Red ? UiSurface.Role.Danger
		: accent == KitTheme.Amber ? UiSurface.Role.Warning
		: accent == KitTheme.Sky ? UiSurface.Role.Info
		: UiSurface.Role.Neutral;

	private static T? FindNamed<T>(Node at, string name) where T : Node
	{
		foreach (Node child in at.GetChildren())
		{
			if (child.Name == name && child is T typed)
				return typed;

			T? found = FindNamed<T>(child, name);

			if (found is not null)
				return found;
		}

		return null;
	}

	private T RequireNode<T>(NodePath path) where T : Node =>
		GetNodeOrNull<T>(path) ?? throw new InvalidOperationException(
			$"{nameof(PauseMenu)} requires a design-time {typeof(T).Name} at '{path}'.");

	private static T RequireNamed<T>(Node at, string name) where T : Node =>
		FindNamed<T>(at, name) ?? throw new InvalidOperationException(
			$"{nameof(PauseMenu)} requires a design-time {typeof(T).Name} named '{name}' under {at.GetPath()}.");
}
