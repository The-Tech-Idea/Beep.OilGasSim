#nullable enable

using System;
using Godot;

namespace OilfieldDays.World;

/// <summary>
/// Draws one terrain material as a SpriteCook 15-piece dual-grid layer.
///
/// <para><b>Why dual grid.</b> The generated atlases are not eight-neighbour blob
/// sets — each of the sixteen cells is the picture of a 2x2 <em>corner</em>
/// pattern. So a drawn tile does not sit on a logical cell; it sits on the
/// crossing point between four of them, offset by half a tile, and its picture is
/// chosen by which of those four are the material. That is what makes an edge
/// land where two materials actually meet instead of on a cell boundary, and it
/// is why every material gets its own layer rather than sharing one.</para>
///
/// <para>The mask bit order and the mask-to-frame table below are SpriteCook's
/// own; they are not a convention that can be re-derived by looking at the art.</para>
/// </summary>
public sealed partial class DualGridTerrain : TileMapLayer
{
    /// <summary>Mask (TL=1, TR=2, BL=4, BR=8) to atlas frame. -1 draws nothing.</summary>
    private static readonly int[] FrameByMask =
    {
        -1, 15, 8, 9, 0, 11, 14, 7, 13, 4, 1, 10, 3, 2, 5, 6,
    };

    private const int AtlasColumns = 4;

    private int _sourceId = -1;

    /// <summary>
    /// Build the layer's tile set from one atlas texture.
    /// </summary>
    /// <param name="atlas">A 4x4 SpriteCook 15-piece atlas.</param>
    /// <param name="tileSize">Edge length of one tile in the atlas, in pixels.</param>
    public void UseAtlas(Texture2D atlas, int tileSize)
    {
        ArgumentNullException.ThrowIfNull(atlas);

        var source = new TileSetAtlasSource
        {
            Texture = atlas,
            TextureRegionSize = new Vector2I(tileSize, tileSize),
        };

        for (int row = 0; row < AtlasColumns; row++)
        {
            for (int column = 0; column < AtlasColumns; column++)
                source.CreateTile(new Vector2I(column, row));
        }

        var tileSet = new TileSet { TileSize = new Vector2I(tileSize, tileSize) };
        _sourceId = tileSet.AddSource(source);

        TileSet = tileSet;
        // The atlases are 64 px tiles drawn at that size, so they are not being
        // reduced — but linear keeps the grass edge soft when the camera zooms
        // out rather than crawling with hard steps.
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;

        // The half-tile shift is the whole trick: rendered tiles live between
        // logical cells, so the layer is offset by half a cell in both axes.
        Position = new Vector2(-tileSize / 2.0f, -tileSize / 2.0f);
    }

    /// <summary>
    /// Repaint the layer for a map, drawing wherever <paramref name="isMaterial"/>
    /// holds.
    /// </summary>
    /// <remarks>
    /// One more row and column than the map is painted, because a rendered tile
    /// sits at the corner of four cells and the far edge has cells on one side
    /// only — without the extra pass the material would end a half tile early.
    /// </remarks>
    public void Repaint(WorldMap map, Func<Vector2I, bool> isMaterial)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(isMaterial);

        if (_sourceId < 0)
            throw new InvalidOperationException("UseAtlas must be called before the layer can be painted");

        Clear();

        for (int y = 0; y <= map.Height; y++)
        {
            for (int x = 0; x <= map.Width; x++)
            {
                int mask = 0;

                if (Holds(map, isMaterial, x - 1, y - 1)) mask |= 1;   // top-left
                if (Holds(map, isMaterial, x, y - 1)) mask |= 2;       // top-right
                if (Holds(map, isMaterial, x - 1, y)) mask |= 4;       // bottom-left
                if (Holds(map, isMaterial, x, y)) mask |= 8;           // bottom-right

                int frame = FrameByMask[mask];

                if (frame < 0)
                    continue;

                SetCell(
                    new Vector2I(x, y),
                    _sourceId,
                    new Vector2I(frame % AtlasColumns, frame / AtlasColumns));
            }
        }
    }

    private static bool Holds(WorldMap map, Func<Vector2I, bool> isMaterial, int x, int y)
    {
        var cell = new Vector2I(x, y);

        return map.Contains(cell) && isMaterial(cell);
    }
}
