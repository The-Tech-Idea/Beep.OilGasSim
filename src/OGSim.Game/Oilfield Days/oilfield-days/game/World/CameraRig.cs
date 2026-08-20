#nullable enable

using System;
using Godot;

namespace OilfieldDays.World;

/// <summary>
/// The view over the lease — Stage A of the gameplay redesign (plans 16).
///
/// <para>The camera used to be a child of the truck, which is what made the
/// player a truck: the only way to look at something was to drive to it. It is
/// its own rig now, and the truck is one of the things it can look at.</para>
///
/// <para><b>Zoom is stepped, not continuous.</b> The world is tile art at a fixed
/// pixel size, and a fractional zoom lands every tile on a fractional pixel, so
/// the whole ground resamples and shimmers as the view moves — the same fault the
/// layout audit's OFFGRID check exists to catch on one control, happening across
/// the entire screen. Whole ratios keep the tiles crisp.</para>
/// </summary>
public sealed partial class CameraRig : Camera2D
{
    /// <summary>The steps, from the whole lease down to a single pad.</summary>
    private static readonly float[] Steps = { 0.2f, 0.3f, 0.45f, 0.6f, 0.9f, 1.35f, 2.0f };

    /// <summary>Where a fresh run opens: close enough to read a pad.</summary>
    private const int OpeningStep = 3;

    /// <summary>Lease pixels a second at zoom 1, before the zoom scaling.</summary>
    private const float PanSpeed = 1400.0f;

    /// <summary>How near the window edge the pointer starts pushing the view.</summary>
    private const float EdgeBand = 12.0f;

    private Vector2 _extent;
    private int _step = OpeningStep;
    private Node2D? _following;

    /// <summary>Whether the view answers the keyboard and the mouse.</summary>
    public bool ControlsEnabled { get; set; } = true;

    /// <summary>Frame the basin and stop the view leaving it.</summary>
    public void Frame(Vector2 extent, Vector2 openAt)
    {
        _extent = extent;

        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = (int)extent.X;
        LimitBottom = (int)extent.Y;

        // Smoothing is for the follow, and it fights a drag: a view that eases
        // towards the pointer never quite arrives and reads as lag.
        PositionSmoothingEnabled = false;

        GlobalPosition = openAt;
        Apply();
    }

    /// <summary>Centre on something and keep it centred until the view is moved.</summary>
    public void Follow(Node2D what)
    {
        ArgumentNullException.ThrowIfNull(what);

        _following = what;
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 6.0f;
    }

    /// <summary>Stop following. Any deliberate pan does this.</summary>
    public void Release()
    {
        _following = null;
        PositionSmoothingEnabled = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!ControlsEnabled)
            return;

        if (@event is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)
                Step(+1, wheel.Position);
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
                Step(-1, wheel.Position);
        }

        // Middle-drag pans by the same distance the pointer moved, which means
        // dividing by the zoom: at 0.2 the world is five times smaller under the
        // cursor and a raw delta would send the view flying.
        if (@event is InputEventMouseMotion drag
            && (drag.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            Release();
            GlobalPosition -= drag.Relative / Zoom;
            Clamp();
        }
    }

    public override void _Process(double delta)
    {
        if (_following is not null && IsInstanceValid(_following))
        {
            GlobalPosition = _following.GlobalPosition;
            Clamp();

            return;
        }

        if (!ControlsEnabled)
            return;

        Vector2 push = GameInput.DriveVector() + EdgePush();

        if (push == Vector2.Zero)
            return;

        Release();

        // Divided by the zoom so a pan crosses the same amount of WORLD at every
        // step. Without it, panning zoomed out crawls and panning zoomed in
        // races, and both feel like the controls are broken rather than the view.
        GlobalPosition += push.Normalized() * (PanSpeed / Zoom.X) * (float)delta;
        Clamp();
    }

    /// <summary>Which way the pointer is pushing from the window edge.</summary>
    private Vector2 EdgePush()
    {
        Vector2 at = GetViewport().GetMousePosition();
        Vector2 window = GetViewport().GetVisibleRect().Size;
        var push = Vector2.Zero;

        // Outside the window there is no push: an alt-tabbed pointer parked off
        // the left edge would otherwise scroll the view for as long as it sat
        // there.
        if (at.X < 0.0f || at.Y < 0.0f || at.X > window.X || at.Y > window.Y)
            return push;

        if (at.X < EdgeBand) push.X -= 1.0f;
        if (at.X > window.X - EdgeBand) push.X += 1.0f;
        if (at.Y < EdgeBand) push.Y -= 1.0f;
        if (at.Y > window.Y - EdgeBand) push.Y += 1.0f;

        return push;
    }

    /// <summary>Zoom one step, keeping what is under the pointer under it.</summary>
    private void Step(int by, Vector2 pointer)
    {
        int wanted = Mathf.Clamp(_step + by, 0, Steps.Length - 1);

        if (wanted == _step)
            return;

        Vector2 before = GetCanvasTransform().AffineInverse() * pointer;
        _step = wanted;
        Apply();

        // Zooming about the pointer rather than the centre: without this, the
        // thing a player is looking at slides away from them every time they
        // scroll, and they chase it.
        Release();
        GlobalPosition += before - (GetCanvasTransform().AffineInverse() * pointer);
        Clamp();
    }

    private void Apply() => Zoom = new Vector2(Steps[_step], Steps[_step]);

    /// <summary>
    /// Keep the view inside the basin.
    /// </summary>
    /// <remarks>
    /// Camera2D's own limits only bite while it is moving itself; a position set
    /// directly walks straight past them. This is the half-viewport inset that
    /// keeps the edge of the world at the edge of the screen — and it gives up
    /// when the basin is smaller than the window, because there is nothing to
    /// clamp to and fighting over it would jitter.
    /// </remarks>
    private void Clamp()
    {
        Vector2 half = GetViewport().GetVisibleRect().Size * 0.5f / Zoom;

        float x = _extent.X <= half.X * 2.0f
            ? _extent.X * 0.5f
            : Mathf.Clamp(GlobalPosition.X, half.X, _extent.X - half.X);

        float y = _extent.Y <= half.Y * 2.0f
            ? _extent.Y * 0.5f
            : Mathf.Clamp(GlobalPosition.Y, half.Y, _extent.Y - half.Y);

        GlobalPosition = new Vector2(x, y);
    }
}
