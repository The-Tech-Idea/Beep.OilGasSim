#nullable enable

using Godot;
using OilfieldDays.App;

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

        var ground = new ColorRect { Color = KitTheme.Void };
        ground.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ground);

        var art = new TextureRect
        {
            Texture = GD.Load<Texture2D>(SlateChrome.SplashPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };

        art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(art);

        Label credit = SlateChrome.Line("Powered by OGSim", 18, KitTheme.Ink);
        credit.SetAnchorsPreset(LayoutPreset.CenterBottom);
        credit.Position = new Vector2(-90, -70);
        credit.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
        credit.AddThemeConstantOverride("shadow_outline_size", 8);
        AddChild(credit);

        Label skip = SlateChrome.Line("press any key", 14, KitTheme.Muted);
        skip.SetAnchorsPreset(LayoutPreset.CenterBottom);
        skip.Position = new Vector2(-45, -40);
        skip.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.9f));
        skip.AddThemeConstantOverride("shadow_outline_size", 8);
        AddChild(skip);

        _veil = new ColorRect { Color = new Color(0.0f, 0.0f, 0.0f, 0.0f), MouseFilter = MouseFilterEnum.Ignore };
        _veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_veil);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true })
            Leave();
    }

    public override void _Process(double delta)
    {
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
}
