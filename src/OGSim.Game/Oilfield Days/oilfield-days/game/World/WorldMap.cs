#nullable enable

using System;
using Godot;

namespace OilfieldDays.World;

/// <summary>What a logical cell is made of. One ground material per cell.</summary>
public enum TerrainKind
{
    Grass,
    DirtRoad,
    GravelPad,
}

/// <summary>
/// The logical ground of a site: one material per cell, and nothing else.
///
/// <para><b>Logical, not drawn.</b> Plan 11 §6.1 splits the world into a terrain
/// layer, a building layer and a vehicle layer; this is the first of those, and
/// it deliberately knows nothing about tiles, atlases or corners. What a cell
/// <em>looks</em> like is decided by <see cref="DualGridTerrain"/> from the four
/// cells around each rendered tile, which is why the map can stay this simple.</para>
/// </summary>
public sealed class WorldMap
{
    private readonly TerrainKind[] _cells;

    public WorldMap(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "a map has width");

        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "a map has height");

        Width = width;
        Height = height;
        _cells = new TerrainKind[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public bool Contains(Vector2I cell) =>
        cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height;

    public TerrainKind At(Vector2I cell) =>
        Contains(cell) ? _cells[(cell.Y * Width) + cell.X] : TerrainKind.Grass;

    public void Set(Vector2I cell, TerrainKind kind)
    {
        if (!Contains(cell))
            return;

        _cells[(cell.Y * Width) + cell.X] = kind;
    }

    public void Fill(Rect2I area, TerrainKind kind)
    {
        for (int y = area.Position.Y; y < area.End.Y; y++)
        {
            for (int x = area.Position.X; x < area.End.X; x++)
                Set(new Vector2I(x, y), kind);
        }
    }

    /// <summary>
    /// Lay a road of <paramref name="width"/> cells from one point to another,
    /// horizontal leg first. Roads in the mockup turn square corners, not curves.
    /// </summary>
    public void Road(Vector2I from, Vector2I to, int width)
    {
        int half = Math.Max(0, width - 1) / 2;
        int step = Math.Sign(to.X - from.X);

        for (int x = from.X; step != 0 && x != to.X + step; x += step)
        {
            for (int w = -half; w <= half; w++)
                Set(new Vector2I(x, from.Y + w), TerrainKind.DirtRoad);
        }

        step = Math.Sign(to.Y - from.Y);

        for (int y = from.Y; step != 0 && y != to.Y + step; y += step)
        {
            for (int w = -half; w <= half; w++)
                Set(new Vector2I(to.X + w, y), TerrainKind.DirtRoad);
        }
    }

    /// <summary>Whether a cell can be driven over. Nothing here blocks a truck yet;
    /// what blocks it is placed plant, which the site owns rather than the map.</summary>
    public bool IsDrivable(Vector2I cell) => Contains(cell);
}
