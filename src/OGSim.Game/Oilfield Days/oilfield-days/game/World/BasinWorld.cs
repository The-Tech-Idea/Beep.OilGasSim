#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OilfieldDays.World;

/// <summary>
/// The basin the engine generated, built as a place you can drive through.
///
/// <para><b>Settlers and Stardew, not a map with pins.</b> A structure is a
/// cleared square with a stake on it; a well is a gravel pad with plant standing
/// on it; the surface facilities are a yard; and each is joined to the others by
/// a dirt road that vehicles actually use. The world is dense enough to be
/// somewhere rather than sparse enough to be a chart.</para>
///
/// <para><b>Nothing here decides where anything is.</b> Prospects carry a
/// <see cref="Coordinate"/> from world generation (plan 00 §5), and this class
/// converts kilometres to tiles, clears ground around them, and lays road
/// between them. A hand-placed structure would be a second world disagreeing
/// with the real one.</para>
///
/// <para>The ground itself is presentation: <c>WorldView</c> — terrain,
/// settlements, transport — is declared in the contract surface but the concrete
/// <c>Engine</c> does not publish it yet (plan 09 §2's "plan now, expose later"),
/// so grass, pads and roads are drawn by the host until it does.</para>
/// </summary>
public sealed partial class BasinWorld : Node2D
{
    /// <summary>World generation's grid is one kilometre per cell (`WorldGenerator.CellSizeMetres`).</summary>
    public const double MetresPerCell = 1000.0;

    /// <summary>One tile of ground. The tilesets are cut at this size.</summary>
    public const int TileSize = 64;

    /// <summary>
    /// How many tiles an engine kilometre is worth.
    /// </summary>
    /// <remarks>
    /// The one number that decides whether this is a place or a map. At one tile
    /// per kilometre a well pad is a dot and the basin is a chart; at six, a pad
    /// is a yard you drive onto and the road between two wells is a journey.
    /// </remarks>
    public const int TilesPerKilometre = 6;

    private const int WellPadTiles = 5;
    private const int ProspectPadTiles = 3;
    private const int PlantPadWide = 16;
    private const int PlantPadTall = 9;
    private const int RoadWidth = 2;

    private readonly Dictionary<ulong, Node2D> _prospectMarkers = new();
    private readonly Dictionary<ulong, Node2D> _wellMarkers = new();
    private readonly Dictionary<ulong, Coordinate> _wellSites = new();
    private readonly List<Coordinate> _holesBeingDrilled = new();
    private readonly List<Coordinate> _dryHoles = new();

    private WorldMap _ground = null!;
    private TerrainMap _terrain = null!;
    private EdgeMaskTerrain _water = null!;
    private EdgeMaskTerrain _sand = null!;
    private EdgeMaskTerrain _grass = null!;
    private EdgeMaskTerrain _dryGrass = null!;
    private EdgeMaskTerrain _rock = null!;
    private EdgeMaskTerrain _gravel = null!;
    private EdgeMaskTerrain _road = null!;
    private Node2D _sceneryLayer = null!;
    private Node2D _markerLayer = null!;
    private Node2D _plantLayer = null!;
    private Node2D _yardLayer = null!;
    private RoadTruck _traffic = null!;

    private int _tiles;
    private int _wellsLastSeen;
    private int _activitiesLastSeen;
    private bool _groundDirty = true;

    /// <summary>The ground the world is tiled from, for the minimap to draw.</summary>
    public TerrainMap Terrain => _terrain;

    /// <summary>How many tiles across the basin is.</summary>
    public int Tiles => _tiles;

    /// <summary>The basin's extent in pixels.</summary>
    public Vector2 Extent => new(_tiles * TileSize, _tiles * TileSize);

    /// <summary>Where the surface facilities stand. The engine's chain has no
    /// coordinates, so the host picks one place to build it and keeps it.</summary>
    public Vector2 PlantSite { get; private set; }

