#nullable enable

using Godot;
using System.Collections.Generic;

namespace OilfieldDays.World;

/// <summary>The ground a tile is made of, low to high.</summary>
public enum Ground
{
	Water = 0,
	Sand = 1,
	Grass = 2,
	Rock = 3,
}

/// <summary>
/// The basin's ground, drawn from a noise bitmap.
///
/// <para><b>Layered, not flat.</b> Two noise fields — height and moisture —
/// decide what every tile is made of: water in the hollows, sand at its shore,
/// grass across the middle, dry grass where the moisture runs out, and rock on
/// the tops. Each becomes its own tile layer, so the transitions are drawn by
/// the tilesets' own edge pieces rather than painted by hand.</para>
///
/// <para><b>Seeded from the engine's world seed.</b> The same seed gives the
/// same basin — the same structures from world generation, and now the same
/// ground under them — which is what plan 11 §5 needs for two players to compare
/// runs at all.</para>
///
/// <para>The bitmap is kept as an image so it can be saved and looked at when
/// the terrain misbehaves; nothing in the game reads it except this class.</para>
/// </summary>
public sealed class TerrainMap
{
    private readonly Ground[] _ground;
    private readonly bool[] _dry;
    private readonly float[] _height;
    private readonly float[] _moisture;
    private readonly List<Rect2I> _leveled = new();
    private readonly FastNoiseLite _heightNoise;
    private readonly FastNoiseLite _ridgeNoise;
    private readonly FastNoiseLite _moistureNoise;
    private readonly float _sea;
    private readonly float _arid;
    private readonly float _rockLine;

    /// <param name="landFraction">
    /// How much of the basin is dry land, straight off the world draft. It moves
    /// the shoreline, so the ground a player previews at setup is the ground the
    /// world is built from rather than a picture of a different basin.
    /// </param>
    /// <param name="climateSeverity">
    /// How hard the climate is, 0 to 1. It moves the line between green country
    /// and burnt country; it does not move the shoreline, because a severe
    /// climate makes a basin drier, not smaller.
    /// </param>
    public TerrainMap(int tiles, ulong seed, double landFraction, double climateSeverity)
    {
        Size = tiles;
        _ground = new Ground[tiles * tiles];
        _dry = new bool[tiles * tiles];
        _height = new float[tiles * tiles];
        _moisture = new float[tiles * tiles];

        // Fractal Brownian motion: each octave is LACUNARITY times finer and
        // PERSISTENCE times weaker than the one before it. Octaves decide how
        // much detail there is; persistence decides how much of it survives —
        // low persistence gives smooth country with a little texture, high
        // gives a noisy one. These are the two knobs the landscape has.
        const int Octaves = 5;
        const float Lacunarity = 2.0f;
        const float Persistence = 0.48f;

        _heightNoise = new FastNoiseLite
        {
            Seed = (int)(seed & 0x7FFFFFFF),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            Frequency = 0.010f,
            FractalOctaves = Octaves,
            FractalLacunarity = Lacunarity,
            FractalGain = Persistence,
        };

        // Ridged noise on top of the smooth field is what makes high ground read
        // as ridges and outcrops rather than as round hills.
        _ridgeNoise = new FastNoiseLite
        {
            Seed = (int)((seed >> 16) & 0x7FFFFFFF),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            Frequency = 0.021f,
            FractalOctaves = 3,
            FractalLacunarity = Lacunarity,
            FractalGain = 0.55f,
        };

        _moistureNoise = new FastNoiseLite
        {
            Seed = (int)((seed >> 8) & 0x7FFFFFFF),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            Frequency = 0.016f,
            FractalOctaves = 3,
            FractalLacunarity = Lacunarity,
            FractalGain = 0.45f,
        };

        Bitmap = Image.CreateEmpty(tiles, tiles, false, Image.Format.Rgb8);

        int count = tiles * tiles;
        float[] heights = new float[count];
        float[] moist = new float[count];

        for (int y = 0; y < tiles; y++)
        {
            for (int x = 0; x < tiles; x++)
            {
                // Noise runs -1..1; the game thinks in 0..1.
                float h = RawHeightAt(new Vector2(x, y));

                int i = (y * tiles) + x;
                heights[i] = h;
                moist[i] = RawMoistureAt(new Vector2(x, y));
                _height[i] = heights[i];
                _moisture[i] = moist[i];
            }
        }

        // Sea level is read off the terrain rather than guessed at: sort the
        // heights and cut where the asked-for fraction is above the line. Land
        // fraction then means what it says on the setup screen — 0.4 gives a
        // basin that is 40% dry, on any seed, at any size — instead of naming a
        // threshold whose effect depends on how the noise happened to fall.
        float[] sorted = (float[])heights.Clone();
        System.Array.Sort(sorted);

        _sea = sorted[Mathf.Clamp(
            (int)((1.0 - Mathf.Clamp(landFraction, 0.05, 0.98)) * count), 0, count - 1)];

        // The same trick for the wet/dry line: severity is the share of the
        // basin that comes up burnt.
        float[] wetness = (float[])moist.Clone();
        System.Array.Sort(wetness);

        _arid = wetness[Mathf.Clamp(
            (int)(Mathf.Clamp(climateSeverity, 0.0, 1.0) * count), 0, count - 1)];

        // Rock starts a fixed way up what is left above the water, so a drowned
        // basin does not turn its few remaining hills into a mountain range.
        _rockLine = _sea + ((1.0f - _sea) * RockHeadroom);

        for (int i = 0; i < count; i++)
        {
            float h = heights[i];

            Ground ground = h < _sea ? Ground.Water
                : h < _sea + BeachWidth ? Ground.Sand
                : h < _rockLine ? Ground.Grass
                : Ground.Rock;

            _ground[i] = ground;
            _dry[i] = moist[i] < _arid;

            Bitmap.SetPixel(i % tiles, i / tiles, new Color(h, moist[i], (float)ground / 3.0f));
        }
    }

