#nullable enable

using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OilfieldDays.App;
using OilfieldDays.World;

namespace OilfieldDays.Ui;

/// <summary>
/// The minimap the main-scene and lease mockups both put top-right.
///
/// <para>The ground comes from the same <see cref="TerrainMap"/> the world is
/// tiled from, so the map and the field cannot disagree; the markers come from
/// the read model. Drawn rather than rendered off a camera, because a 24 km
/// basin at one pixel per tile is cheaper to draw than to photograph.</para>
/// </summary>
public sealed partial class Minimap : Control
{
    private TerrainMap? _terrain;
    private FieldReadModel? _snapshot;
    private Vector2 _truck;
    private int _tiles = 1;

    public void Bind(TerrainMap terrain, FieldReadModel snapshot, Vector2 truck, int tiles)
    {
        _terrain = terrain;
        _snapshot = snapshot;
        _truck = truck;
        _tiles = Mathf.Max(1, tiles);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = Size;

        DrawRect(new Rect2(Vector2.Zero, size), ScreenChrome.WoodDark);

        if (_terrain is null)
            return;

        // One rectangle per sampled tile: the basin is bigger than this control,
        // so it is walked in steps rather than pixel by pixel.
        int step = Mathf.Max(1, _tiles / 96);
        float scale = size.X / _tiles;

        for (int y = 0; y < _tiles; y += step)
        {
            for (int x = 0; x < _tiles; x += step)
            {
                Color colour = _terrain.At(new Vector2I(x, y)) switch
                {
                    Ground.Water => new Color(0.30f, 0.55f, 0.75f),
                    Ground.Sand => new Color(0.72f, 0.60f, 0.42f),
                    Ground.Rock => new Color(0.42f, 0.42f, 0.44f),
                    _ => _terrain.IsDry(new Vector2I(x, y))
                        ? new Color(0.44f, 0.50f, 0.28f)
                        : new Color(0.40f, 0.62f, 0.30f),
                };

                DrawRect(new Rect2(x * scale, y * scale, step * scale + 1.0f, step * scale + 1.0f), colour);
            }
        }

        if (_snapshot is null)
            return;

        float perMetre = size.X / (_tiles / (float)BasinWorld.TilesPerKilometre * (float)BasinWorld.MetresPerCell);

        for (int i = 0; i < _snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = _snapshot.Prospects[i];
            var at = new Vector2((float)prospect.At.X * perMetre, (float)prospect.At.Y * perMetre);

            DrawCircle(at, 4.0f, prospect.ProbabilityOfSuccess switch
            {
                < 0.20 => ScreenChrome.Bad,
                < 0.35 => ScreenChrome.Gold,
                _ => ScreenChrome.Good,
            });
        }

        // The truck last, so it is never hidden under a marker.
        Vector2 truck = _truck / (_tiles * BasinWorld.TileSize) * size.X;
        DrawCircle(truck, 5.0f, ScreenChrome.Cream);
        DrawArc(truck, 8.0f, 0.0f, Mathf.Tau, 20, ScreenChrome.WoodDark, 2.0f);
    }
}