    public void Build(int basinKilometres, ulong seed)
    {
        _tiles = basinKilometres * TilesPerKilometre;
        _ground = new WorldMap(_tiles, _tiles);

        // The ground is generated from the same seed the engine generated the
        // basin from, so a run's field and its landscape are one world.
        _terrain = new TerrainMap(_tiles, seed);

        // The yard sits in the middle of the basin's south, where one road can
        // reach every structure without crossing the whole field.
        PlantSite = TileCentre(PlantTile());

        // The project's own 17-piece sheets (assets/tilesets), keyed off their
        // checkerboard into flat17/. One layer per material, stacked low to
        // high, so every transition is drawn by the sheets' own edge pieces.
        _water = AddTerrain("Water", "res://assets/tilesets/flat17/water.png", -70);
        _sand = AddTerrain("Shore", "res://assets/tilesets/flat17/dirt.png", -60);
        _grass = AddTerrain("Grass", "res://assets/tilesets/flat17/grass.png", -50);
        _dryGrass = AddTerrain("DryGrass", "res://assets/tilesets/flat17/olive-grass.png", -45);
        _rock = AddTerrain("Rock", "res://assets/tilesets/flat17/stone.png", -40);
        _gravel = AddTerrain("Gravel", "res://assets/tilesets/flat17/cracked-dirt.png", -20);
        _road = AddTerrain("Roads", "res://assets/tilesets/flat17/dirt.png", -10);

        _sceneryLayer = new Node2D { Name = "Scenery", YSortEnabled = true };
        _plantLayer = new Node2D { Name = "Plant", YSortEnabled = true };
        _yardLayer = new Node2D { Name = "Yard", YSortEnabled = true };
        _markerLayer = new Node2D { Name = "Markers", YSortEnabled = true };
        AddChild(_sceneryLayer);
        AddChild(_yardLayer);
        AddChild(_plantLayer);
        AddChild(_markerLayer);

        _traffic = new RoadTruck { Name = "Traffic" };
        AddChild(_traffic);

        BuildYard();
    }

    /// <summary>
    /// The maintenance yard of the main-scene mockup: a control room, a
    /// workshop, a warehouse, a store and a gate.
    /// </summary>
    /// <remarks>
    /// Host scenery, and deliberately so — no command placed them and no
    /// snapshot reports them, because a workshop is somewhere the company works
    /// from rather than plant the field owns. It is what turns the plant pad
    /// from a row of vessels into a place.
    /// </remarks>
    private void BuildYard()
    {
        (string art, string name, float dx, float dy, float tall)[] yard =
        {
            ("control-room-cabin", "Control Room", -7.5f, -3.4f, 2.4f),
            ("maintenance-workshop", "Maintenance Workshop", -4.4f, -3.6f, 2.6f),
            ("equipment-warehouse", "Equipment Warehouse", 4.8f, -3.6f, 2.6f),
            ("worker-accommodation-cabin", "Crew Quarters", 7.6f, -3.2f, 2.2f),
            ("fuel-tank", "Fuel", -7.4f, 2.8f, 1.6f),
            ("frac-tank", "Water", 7.6f, 2.8f, 1.6f),
            ("site-lighting-pole", string.Empty, 0.0f, -3.8f, 2.2f),
            ("security-checkpoint", "Gate", -1.8f, 4.4f, 1.8f),
        };

        foreach ((string art, string name, float dx, float dy, float tall) in yard)
        {
            var texture = GD.Load<Texture2D>($"res://assets/props/{art}.png");

            if (texture is null)
                continue;

            Node2D prop = MakeProp(
                texture,
                PlantSite + new Vector2(dx * TileSize, dy * TileSize),
                tall);

            // The mockup labels its buildings on little signs; without them a
            // yard is a row of sheds and a player has to guess which is which.
            if (name.Length > 0)
                prop.AddChild(Caption(name));

            _yardLayer.AddChild(prop);
        }
    }

    /// <summary>Redraw everything the snapshot decides.</summary>
    public void Bind(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ResolveDrilling(snapshot);
        SyncProspects(snapshot);
        SyncWells(snapshot);
        SyncChain(snapshot);

        if (_groundDirty)
        {
            PaintGround(snapshot);
            _groundDirty = false;
        }

        _traffic.Drive(PlantSite, NewestWellSite(), snapshot.ActivitiesRunning > 0);
    }