    /// <summary>How much beach there is between water and grass.</summary>
    private const float BeachWidth = 0.06f;

    /// <summary>How far up the dry ground rock takes over.</summary>
    private const float RockHeadroom = 0.62f;

    public int Size { get; }

    /// <summary>The height/moisture/ground field, for inspection.</summary>
    public Image Bitmap { get; }

    public float SeaLevel => _sea;

    public float ShoreWidth => BeachWidth;

    public float RockLine => _rockLine;

    public Ground At(Vector2I cell) =>
        Contains(cell) ? _ground[(cell.Y * Size) + cell.X] : Ground.Grass;

    public Ground At(Vector2 tile)
    {
        float h = HeightAt(tile);

        return h < _sea ? Ground.Water
            : h < _sea + BeachWidth ? Ground.Sand
            : h < _rockLine ? Ground.Grass
            : Ground.Rock;
    }

    /// <summary>Whether a grass tile is the dry kind. Meaningless off grass.</summary>
    public bool IsDry(Vector2I cell) =>
        Contains(cell) && _dry[(cell.Y * Size) + cell.X];

    public bool IsDry(Vector2 tile) => MoistureAt(tile) < _arid;

    public float HeightAt(Vector2 tile) => Mathf.Max(RawHeightAt(tile), LeveledHeightAt(tile));

    public float MoistureAt(Vector2 tile) => RawMoistureAt(tile);

