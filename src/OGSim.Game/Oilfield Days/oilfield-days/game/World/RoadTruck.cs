#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// A truck that runs the road while there is work out there.
///
/// <para>The Settlers touch: a field with an operation running should <em>look</em>
/// like one. When the engine reports an activity, a truck leaves the yard for the
/// site and shuttles; when the count falls to zero it parks. It carries nothing
/// and decides nothing — it is the visible form of
/// <c>FieldReadModel.ActivitiesRunning</c>, and if it disagreed with that number
/// it would be lying about the field.</para>
/// </summary>
public sealed partial class RoadTruck : Node2D
{
    private const float Speed = 190.0f;

    private Sprite2D _sprite = null!;
    private Texture2D _side = null!;
    private Texture2D _north = null!;
    private Texture2D _south = null!;
    private Vector2 _from;
    private Vector2 _to;
    private float _along;
    private bool _outbound = true;
    private bool _running;

    public override void _Ready()
    {
        _side = Load("res://assets/vehicles/service-truck/west.png");
        _north = Load("res://assets/vehicles/service-truck/north.png");
        _south = Load("res://assets/vehicles/service-truck/south.png");

        float scale = (1.7f * BasinWorld.TileSize) / _side.GetWidth();

        _sprite = new Sprite2D
        {
            Texture = _side,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Scale = new Vector2(scale, scale),
            Modulate = new Color(0.92f, 0.94f, 1.0f),
        };

        AddChild(_sprite);
        Visible = false;
    }

    /// <summary>Tell the truck where the road runs, and whether there is work.</summary>
    public void Drive(Vector2 yard, Vector2? site, bool working)
    {
        _running = working && site.HasValue;
        Visible = _running;

        if (!_running)
            return;

        _from = yard;
        _to = site!.Value;
    }

    public override void _Process(double delta)
    {
        if (!_running)
            return;

        float distance = _from.DistanceTo(_to);

        if (distance < 1.0f)
            return;

        _along += (float)delta * Speed / distance * (_outbound ? 1.0f : -1.0f);

        if (_along >= 1.0f)
        {
            _along = 1.0f;
            _outbound = false;
        }
        else if (_along <= 0.0f)
        {
            _along = 0.0f;
            _outbound = true;
        }

        // The road is laid horizontal-then-vertical, so the truck drives the
        // same two legs rather than cutting across country.
        var corner = new Vector2(_to.X, _from.Y);
        float legOne = _from.DistanceTo(corner);
        float total = legOne + corner.DistanceTo(_to);
        float travelled = _along * total;

        Vector2 previous = Position;

        Position = travelled <= legOne
            ? _from.Lerp(corner, legOne <= 0.0f ? 0.0f : travelled / legOne)
            : corner.Lerp(_to, (travelled - legOne) / Mathf.Max(1.0f, total - legOne));

        Face(Position - previous);
    }

    private void Face(Vector2 movement)
    {
        if (movement.LengthSquared() < 0.01f)
            return;

        if (Mathf.Abs(movement.X) >= Mathf.Abs(movement.Y))
        {
            _sprite.Texture = _side;
            _sprite.FlipH = movement.X > 0.0f;
        }
        else
        {
            _sprite.Texture = movement.Y < 0.0f ? _north : _south;
            _sprite.FlipH = false;
        }


    }

    private static Texture2D Load(string path)
    {
        var texture = GD.Load<Texture2D>(path);

        if (texture is null)
            throw new System.InvalidOperationException($"vehicle art missing: {path}");

        return texture;
    }
}