    /// <summary>Engine metres to world pixels.</summary>
    public static Vector2 ToWorld(Coordinate at) => new(
        (float)(at.X / MetresPerCell * TilesPerKilometre * TileSize),
        (float)(at.Y / MetresPerCell * TilesPerKilometre * TileSize));

    /// <summary>The prospect nearest a point, within reach, or null.</summary>
    public ProspectView? ProspectNear(Vector2 point, float reach, FieldReadModel snapshot)
    {
        ProspectView? nearest = null;
        float best = reach;

        for (int i = 0; i < snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = snapshot.Prospects[i];
            float distance = ToWorld(prospect.At).DistanceTo(point);

            if (distance > best)
                continue;

            nearest = prospect;
            best = distance;
        }

        return nearest;
    }

    /// <summary>The well nearest a point, within reach, or null.</summary>
    public WellStatusView? WellNear(Vector2 point, float reach, FieldReadModel snapshot)
    {
        WellStatusView? nearest = null;
        float best = reach;

        for (int i = 0; i < snapshot.Wellbores.Count; i++)
        {
            WellStatusView well = snapshot.Wellbores[i];

            if (!_wellSites.TryGetValue(well.Well.Value, out Coordinate site))
                continue;

            float distance = ToWorld(site).DistanceTo(point);

            if (distance > best)
                continue;

            nearest = well;
            best = distance;
        }

        return nearest;
    }

    /// <summary>Remember that a hole has been ordered here, so whatever it finds
    /// — a well or nothing — is drawn where it actually went.</summary>
    public void RecordDrill(ProspectView prospect) => _holesBeingDrilled.Add(prospect.At);

    // ------------------------------------------------------------- the ground

    /// <summary>
    /// Clear the pads and lay the roads.
    /// </summary>
    /// <remarks>
    /// Repainted only when the set of sites changes, which is rare — a hole takes
    /// months. The network is a spine from the yard to everything worth
    /// visiting, which is how a field actually grows: access is built to what
    /// has been found.
    /// </remarks>
    private void PaintGround(FieldReadModel snapshot)
    {
        _ground.Fill(new Rect2I(0, 0, _tiles, _tiles), TerrainKind.Grass);

        Vector2I plant = PlantTile();

        // Everything that gets built on is levelled first: a pad is cleared
        // ground, and the noise does not get a vote on where the rig goes.
        _terrain.Level(Around(plant, PlantPadWide + 2, PlantPadTall + 2));

        for (int i = 0; i < snapshot.Prospects.Count; i++)
            _terrain.Level(Around(ToTile(snapshot.Prospects[i].At), ProspectPadTiles + 2, ProspectPadTiles + 2));

        foreach (KeyValuePair<ulong, Coordinate> pair in _wellSites)
            _terrain.Level(Around(ToTile(pair.Value), WellPadTiles + 2, WellPadTiles + 2));

        // Roads first, pads second: a road that ran through the yard would cut
        // it in half, and a pad is the thing that has been cleared — the track
        // stops at its gate.
        foreach (KeyValuePair<ulong, Coordinate> pair in _wellSites)
            _ground.Road(ToTile(pair.Value), plant, RoadWidth);

        for (int i = 0; i < snapshot.Prospects.Count; i++)
            _ground.Road(ToTile(snapshot.Prospects[i].At), plant, RoadWidth);

        _ground.Fill(Around(plant, PlantPadWide, PlantPadTall), TerrainKind.GravelPad);

        for (int i = 0; i < snapshot.Prospects.Count; i++)
            _ground.Fill(Around(ToTile(snapshot.Prospects[i].At), ProspectPadTiles, ProspectPadTiles), TerrainKind.GravelPad);

        for (int i = 0; i < _dryHoles.Count; i++)
            _ground.Fill(Around(ToTile(_dryHoles[i]), ProspectPadTiles, ProspectPadTiles), TerrainKind.GravelPad);

        foreach (KeyValuePair<ulong, Coordinate> pair in _wellSites)
            _ground.Fill(Around(ToTile(pair.Value), WellPadTiles, WellPadTiles), TerrainKind.GravelPad);

        // Low to high. Each layer paints where its material reaches OR ANYTHING
        // ABOVE IT DOES, so a grass shore sits on sand and sand sits in water
        // instead of every band being an island.
        _water.Repaint(_ground, _ => true, treatOutsideAsMaterial: true);
        _sand.Repaint(_ground, cell => _terrain.At(cell) >= Ground.Sand, treatOutsideAsMaterial: true);
        _grass.Repaint(_ground, cell => _terrain.At(cell) >= Ground.Grass && !_terrain.IsDry(cell));
        _dryGrass.Repaint(_ground, cell => _terrain.At(cell) >= Ground.Grass && _terrain.IsDry(cell));
        _rock.Repaint(_ground, cell => _terrain.At(cell) == Ground.Rock);
        _gravel.Repaint(_ground, cell => _ground.At(cell) == TerrainKind.GravelPad);
        _road.Repaint(_ground, cell => _ground.At(cell) == TerrainKind.DirtRoad);

        PaintScenery();
    }

