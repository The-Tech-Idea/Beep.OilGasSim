#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// The player, who is a truck.
///
/// <para>Plan 11 §1 is absolute about this: <b>no humans, no animals, no NPCs</b>.
/// The player is a machine, and every interaction is a vehicle action or a
/// terminal — so there is no character controller here, no footstep, and nothing
/// that would ever need a person to be drawn.</para>
///
/// <para>Three directions are drawn and the fourth is a mirror of the third,
/// which is the rule the vehicle art is cut to (<c>assets/_sample-hd-topdown/README.md</c>,
/// "Direction rule"): East is generated, West is a free horizontal flip, and
/// sprites carry no lettering precisely so that flip gives nothing away.</para>
/// </summary>
public sealed partial class ServiceTruck : CharacterBody2D
{
    private const float Speed = 300.0f;
    private const float Acceleration = 1800.0f;
    private const float Friction = 2400.0f;

    /// <summary>
    /// How long the truck is, nose to tail, in cells.
    /// </summary>
    /// <remarks>
    /// Length rather than height, and one factor for all three facings. The side
    /// view is long and low while the rear view is short and tall; scaling each
    /// picture to its own height would shrink and grow the truck every time it
    /// turned a corner.
    /// </remarks>
    private const float LengthInTiles = 1.9f;

    private Sprite2D _sprite = null!;
    private Texture2D _side = null!;
    private Texture2D _north = null!;
    private Texture2D _south = null!;
    private float _scale;

    /// <summary>Where the truck is looking. Used by prompts and by the sprite.</summary>
    public Vector2I Facing { get; private set; } = Vector2I.Down;

    /// <summary>Whether the truck answers the controls. False while a screen is open.</summary>
    public bool ControlsEnabled { get; set; } = true;

    public override void _Ready()
    {
        _side = Load("res://assets/vehicles/service-truck/west.png");
        _north = Load("res://assets/vehicles/service-truck/north.png");
        _south = Load("res://assets/vehicles/service-truck/south.png");

        // The masters are 1024 px art (assets/_sample-hd-topdown), so this is a
        // large reduction: mipmaps, or the tank's ladder and railings alias into
        // sparkling noise as the truck moves.
        _scale = (LengthInTiles * BasinWorld.TileSize) / _side.GetWidth();

        _sprite = new Sprite2D
        {
            Texture = _south,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Scale = new Vector2(_scale, _scale),
        };

        AddChild(_sprite);
        ApplyFacing();

        var shape = new CollisionShape2D
        {
            Shape = new CircleShape2D { Radius = BasinWorld.TileSize * 0.35f },
        };

        AddChild(shape);
    }

    public override void _PhysicsProcess(double delta)
    {
        Vector2 wanted = ControlsEnabled ? GameInput.DriveVector() : Vector2.Zero;

        Velocity = wanted == Vector2.Zero
            ? Velocity.MoveToward(Vector2.Zero, Friction * (float)delta)
            : Velocity.MoveToward(wanted * Speed, Acceleration * (float)delta);

        if (wanted != Vector2.Zero)
        {
            // The dominant axis decides the sprite: a truck at 45 degrees still
            // has to be drawn facing one of the four ways that exist.
            Vector2I facing = Mathf.Abs(wanted.X) >= Mathf.Abs(wanted.Y)
                ? new Vector2I(Mathf.Sign(wanted.X), 0)
                : new Vector2I(0, Mathf.Sign(wanted.Y));

            if (facing != Facing)
            {
                Facing = facing;
                ApplyFacing();
            }
        }

        MoveAndSlide();
        ClampToMap();
    }

    private void ApplyFacing()
    {
        if (Facing.Y < 0)
        {
            _sprite.Texture = _north;
            _sprite.FlipH = false;
        }
        else if (Facing.Y > 0)
        {
            _sprite.Texture = _south;
            _sprite.FlipH = false;
        }
        else
        {
            _sprite.Texture = _side;

            // The sheet draws the truck facing WEST; East is that picture
            // mirrored, which is free and — because the art carries no
            // lettering — indistinguishable from a drawn facing.
            _sprite.FlipH = Facing.X > 0;
        }

        // The art is drawn from above, so it sits centred on the ground rather
        // than standing on its own base line.
        _sprite.Offset = Vector2.Zero;
    }

    /// <summary>The basin's edge, set by the scene that built the world.</summary>
    public Vector2 Bounds { get; set; } = new(4096, 4096);

    private void ClampToMap()
    {
        float margin = BasinWorld.TileSize * 0.5f;

        GlobalPosition = new Vector2(
            Mathf.Clamp(GlobalPosition.X, margin, Bounds.X - margin),
            Mathf.Clamp(GlobalPosition.Y, margin, Bounds.Y - margin));
    }

    private static Texture2D Load(string path)
    {
        var texture = GD.Load<Texture2D>(path);

        if (texture is null)
            throw new System.InvalidOperationException($"vehicle art missing: {path}");

        return texture;
    }
}