    public bool Contains(Vector2I cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < Size && cell.Y < Size;

    public void ClearLeveling() => _leveled.Clear();

    /// <summary>
    /// Flatten a patch to dry land, for anything that has to be built on.
    /// </summary>
    /// <remarks>
    /// A structure lands where world generation put it, and the ground does not
    /// get a say: a pad is cleared, and clearing it is what a company does when
    /// the rock says drill here and the map says swamp.
    /// </remarks>
    public void Level(Rect2I area)
    {
        _leveled.Add(area);

        for (int y = area.Position.Y; y < area.End.Y; y++)
        {
            for (int x = area.Position.X; x < area.End.X; x++)
            {
                var cell = new Vector2I(x, y);

                if (!Contains(cell))
                    continue;

                _ground[(cell.Y * Size) + cell.X] = Ground.Grass;
            }
        }
    }

    private float LeveledHeightAt(Vector2 tile)
    {
        float target = _sea + BeachWidth + 0.14f;
        float best = 0.0f;

        for (int i = 0; i < _leveled.Count; i++)
        {
            Rect2I rect = _leveled[i];
            Vector2 centre = rect.Position + (Vector2)rect.Size * 0.5f;
            Vector2 radius = new(
                Mathf.Max(2.5f, rect.Size.X * 0.62f),
                Mathf.Max(2.5f, rect.Size.Y * 0.62f));
            Vector2 delta = tile - centre;
            if (Mathf.Abs(delta.X) > radius.X * 1.6f || Mathf.Abs(delta.Y) > radius.Y * 1.6f)
                continue;

            float ellipse = Mathf.Sqrt(
                ((delta.X * delta.X) / (radius.X * radius.X)) +
                ((delta.Y * delta.Y) / (radius.Y * radius.Y)));
            float edgeNoise = LevelNoise(rect.Position.X + i, rect.Position.Y - i) * 0.10f;
            float weight = 1.0f - Mathf.Clamp((ellipse - 0.55f + edgeNoise) / 0.85f, 0.0f, 1.0f);
            weight = weight * weight * (3.0f - (2.0f * weight));
            best = Mathf.Max(best, Mathf.Lerp(0.0f, target, weight));
        }

        return best;
    }

    private static float LevelNoise(int x, int y)
    {
        uint n = (uint)(x * 374761393) + (uint)(y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        n ^= n >> 16;

        return ((n & 255u) / 127.5f) - 1.0f;
    }

    private float Bilinear(float[] values, Vector2 tile)
    {
        float x = Mathf.Clamp(tile.X, 0.0f, Size - 1.001f);
        float y = Mathf.Clamp(tile.Y, 0.0f, Size - 1.001f);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Size - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, Size - 1);
        int x1 = Mathf.Clamp(x0 + 1, 0, Size - 1);
        int y1 = Mathf.Clamp(y0 + 1, 0, Size - 1);
        float tx = Smooth(x - x0);
        float ty = Smooth(y - y0);

        float top = Mathf.Lerp(values[(y0 * Size) + x0], values[(y0 * Size) + x1], tx);
        float bottom = Mathf.Lerp(values[(y1 * Size) + x0], values[(y1 * Size) + x1], tx);

        return Mathf.Lerp(top, bottom, ty);
    }

    private static float Smooth(float t) => t * t * (3.0f - (2.0f * t));

    private float RawHeightAt(Vector2 tile)
    {
        float h = (_heightNoise.GetNoise2D(tile.X, tile.Y) + 1.0f) * 0.5f;
        float r = (_ridgeNoise.GetNoise2D(tile.X, tile.Y) + 1.0f) * 0.5f;
        h = Mathf.Lerp(h, Mathf.Max(h, r), Mathf.Clamp((h - 0.55f) / 0.25f, 0.0f, 1.0f));

        return h * EdgeFalloff(tile.X, tile.Y, Size);
    }

    private float RawMoistureAt(Vector2 tile) =>
        (_moistureNoise.GetNoise2D(tile.X, tile.Y) + 1.0f) * 0.5f;

    /// <summary>A rim of low ground so the basin fades out rather than being cut off.</summary>
    private static float EdgeFalloff(float x, float y, int tiles)
    {
        float margin = tiles * 0.06f;
        float dx = Mathf.Min(x, tiles - 1 - x);
        float dy = Mathf.Min(y, tiles - 1 - y);
        float d = Mathf.Min(dx, dy);

        return d >= margin ? 1.0f : Mathf.Lerp(0.55f, 1.0f, d / margin);
    }
}
