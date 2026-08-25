#nullable enable

using Godot;
using OilfieldDays.World;
using System;

namespace OilfieldDays.Screens;

/// <summary>
/// The seeded ground, painted - the preview panel of the setup mockups.
///
/// <para>Not a diagram of the world: <b>the world</b>. It builds a real
/// <see cref="BasinWorld"/> from the seed and knobs on screen, into an offscreen
/// viewport with the camera pulled back to hold the whole basin, so the preview
/// is drawn by the same tilesets, autotiling and scatter the game will use.</para>
///
/// <para>It is surface only because there is nothing else to draw. No engine
/// exists at setup, so <see cref="BasinWorld.PaintBareGround"/> runs with no
/// prospects and no wells - which is what §7A.4 asks for, arrived at by having no
/// subsurface rather than by hiding one.</para>
/// </summary>
[Tool]
public sealed partial class WorldPreview : Control
{
    private SubViewport _viewport = null!;
    private TextureRect _screen = null!;
    private Camera2D _camera = null!;
    private BasinWorld? _basin;

    private ulong _seed;
    private int _cells;
    private double _land;
    private double _climate;
    private bool _pending;

    /// <summary>The ground that was built, for the screen to measure.</summary>
    public TerrainMap? Terrain => _basin?.Terrain;

    public override void _Ready()
    {
        ClipContents = true;

        _viewport = RequireNode<SubViewport>("Viewport");
        _viewport.Size = new Vector2I(960, 720);
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        _viewport.TransparentBg = false;

        var sea = RequireNode<CanvasLayer>(_viewport, "Sea");
        sea.Layer = -100;

        var fill = RequireNode<ColorRect>(sea, "Fill");
        fill.Color = new Color(0.13f, 0.30f, 0.45f);
        fill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _camera = RequireNode<Camera2D>(_viewport, "Camera");
        _camera.Enabled = true;

        _screen = RequireNode<TextureRect>("Screen");
        _screen.Texture = _viewport.GetTexture();
        _screen.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _screen.StretchMode = TextureRect.StretchModeEnum.Scale;
        _screen.TextureFilter = TextureFilterEnum.Nearest;
        _screen.MouseFilter = MouseFilterEnum.Ignore;
        _screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        Resized += Fit;
        Fit();

        if (_pending)
            Repaint();
    }

    /// <summary>Match the offscreen viewport to the panel it is shown in.</summary>
    private void Fit()
    {
        var wanted = new Vector2I(
            Mathf.Max(64, (int)Size.X),
            Mathf.Max(64, (int)Size.Y));

        if (_viewport.Size == wanted)
            return;

        _viewport.Size = wanted;
        Frame();
    }

    /// <summary>
    /// Put the whole basin in shot. The smaller of the two fits, so a square
    /// basin in a wide panel keeps its coasts rather than having them cropped.
    /// </summary>
    private void Frame()
    {
        if (_basin is null)
            return;

        _camera.Position = _basin.Extent * 0.5f;

        float fit = Mathf.Min(_viewport.Size.X / _basin.Extent.X, _viewport.Size.Y / _basin.Extent.Y);
        _camera.Zoom = Vector2.One * (fit * 0.98f);
    }

    public void Bind(ulong seed, int cells, double landFraction, double climateSeverity)
    {
        _seed = seed;
        _cells = cells;
        _land = landFraction;
        _climate = climateSeverity;

        if (!IsNodeReady())
        {
            _pending = true;
            return;
        }

        Repaint();
    }

    private void Repaint()
    {
        _pending = false;

        if (_basin is not null)
        {
            _viewport.RemoveChild(_basin);
            _basin.QueueFree();
        }

        _basin = new BasinWorld
        {
            TerrainPixelsPerTile = 16,
        };
        _viewport.AddChild(_basin);
        _basin.Build(_cells, _seed, _land, _climate);
        _basin.PaintBareGround();

        Frame();
    }

    private T RequireNode<T>(NodePath path) where T : Node =>
        GetNodeOrNull<T>(path) ?? throw new InvalidOperationException(
            $"{nameof(WorldPreview)} requires a design-time {typeof(T).Name} at '{path}'.");

    private static T RequireNode<T>(Node at, NodePath path) where T : Node =>
        at.GetNodeOrNull<T>(path) ?? throw new InvalidOperationException(
            $"{nameof(WorldPreview)} requires a design-time {typeof(T).Name} at '{at.GetPath()}/{path}'.");
}