    /// <summary>
    /// Scatter trees, scrub and boulders over open ground.
    /// </summary>
    /// <remarks>
    /// Placed by a hash of the tile, so a basin is dressed the same way every
    /// time it is drawn and the world does not shimmer when the ground is
    /// repainted. Nothing lands on a pad or a road: this is worked ground, and a
    /// tree growing through a wellhead would say otherwise.
    /// </remarks>
    private void PaintScenery()
    {
        foreach (Node child in _sceneryLayer.GetChildren())
            child.QueueFree();

        for (int y = 0; y < _tiles; y += 2)
        {
            for (int x = 0; x < _tiles; x += 2)
            {
                var cell = new Vector2I(x, y);

                if (_ground.At(cell) != TerrainKind.Grass || _terrain.At(cell) != Ground.Grass)
                    continue;

                uint noise = Hash((uint)x, (uint)y);

                if (noise % 100 >= 26)
                    continue;

                (string art, float tall) = (noise % 3) switch
                {
                    0 => ("res://assets/scenery/tree.png", 2.4f),
                    1 => ("res://assets/scenery/scrub.png", 0.9f),
                    _ => ("res://assets/scenery/boulder.png", 1.0f),
                };

                var texture = GD.Load<Texture2D>(art);

                if (texture is null)
                    continue;

                float jitterX = ((noise >> 8) % 48) - 24.0f;
                float jitterY = ((noise >> 16) % 48) - 24.0f;

                _sceneryLayer.AddChild(MakeProp(texture, TileCentre(cell) + new Vector2(jitterX, jitterY), tall));
            }
        }
    }

    private static uint Hash(uint x, uint y)
    {
        uint h = (x * 374761393u) + (y * 668265263u);
        h = (h ^ (h >> 13)) * 1274126177u;

        return h ^ (h >> 16);
    }

    private Vector2I PlantTile() => new(_tiles / 2, (int)(_tiles * 0.82f));

    private static Vector2I ToTile(Coordinate at) => new(
        Mathf.FloorToInt((float)(at.X / MetresPerCell * TilesPerKilometre)),
        Mathf.FloorToInt((float)(at.Y / MetresPerCell * TilesPerKilometre)));

    private static Vector2 TileCentre(Vector2I tile) =>
        new((tile.X + 0.5f) * TileSize, (tile.Y + 0.5f) * TileSize);

    private Rect2I Around(Vector2I centre, int wide, int tall) => new(
        Mathf.Clamp(centre.X - (wide / 2), 0, Mathf.Max(0, _tiles - wide)),
        Mathf.Clamp(centre.Y - (tall / 2), 0, Mathf.Max(0, _tiles - tall)),
        wide,
        tall);

    // ------------------------------------------------------------- the things

    private void ResolveDrilling(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Wellbores.Count; i++)
        {
            ulong id = snapshot.Wellbores[i].Well.Value;

            if (_wellSites.ContainsKey(id) || _holesBeingDrilled.Count == 0)
                continue;

            _wellSites[id] = Spread(_holesBeingDrilled[0]);
            _holesBeingDrilled.RemoveAt(0);
            _groundDirty = true;
        }

        bool activityFinished = _activitiesLastSeen > 0 && snapshot.ActivitiesRunning == 0;

