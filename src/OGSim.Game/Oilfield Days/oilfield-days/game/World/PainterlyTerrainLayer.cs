#nullable enable

using Godot;
using Beep.ECS;
using System;

namespace OilfieldDays.World;

/// <summary>
/// A single smoothed terrain image for the playable basin.
///
/// The logical world remains tile-based for placement and pathing, but the
/// visible base terrain should read like the supplied overview mockups rather
/// than like repeated 64px tiles. This layer turns the same maps into one
/// filtered texture with blended material boundaries and light surface noise.
/// </summary>
public sealed partial class PainterlyTerrainLayer : PainterlyTerrainComponent
{
    private static TerrainTextureSet? _textures;

    public void Repaint(WorldMap ground, TerrainMap terrain, int tileSize)
    {
        TileSize = tileSize;
        _textures ??= TerrainTextureSet.Load();
        RenderFromContinuousSampler(ground.Width, ground.Height, at => Sample(ground, terrain, at), tileSize);
    }

    private static PaintSample Sample(WorldMap ground, TerrainMap terrain, Vector2 at)
    {
        (TerrainKind? overlay, float overlayWeight) = OverlayAt(ground, at);
        Ground natural = terrain.At(at);
        float height = terrain.HeightAt(at);
        bool dry = terrain.IsDry(at);

        Color colour = TexturedTerrainColour(terrain, natural, height, dry, at);

        colour = TintByHeight(colour, height);
        colour = SurfaceDetail(colour, natural, dry, height, at);

        if (overlay is TerrainKind.DirtRoad)
            colour = colour.Lerp(MaterialTexture(TerrainKind.DirtRoad, at), overlayWeight * 0.62f);
        else if (overlay is TerrainKind.GravelPad)
            colour = colour.Lerp(MaterialTexture(TerrainKind.GravelPad, at), overlayWeight * 0.50f);

        return new PaintSample(colour, natural == Ground.Water ? TerrainPaintEffect.Water : TerrainPaintEffect.None);
    }

    private static Color TintByHeight(Color colour, float height)
    {
        return height >= 0.5f
            ? colour.Lightened((height - 0.5f) * 0.10f)
            : colour.Darkened((0.5f - height) * 0.08f);
    }

    private static Color TerrainColour(TerrainMap terrain, float height, bool dry)
    {
        Color water = new(0.05f, 0.40f, 0.54f);
        Color sand = new(0.58f, 0.50f, 0.30f);
        Color grass = dry ? new Color(0.42f, 0.48f, 0.28f) : new Color(0.25f, 0.48f, 0.20f);
        Color rock = new(0.34f, 0.38f, 0.35f);

        float waterToSand = SmoothStep(terrain.SeaLevel - 0.018f, terrain.SeaLevel + 0.036f, height);
        float sandToGrass = SmoothStep(
            terrain.SeaLevel + (terrain.ShoreWidth * 0.50f),
            terrain.SeaLevel + terrain.ShoreWidth + 0.050f,
            height);
        float grassToRock = SmoothStep(terrain.RockLine - 0.050f, terrain.RockLine + 0.040f, height);

        Color colour = water.Lerp(sand, waterToSand);
        colour = colour.Lerp(grass, sandToGrass);
        return colour.Lerp(rock, grassToRock);
    }

    private static Color TexturedTerrainColour(TerrainMap terrain, Ground ground, float height, bool dry, Vector2 at)
    {
        Color baseColour = TerrainColour(terrain, height, dry);
        Color texture = TerrainTexture(terrain, ground, height, dry, at);
        return baseColour.Lerp(texture, 0.72f);
    }

    private static Color TerrainTexture(TerrainMap terrain, Ground ground, float height, bool dry, Vector2 at)
    {
        if (_textures is null)
            return TerrainColour(terrain, height, dry);

        return ground switch
        {
            Ground.Water => WaterTexture(terrain, height, at),
            Ground.Sand => SandTexture(terrain, height, at),
            Ground.Rock => _textures.Rock.Sample(at, 5.5f, new Vector2(0.29f, 0.61f)),
            _ when dry => _textures.DryGrass.Sample(at, 7.5f, new Vector2(0.73f, 0.17f)),
            _ => _textures.Grass.Sample(at, 7.0f, new Vector2(0.11f, 0.37f)),
        };
    }

