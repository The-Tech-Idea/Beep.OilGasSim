#nullable enable

using Godot;

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

        // Fractal Brownian motion: each octave is LACUNARITY times finer and
        // PERSISTENCE times weaker than the one before it. Octaves decide how
        // much detail there is; persistence decides how much of it survives —
        // low persistence gives smooth country with a little texture, high
        // gives a noisy one. These are the two knobs the landscape has.
        const int Octaves = 5;
        const float Lacunarity = 2.0f;
        const float Persistence = 0.48f;

        var height = new FastNoiseLite
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
        var ridge = new FastNoiseLite
        {
            Seed = (int)((seed >> 16) & 0x7FFFFFFF),
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            Frequency = 0.021f,
            FractalOctaves = 3,
            FractalLacunarity = Lacunarity,
            FractalGain = 0.55f,
        };

        var moisture = new FastNoiseLite
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
                float h = (height.GetNoise2D(x, y) + 1.0f) * 0.5f;
                float r = (ridge.GetNoise2D(x, y) + 1.0f) * 0.5f;

                // The ridges only bite where the ground is already high, so the
                // lowlands stay smooth and the tops break up.
                h = Mathf.Lerp(h, Mathf.Max(h, r), Mathf.Clamp((h - 0.55f) / 0.25f, 0.0f, 1.0f));

                // Pull the edges of the basin down so a field does not start
                // halfway up a cliff at the map border.
                h *= EdgeFalloff(x, y, tiles);

                int i = (y * tiles) + x;
                heights[i] = h;
                moist[i] = (moisture.GetNoise2D(x, y) + 1.0f) * 0.5f;
            }
        }

        // Sea level is read off the terrain rather than guessed at: sort the
        // heights and cut where the asked-for fraction is above the line. Land
        // fraction then means what it says on the setup screen — 0.4 gives a
        // basin that is 40% dry, on any seed, at any size — instead of naming a
        // threshold whose effect depends on how the noise happened to fall.
        float[] sorted = (float[])heights.Clone();
        System.Array.Sort(sorted);

        float sea = sorted[Mathf.Clamp(
            (int)((1.0 - Mathf.Clamp(landFraction, 0.05, 0.98)) * count), 0, count - 1)];

        // The same trick for the wet/dry line: severity is the share of the
        // basin that comes up burnt.
        float[] wetness = (float[])moist.Clone();
        System.Array.Sort(wetness);

        float arid = wetness[Mathf.Clamp(
            (int)(Mathf.Clamp(climateSeverity, 0.0, 1.0) * count), 0, count - 1)];

        // Rock starts a fixed way up what is left above the water, so a drowned
        // basin does not turn its few remaining hills into a mountain range.
        float rockLine = sea + ((1.0f - sea) * RockHeadroom);

        for (int i = 0; i < count; i++)
        {
            float h = heights[i];

            Ground ground = h < sea ? Ground.Water
                : h < sea + BeachWidth ? Ground.Sand
                : h < rockLine ? Ground.Grass
                : Ground.Rock;

            _ground[i] = ground;
            _dry[i] = moist[i] < arid;

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

    public Ground At(Vector2I cell) =>
        Contains(cell) ? _ground[(cell.Y * Size) + cell.X] : Ground.Grass;

    /// <summary>Whether a grass tile is the dry kind. Meaningless off grass.</summary>
    public bool IsDry(Vector2I cell) =>
        Contains(cell) && _dry[(cell.Y * Size) + cell.X];

    public bool Contains(Vector2I cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < Size && cell.Y < Size;

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

    /// <summary>A rim of low ground so the basin fades out rather than being cut off.</summary>
    private static float EdgeFalloff(int x, int y, int tiles)
    {
        float margin = tiles * 0.06f;
        float dx = Mathf.Min(x, tiles - 1 - x);
        float dy = Mathf.Min(y, tiles - 1 - y);
        float d = Mathf.Min(dx, dy);

        return d >= margin ? 1.0f : Mathf.Lerp(0.55f, 1.0f, d / margin);
    }
}
