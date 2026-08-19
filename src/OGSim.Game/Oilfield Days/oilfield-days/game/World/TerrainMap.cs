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

    public TerrainMap(int tiles, ulong seed)
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

        for (int y = 0; y < tiles; y++)
        {
            for (int x = 0; x < tiles; x++)
            {
                // Noise runs -1..1; the game thinks in 0..1.
                float h = (height.GetNoise2D(x, y) + 1.0f) * 0.5f;
                float r = (ridge.GetNoise2D(x, y) + 1.0f) * 0.5f;
                float m = (moisture.GetNoise2D(x, y) + 1.0f) * 0.5f;

                // The ridges only bite where the ground is already high, so the
                // lowlands stay smooth and the tops break up.
                h = Mathf.Lerp(h, Mathf.Max(h, r), Mathf.Clamp((h - 0.55f) / 0.25f, 0.0f, 1.0f));

                // Pull the edges of the basin down a little so a field does not
                // start halfway up a cliff at the map border.
                float edge = EdgeFalloff(x, y, tiles);
                h *= edge;

                Ground ground = h switch
                {
                    < 0.30f => Ground.Water,
                    < 0.36f => Ground.Sand,
                    < 0.74f => Ground.Grass,
                    _ => Ground.Rock,
                };

                int i = (y * tiles) + x;
                _ground[i] = ground;
                _dry[i] = m < 0.42f;

                Bitmap.SetPixel(x, y, new Color(h, m, (float)ground / 3.0f));
            }
        }
    }

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