    private static Color WaterTexture(TerrainMap terrain, float height, Vector2 at)
    {
        if (_textures is null)
            return new Color(0.05f, 0.40f, 0.54f);

        float shallow = 1.0f - SmoothStep(terrain.SeaLevel - 0.020f, terrain.SeaLevel + 0.085f, height);
        Color deep = _textures.WaterDeep.Sample(at, 8.5f, new Vector2(0.05f, 0.42f));
        Color shallowColour = _textures.WaterShallow.Sample(at, 7.0f, new Vector2(0.31f, 0.76f));
        return deep.Lerp(shallowColour, shallow * 0.72f);
    }

    private static Color SandTexture(TerrainMap terrain, float height, Vector2 at)
    {
        if (_textures is null)
            return new Color(0.58f, 0.50f, 0.30f);

        float mud = 1.0f - SmoothStep(
            terrain.SeaLevel + (terrain.ShoreWidth * 0.15f),
            terrain.SeaLevel + (terrain.ShoreWidth * 0.95f),
            height);
        Color sand = _textures.Sand.Sample(at, 6.5f, new Vector2(0.19f, 0.47f));
        Color wet = _textures.Mud.Sample(at, 5.5f, new Vector2(0.64f, 0.08f));
        return sand.Lerp(wet, mud * 0.55f);
    }

    private static Color MaterialTexture(TerrainKind kind, Vector2 at)
    {
        if (_textures is null)
        {
            return kind == TerrainKind.DirtRoad
                ? new Color(0.50f, 0.36f, 0.20f)
                : new Color(0.39f, 0.41f, 0.39f);
        }

        return kind == TerrainKind.DirtRoad
            ? _textures.Dirt.Sample(at, 4.2f, new Vector2(0.09f, 0.55f)).Darkened(0.04f)
            : _textures.Gravel.Sample(at, 4.8f, new Vector2(0.42f, 0.23f)).Darkened(0.03f);
    }

    private static Color SurfaceDetail(Color colour, Ground ground, bool dry, float height, Vector2 at)
    {
        float broad = ValueNoise(at, 0.42f, 11);
        float medium = ValueNoise(at, 1.15f, 41);
        float fine = ValueNoise(at, 3.40f, 83);
        float texture = (broad * 0.50f) + (medium * 0.35f) + (fine * 0.15f);

        return ground switch
        {
            Ground.Water => WaterDetail(colour, texture, height, at),
            Ground.Sand => SandDetail(colour, texture, at),
            Ground.Rock => RockDetail(colour, texture),
            _ => GrassDetail(colour, texture, dry, at),
        };
    }

    private static Color WaterDetail(Color colour, float texture, float height, Vector2 at)
    {
        float shallow = Mathf.Clamp((0.58f - height) / 0.22f, 0.0f, 1.0f);
        float bands = Mathf.Sin((at.X * 1.8f) + (at.Y * 0.7f)) * 0.5f;
        Color deep = colour.Darkened(0.04f + Mathf.Max(0.0f, -texture) * 0.035f);
        return deep.Lerp(new Color(0.10f, 0.50f, 0.58f), shallow * 0.10f + Mathf.Max(0.0f, bands) * 0.025f);
    }

    private static Color SandDetail(Color colour, float texture, Vector2 at)
    {
        float streak = Mathf.Sin((at.X * 2.4f) - (at.Y * 0.55f)) * 0.5f;
        Color warm = colour.Lerp(new Color(0.68f, 0.54f, 0.28f), 0.18f);
        return texture + streak > 0.0f
            ? warm.Lightened((texture + streak) * 0.025f)
            : warm.Darkened(-(texture + streak) * 0.020f);
    }

    private static Color RockDetail(Color colour, float texture)
    {
        return texture > 0.0f
            ? colour.Lightened(texture * 0.06f)
            : colour.Darkened(-texture * 0.08f);
    }

