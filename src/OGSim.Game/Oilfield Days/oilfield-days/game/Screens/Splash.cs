#nullable enable

using Godot;
using OilfieldDays.App;
using System;

namespace OilfieldDays.Screens;

/// <summary>
/// The splash — the key art, held for a moment, then the menu.
///
/// <para>GAME-SDD-002 §7B.1 asks for a `Boot` scene and this is it, doing the
/// one job a boot scene honestly has: showing the game's face while the autoloads
/// come up. It starts nothing and loads no engine — <c>EngineHost</c> builds only
/// when a run is created — so the wait is a beat, not a progress bar it would
/// have to invent a percentage for (§12, G-12).</para>
///
/// <para>Any key or click skips it. A splash that cannot be skipped is a splash
/// the player resents by the third run.</para>
/// </summary>
[Tool]
public sealed partial class Splash : Control
{
	/// <summary>How long the art holds before the menu takes over.</summary>
	private const double Hold = 2.4;

	/// <summary>How long the fade out of it runs.</summary>
	private const double Fade = 0.5;

	private ColorRect _veil = null!;
	private double _elapsed;
	private bool _leaving;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var ground = RequireNode<ColorRect>("Ground");
		ground.Color = KitTheme.Void;
		ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		var art = RequireNode<TextureRect>("Art");
		art.Texture ??= GD.Load<Texture2D>(SlateChrome.SplashPath);
		art.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		art.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		art.MouseFilter = MouseFilterEnum.Ignore;
		art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		Label credit = RequireNode<Label>("Credit");
		credit.Text = "Powered by OGSim";
		AnchorBottomCenter(credit, -90, -70, 90, -44);
		credit.AddThemeFontSizeOverride("font_size", 18);
		credit.AddThemeColorOverride("font_color", KitTheme.Ink);
		credit.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
		credit.AddThemeConstantOverride("shadow_outline_size", 8);

		Label skip = RequireNode<Label>("Skip");
		skip.Text = "press any key";
		AnchorBottomCenter(skip, -45, -40, 45, -18);
		skip.AddThemeFontSizeOverride("font_size", 14);
		skip.AddThemeColorOverride("font_color", KitTheme.Muted);
		skip.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
		skip.AddThemeConstantOverride("shadow_outline_size", 8);

		_veil = RequireNode<ColorRect>("Veil");
		_veil.Color = new Color(0.0f, 0.0f, 0.0f, 0.0f);
		_veil.MouseFilter = MouseFilterEnum.Ignore;
		_veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
			Leave();
	}

	public override void _Process(double delta)
	{
		if (Godot.Engine.IsEditorHint())
			return;

		_elapsed += delta;

		if (!_leaving && _elapsed >= Hold)
			Leave();

		if (!_leaving)
			return;

		// Fade to black over the last stretch, then hand over. The menu draws the
		// same art behind its panels, so the cut is a dissolve rather than a jump.
		double into = _elapsed - Hold;
		_veil.Color = new Color(0.0f, 0.0f, 0.0f, (float)Mathf.Clamp(into / Fade, 0.0, 1.0));

		if (into >= Fade)
		{
			SetProcess(false);
			SceneRouter.Instance.Go(SceneRouter.MainMenu);
		}
	}

	private void Leave()
	{
		if (_leaving)
			return;

		_leaving = true;
		_elapsed = Hold;
	}

	private static void AnchorBottomCenter(Control control, float left, float top, float right, float bottom)
	{
		control.AnchorLeft = 0.5f;
		control.AnchorTop = 1.0f;
		control.AnchorRight = 0.5f;
		control.AnchorBottom = 1.0f;
		control.OffsetLeft = left;
		control.OffsetTop = top;
		control.OffsetRight = right;
		control.OffsetBottom = bottom;
		control.GrowHorizontal = GrowDirection.Both;
		control.GrowVertical = GrowDirection.Begin;
	}

	private T RequireNode<T>(NodePath path) where T : Node =>
		GetNodeOrNull<T>(path) ?? throw new InvalidOperationException(
			$"{nameof(Splash)} requires a design-time {typeof(T).Name} at '{path}'.");
}
