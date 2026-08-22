#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// A single smoothed terrain image for the playable basin.
///
/// The logical world remains tile-based for placement and pathing, but the
/// visible base terrain should read like the supplied overview mockups rather
/// than like repeated 64px tiles. This layer turns the same maps into one
/// filtered texture with blended material boundaries and light surface noise.
/// </summary>
public sealed partial class PainterlyTerrainLayer : Sprite2D
{
    private const int PixelsPerTile = 16;

    public void Repaint(WorldMap ground, TerrainMap terrain, int tileSize)
    {
        int width = ground.Width * PixelsPerTile;
        int height = ground.Height * PixelsPerTile;
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);

        for (int y = 0; y < height; y++)
        {
            float gy = ((float)y / PixelsPerTile) - 0.5f;
            int y0 = Mathf.FloorToInt(gy);
            int y1 = y0 + 1;
            float ty = Mathf.Clamp(gy - y0, 0.0f, 1.0f);

            for (int x = 0; x < width; x++)
            {
                float gx = ((float)x / PixelsPerTile) - 0.5f;
                int x0 = Mathf.FloorToInt(gx);
                int x1 = x0 + 1;
                float tx = Mathf.Clamp(gx - x0, 0.0f, 1.0f);

                Color a = Sample(ground, terrain, x0, y0);
                Color b = Sample(ground, terrain, x1, y0);
                Color c = Sample(ground, terrain, x0, y1);
                Color d = Sample(ground, terrain, x1, y1);

                Color top = a.Lerp(b, Smooth(tx));
                Color bottom = c.Lerp(d, Smooth(tx));
                Color colour = top.Lerp(bottom, Smooth(ty));

                float grain = Grain(x, y);
                colour = colour.Lightened(Mathf.Max(0.0f, grain) * 0.10f);
                colour = colour.Darkened(Mathf.Max(0.0f, -grain) * 0.08f);

                image.SetPixel(x, y, colour);
            }
        }

        Texture = ImageTexture.CreateFromImage(image);
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        Centered = false;
        Scale = new Vector2((float)tileSize / PixelsPerTile, (float)tileSize / PixelsPerTile);
    }

    private static Color Sample(WorldMap ground, TerrainMap terrain, int x, int y)
    {
        var cell = new Vector2I(
            Mathf.Clamp(x, 0, ground.Width - 1),
            Mathf.Clamp(y, 0, ground.Height - 1));

        return ground.At(cell) switch
        {
            TerrainKind.DirtRoad => new Color(0.58f, 0.43f, 0.25f),
            TerrainKind.GravelPad => new Color(0.43f, 0.45f, 0.43f),
            _ => terrain.At(cell) switch
            {
                Ground.Water => new Color(0.05f, 0.40f, 0.54f),
                Ground.Sand => new Color(0.58f, 0.50f, 0.30f),
                Ground.Rock => new Color(0.34f, 0.38f, 0.35f),
                _ when terrain.IsDry(cell) => new Color(0.42f, 0.48f, 0.28f),
                _ => new Color(0.25f, 0.48f, 0.20f),
            },
        };
    }

    private static float Smooth(float t) => t * t * (3.0f - (2.0f * t));

    private static float Grain(int x, int y)
    {
        uint n = (uint)(x * 374761393) + (uint)(y * 668265263);
        n = (n ^ (n >> 13)) * 1274126177u;
        n ^= n >> 16;

        return ((n & 255u) / 127.5f) - 1.0f;
    }
}