    private static Color GrassDetail(Color colour, float texture, bool dry, Vector2 at)
    {
        float clump = ValueNoise(at, 2.15f, 127);
        Color target = dry
            ? new Color(0.50f, 0.48f, 0.24f)
            : new Color(0.18f, 0.40f, 0.16f);
        Color varied = colour.Lerp(target, Mathf.Clamp((texture + 1.0f) * 0.08f, 0.0f, 0.16f));
        return clump > 0.0f ? varied.Lightened(clump * 0.025f) : varied.Darkened(-clump * 0.030f);
    }

    private static float ValueNoise(Vector2 at, float frequency, int salt)
    {
        float x = at.X * frequency;
        float y = at.Y * frequency;
        int x0 = Mathf.FloorToInt(x);
        int y0 = Mathf.FloorToInt(y);
        float tx = Smooth01(x - x0);
        float ty = Smooth01(y - y0);

        float a = HashNoise(x0 + salt, y0 - salt);
        float b = HashNoise(x0 + 1 + salt, y0 - salt);
        float c = HashNoise(x0 + salt, y0 + 1 - salt);
        float d = HashNoise(x0 + 1 + salt, y0 + 1 - salt);

        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    private static float Smooth01(float t) => t * t * (3.0f - (2.0f * t));

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0), 0.0f, 1.0f);
        return Smooth01(t);
    }

    private static (TerrainKind? Kind, float Weight) OverlayAt(WorldMap ground, Vector2 at)
    {
        TerrainKind? bestKind = null;
        float bestWeight = 0.0f;
        int cx = Mathf.FloorToInt(at.X);
        int cy = Mathf.FloorToInt(at.Y);

        for (int y = cy - 2; y <= cy + 2; y++)
        {
            for (int x = cx - 2; x <= cx + 2; x++)
            {
                var cell = new Vector2I(x, y);
                TerrainKind kind = ground.At(cell);

                if (kind == TerrainKind.Grass)
                    continue;

                float distance = DistanceToCell(at, cell) + (HashNoise(x, y) * 0.20f);
                float weight = 1.0f - Mathf.Clamp((distance - 0.08f) / 1.15f, 0.0f, 1.0f);
                weight = weight * weight * (3.0f - (2.0f * weight));

                if (kind == TerrainKind.GravelPad)
                    weight *= 0.72f;

                if (weight <= bestWeight)
                    continue;

                bestKind = kind;
                bestWeight = weight;
            }
        }

        return (bestKind, bestWeight);
    }

    private static float DistanceToCell(Vector2 point, Vector2I cell)
    {
        float dx = Mathf.Max(Mathf.Max(cell.X - point.X, 0.0f), point.X - (cell.X + 1.0f));
        float dy = Mathf.Max(Mathf.Max(cell.Y - point.Y, 0.0f), point.Y - (cell.Y + 1.0f));

        return Mathf.Sqrt((dx * dx) + (dy * dy));
    }

    private static float HashNoise(int x, int y)
    {
        uint n = (uint)(x * 374761393) + (uint)(y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        n ^= n >> 16;

        return ((n & 255u) / 127.5f) - 1.0f;
    }

    private sealed class TerrainTextureSet
    {
        private TerrainTextureSet(
            SampledTerrainTexture grass,
            SampledTerrainTexture dryGrass,
            SampledTerrainTexture sand,
            SampledTerrainTexture mud,
            SampledTerrainTexture rock,
            SampledTerrainTexture gravel,
            SampledTerrainTexture dirt,
            SampledTerrainTexture waterShallow,
            SampledTerrainTexture waterDeep)
        {
            Grass = grass;
            DryGrass = dryGrass;
            Sand = sand;
            Mud = mud;
            Rock = rock;
            Gravel = gravel;
            Dirt = dirt;
            WaterShallow = waterShallow;
            WaterDeep = waterDeep;
        }

        public SampledTerrainTexture Grass { get; }
        public SampledTerrainTexture DryGrass { get; }
        public SampledTerrainTexture Sand { get; }
        public SampledTerrainTexture Mud { get; }
        public SampledTerrainTexture Rock { get; }
        public SampledTerrainTexture Gravel { get; }
        public SampledTerrainTexture Dirt { get; }
        public SampledTerrainTexture WaterShallow { get; }
        public SampledTerrainTexture WaterDeep { get; }

        public static TerrainTextureSet Load()
        {
            const string Root = "res://assets/terrain_textures/";
            return new TerrainTextureSet(
                SampledTerrainTexture.Load(Root + "grass.png", new Color(0.25f, 0.48f, 0.20f)),
                SampledTerrainTexture.Load(Root + "dry_grass.png", new Color(0.42f, 0.48f, 0.28f)),
                SampledTerrainTexture.Load(Root + "sand.png", new Color(0.58f, 0.50f, 0.30f)),
                SampledTerrainTexture.Load(Root + "mud.png", new Color(0.35f, 0.32f, 0.22f)),
                SampledTerrainTexture.Load(Root + "rock.png", new Color(0.34f, 0.38f, 0.35f)),
                SampledTerrainTexture.Load(Root + "gravel.png", new Color(0.39f, 0.41f, 0.39f)),
                SampledTerrainTexture.Load(Root + "dirt.png", new Color(0.50f, 0.36f, 0.20f)),
                SampledTerrainTexture.Load(Root + "water_shallow.png", new Color(0.10f, 0.50f, 0.58f)),
                SampledTerrainTexture.Load(Root + "water_deep.png", new Color(0.05f, 0.40f, 0.54f)));
        }
    }

    private sealed class SampledTerrainTexture
    {
        private readonly byte[] _data;
        private readonly Color _fallback;

        private SampledTerrainTexture(byte[] data, int width, int height, Color fallback)
        {
            _data = data;
            Width = width;
            Height = height;
            _fallback = fallback;
        }

        public int Width { get; }
        public int Height { get; }

        public static SampledTerrainTexture Load(string path, Color fallback)
        {
            try
            {
                Texture2D? texture = GD.Load<Texture2D>(path);
                Image? image = texture?.GetImage();
                if (image is null || image.IsEmpty())
                    return Solid(fallback);

                if (image.GetFormat() != Image.Format.Rgba8)
                    image.Convert(Image.Format.Rgba8);

                return new SampledTerrainTexture(image.GetData(), image.GetWidth(), image.GetHeight(), fallback);
            }
            catch (Exception ex)
            {
                GD.PushWarning($"Terrain texture '{path}' could not be loaded: {ex.Message}");
                return Solid(fallback);
            }
        }

        public Color Sample(Vector2 tile, float tilesPerRepeat, Vector2 offset)
        {
            if (_data.Length == 0 || Width <= 0 || Height <= 0)
                return _fallback;

            float u = Repeat((tile.X / Mathf.Max(0.001f, tilesPerRepeat)) + offset.X);
            float v = Repeat((tile.Y / Mathf.Max(0.001f, tilesPerRepeat)) + offset.Y);
            float x = u * (Width - 1);
            float y = v * (Height - 1);
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            int x1 = (x0 + 1) % Width;
            int y1 = (y0 + 1) % Height;
            float tx = x - x0;
            float ty = y - y0;

            Color a = Pixel(x0, y0);
            Color b = Pixel(x1, y0);
            Color c = Pixel(x0, y1);
            Color d = Pixel(x1, y1);

            return a.Lerp(b, tx).Lerp(c.Lerp(d, tx), ty);
        }

        private static SampledTerrainTexture Solid(Color fallback) => new(Array.Empty<byte>(), 0, 0, fallback);

        private Color Pixel(int x, int y)
        {
            int i = ((y * Width) + x) * 4;
            return new Color(
                _data[i] / 255.0f,
                _data[i + 1] / 255.0f,
                _data[i + 2] / 255.0f,
                _data[i + 3] / 255.0f);
        }

        private static float Repeat(float value) => value - Mathf.Floor(value);
    }
}