        if (activityFinished && snapshot.Wells == _wellsLastSeen && _holesBeingDrilled.Count > 0)
        {
            // Stepped aside like a well: a structure drilled twice has a dry
            // hole and a producer on it, and stacking their markers would read
            // as one confused site.
            Coordinate site = Spread(_holesBeingDrilled[0]);
            _dryHoles.Add(site);
            MarkDryHole(site);
            _holesBeingDrilled.RemoveAt(0);
            _groundDirty = true;
        }

        _wellsLastSeen = snapshot.Wells;
        _activitiesLastSeen = snapshot.ActivitiesRunning;
    }

    /// <summary>
    /// Step a site sideways if something already stands there — a discovery is
    /// drilled again, which is development rather than a mistake.
    /// </summary>
    private Coordinate Spread(Coordinate site)
    {
        var candidate = site;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            bool taken = false;

            foreach (KeyValuePair<ulong, Coordinate> pair in _wellSites)
            {
                if (ToWorld(pair.Value).DistanceTo(ToWorld(candidate)) < TileSize * WellPadTiles)
                {
                    taken = true;
                    break;
                }
            }

            for (int i = 0; !taken && i < _dryHoles.Count; i++)
            {
                if (ToWorld(_dryHoles[i]).DistanceTo(ToWorld(candidate)) < TileSize * WellPadTiles)
                    taken = true;
            }

            if (!taken)
                return candidate;

            candidate = new Coordinate(site.X + (MetresPerCell * 0.9 * (attempt + 1)), site.Y);
        }

        return candidate;
    }

    private void MarkDryHole(Coordinate site)
    {
        var marker = new Node2D { Position = ToWorld(site) };

        var cross = new ColorRect
        {
            Color = new Color(0.32f, 0.28f, 0.26f, 0.95f),
            Size = new Vector2(30, 30),
            Position = new Vector2(-15, -15),
            RotationDegrees = 45.0f,
        };

        marker.AddChild(cross);
        marker.AddChild(Caption("dry hole"));
        _markerLayer.AddChild(marker);
    }

    private void SyncProspects(FieldReadModel snapshot)
    {
        var present = new HashSet<ulong>();

        for (int i = 0; i < snapshot.Prospects.Count; i++)
        {
            ProspectView prospect = snapshot.Prospects[i];
            ulong id = prospect.Prospect.Value;
            present.Add(id);

            if (_prospectMarkers.TryGetValue(id, out Node2D? existing))
            {
                existing.GetNode<Label>("Caption").Text = ProspectCaption(prospect);
                ((ProspectMarker)existing).Probability = prospect.ProbabilityOfSuccess;
                continue;
            }

            var marker = new ProspectMarker
            {
                Position = ToWorld(prospect.At),
                Probability = prospect.ProbabilityOfSuccess,
            };

            marker.AddChild(Caption(ProspectCaption(prospect)));
            _markerLayer.AddChild(marker);
            _prospectMarkers[id] = marker;
            _groundDirty = true;
        }

        var gone = new List<ulong>();

        foreach (KeyValuePair<ulong, Node2D> pair in _prospectMarkers)
        {
            if (!present.Contains(pair.Key))
                gone.Add(pair.Key);
        }

        for (int i = 0; i < gone.Count; i++)
        {
            _prospectMarkers[gone[i]].QueueFree();
            _prospectMarkers.Remove(gone[i]);
        }
    }

    private void SyncWells(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Wellbores.Count; i++)
        {
            WellStatusView well = snapshot.Wellbores[i];
            ulong id = well.Well.Value;

            if (!_wellSites.TryGetValue(id, out Coordinate site))
                continue;

            string caption = $"{well.DisplayId}\n{Readable(well.Status)}" +
                (well.ProducedThisTick.CubicMetres > 0.0
                    ? $"\n{well.ProducedThisTick.CubicMetres:N0} m³"
                    : string.Empty);

            if (_wellMarkers.TryGetValue(id, out Node2D? existing))
            {
                existing.GetNode<Label>("Caption").Text = caption;

                var sprite = existing.GetNode<Sprite2D>("Art");
                var texture = GD.Load<Texture2D>(ArtFor(well.Status));

                if (texture is not null && sprite.Texture != texture)
                {
                    sprite.Texture = texture;
                    FitSprite(sprite, WorldHeight(well.Status));
                }

                sprite.Modulate = well.Status == WellStatus.Abandoned
                    ? new Color(0.65f, 0.65f, 0.65f)
                    : Colors.White;

                continue;
            }

            Node2D marker = MakeMarker(ArtFor(well.Status), ToWorld(site), WorldHeight(well.Status), caption);
            _markerLayer.AddChild(marker);
            _wellMarkers[id] = marker;
        }
    }

    /// <summary>
    /// Build the surface chain the engine composed, as a yard.
    /// </summary>
    /// <remarks>
    /// Plan 05 §4 maps each element to a sprite and asks for the deferred amount
    /// shown on the element that refused flow. The order is the engine's; nothing
    /// here decides what the chain contains.
    /// </remarks>
    private void SyncChain(FieldReadModel snapshot)
    {
        foreach (Node child in _plantLayer.GetChildren())
            child.QueueFree();

        int perRow = Mathf.Max(1, Mathf.CeilToInt(snapshot.Chain.Count / 2.0f));
        float spacing = TileSize * 2.2f;
        float rowHeight = TileSize * 3.2f;
        float left = PlantSite.X - (spacing * (perRow - 1) * 0.5f);
        float top = PlantSite.Y - (rowHeight * 0.5f);

        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            ChainElementView element = snapshot.Chain[i];
            double held = 0.0;

            for (int d = 0; d < element.Deferred.Count; d++)
                held += element.Deferred[d].Deferred.Kilograms;

            string caption = held > 0.0
                ? $"{element.DisplayId}\n{element.Throughput.Kilograms / 1000.0:N0} t · {held / 1000.0:N0} t held"
                : $"{element.DisplayId}\n{element.Throughput.Kilograms / 1000.0:N0} t";

            int row = i / perRow;
            int column = i % perRow;

            Node2D node = MakeMarker(
                ArtForElement(element.DisplayId),
                new Vector2(left + (column * spacing), top + (row * rowHeight)),
                ElementHeight(element.DisplayId),
                caption);

            if (held > 0.0)
                node.GetNode<Label>("Caption").AddThemeColorOverride("font_color", new Color(1.0f, 0.45f, 0.35f));

            _plantLayer.AddChild(node);
        }
    }

    private Vector2? NewestWellSite()
    {
        Vector2? newest = null;

        foreach (KeyValuePair<ulong, Coordinate> pair in _wellSites)
            newest = ToWorld(pair.Value);

        return newest;
    }

    private EdgeMaskTerrain AddTerrain(string name, string texturePath, int zIndex)
    {
        var texture = GD.Load<Texture2D>(texturePath);

        if (texture is null)
            throw new InvalidOperationException($"terrain atlas missing: {texturePath}");

        var layer = new EdgeMaskTerrain { Name = name, ZIndex = zIndex };
        AddChild(layer);
        layer.UseAtlas(texture, TileSize);

        return layer;
    }

    private static string ProspectCaption(ProspectView prospect) =>
        $"{prospect.Play}\nPOS {prospect.ProbabilityOfSuccess * 100.0:F0}%";

    /// <summary>
    /// What a well looks like, by what the engine says it is doing.
    /// </summary>
    /// <remarks>
    /// Every path names a sprite from <c>assets/sprites/256</c> — the project's
    /// own library, one style across seventy-six pieces of plant. A rig stands
    /// over a hole while it is being drilled and is gone when it is finished,
    /// which is how a field reads at a glance.
    /// </remarks>
    private static string ArtFor(WellStatus status) => status switch
    {
        WellStatus.Producing => "res://assets/props/pumpjack.png",
        WellStatus.Injecting => "res://assets/props/water-injection-pump.png",
        WellStatus.Drilling or WellStatus.Completing => "res://assets/props/workover-rig.png",
        WellStatus.Workover => "res://assets/props/workover-rig.png",
        WellStatus.Abandoned => "res://assets/props/wellhead-tree.png",
        _ => "res://assets/props/wellhead-tree.png",
    };

    private static float WorldHeight(WellStatus status) => status switch
    {
        WellStatus.Producing => 2.6f,
        WellStatus.Injecting => 1.8f,
        WellStatus.Drilling or WellStatus.Completing => 3.4f,
        WellStatus.Workover => 3.0f,
        _ => 2.0f,
    };

    private static string Readable(WellStatus status) => status switch
    {
        WellStatus.Producing => "producing",
        WellStatus.ShutIn => "shut in",
        WellStatus.DryHole => "dry hole",
        WellStatus.Drilling => "drilling",
        WellStatus.Abandoned => "abandoned",
        WellStatus.Completing => "completing",
        WellStatus.Workover => "workover",
        WellStatus.Injecting => "injecting",
        WellStatus.Logged => "logged",
        WellStatus.Permitted => "permitted",
        WellStatus.Proposed => "proposed",
        WellStatus.SuspendedNonCommercial => "suspended",
        _ => status.ToString().ToLowerInvariant(),
    };

    /// <summary>
    /// The sprite for a chain element, by the name the engine gave it.
    /// </summary>
    /// <remarks>
    /// Plan 05 §4's mapping, against the library: a custody meter is a metering
    /// station, a flowline is a run of pipe, a gathering line is a choke
    /// manifold. A name with no picture falls back to the manifold rather than
    /// vanishing — an element the player cannot see is an element they cannot
    /// be told is throttling them.
    /// </remarks>
    private static string ArtForElement(string displayId)
    {
        if (displayId.StartsWith("gathering", System.StringComparison.Ordinal))
            return "res://assets/props/choke-manifold.png";

        return displayId switch
        {
            "separator" => "res://assets/props/three-phase-separator.png",
            "tank" => "res://assets/props/oil-storage-tank.png",
            "flare" => "res://assets/props/flare-stack.png",
            "water-disposal" => "res://assets/props/water-injection-pump.png",
            "custody-meter" => "res://assets/props/metering-station.png",
            "flowline" => "res://assets/props/pipe-rack-section.png",
            "manifold" => "res://assets/props/pipeline-manifold.png",
            _ when displayId.StartsWith("well", System.StringComparison.Ordinal) => "res://assets/props/wellhead-tree.png",
            _ => "res://assets/props/pipeline-manifold.png",
        };
    }

    private static float ElementHeight(string displayId) => displayId switch
    {
        "flare" => 3.2f,
        "tank" => 2.8f,
        "separator" => 2.2f,
        "custody-meter" => 1.8f,
        "water-disposal" => 1.8f,
        "flowline" => 1.4f,
        "manifold" => 1.6f,
        _ => 1.6f,
    };

    private static Node2D MakeMarker(string texturePath, Vector2 position, float tilesTall, string caption)
    {
        var texture = GD.Load<Texture2D>(texturePath);

        if (texture is null)
            throw new InvalidOperationException($"prop art missing: {texturePath}");

        Node2D holder = MakeProp(texture, position, tilesTall);
        holder.AddChild(Caption(caption));

        return holder;
    }

    private static Node2D MakeProp(Texture2D texture, Vector2 position, float tilesTall)
    {
        var sprite = new Sprite2D
        {
            Name = "Art",
            Texture = texture,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Centered = false,
        };

        var holder = new Node2D { Position = position };
        holder.AddChild(sprite);
        FitSprite(sprite, tilesTall);

        return holder;
    }

    private static Label Caption(string text)
    {
        var label = new Label
        {
            Name = "Caption",
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            Position = new Vector2(-90, 10),
            CustomMinimumSize = new Vector2(180, 0),
            ZIndex = 5,
        };

        label.AddThemeFontSizeOverride("font_size", 16);
        label.AddThemeColorOverride("font_color", new Color(1, 1, 1));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", 6);

        return label;
    }

    private static void FitSprite(Sprite2D sprite, float tilesTall)
    {
        if (sprite.Texture is null)
            return;

        float scale = (tilesTall * TileSize) / sprite.Texture.GetHeight();
        sprite.Scale = new Vector2(scale, scale);
        sprite.Position = new Vector2(
            -sprite.Texture.GetWidth() * scale / 2.0f,
            -sprite.Texture.GetHeight() * scale);
    }
}
