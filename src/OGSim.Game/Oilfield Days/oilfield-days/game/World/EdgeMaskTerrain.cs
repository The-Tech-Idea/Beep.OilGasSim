#nullable enable

using System;
using Godot;

namespace OilfieldDays.World;

/// <summary>
/// Draws one terrain as a 17-piece edge-mask autotile — the layout the
/// project's own tilesets are cut to.
///
/// <para><b>Not a dual grid.</b> A 15-piece SpriteCook atlas pictures the four
/// <em>corners</em> around a crossing point, so its tiles sit between cells. The
/// 17-piece sheets in <c>assets/tilesets</c> picture a piece of terrain with
/// <em>edges</em> — a top-left corner, a top edge, a vertical strip — so one
/// tile sits on one cell and is chosen by which of its four neighbours share the
/// terrain. The two cannot share a renderer, and using the wrong one puts every
/// edge half a tile out.</para>
///
/// <para>The atlas is five columns by five rows:</para>
/// <code>
///   .      .      .      .      .
///   v-top  TL     T      TR     inner
///   v-mid  L      C      R      .
///   v-bot  BL     B      BR     .
///   single H-L    H      H-R    .
/// </code>
/// </summary>
public sealed partial class EdgeMaskTerrain : TileMapLayer
{
    private const int AtlasColumns = 5;

    /// <summary>
    /// Neighbour mask (N=1, E=2, S=4, W=8) to the atlas cell that shows it.
    /// </summary>
    /// <remarks>
    /// Read off the sheets rather than assumed: index 12 is the fully enclosed
    /// centre, 20 is a tile with nothing beside it, and the strips along column
    /// zero and row four are the one-wide cases a field full of narrow roads
    /// spends most of its time in.
    /// </remarks>
    private static readonly int[] TileByMask =
    {
        20, // ....  isolated
        15, // N     bottom of a vertical strip
        21, // E     left end of a horizontal strip
        16, // NE    bottom-left corner
        5,  // S     top of a vertical strip
        10, // NS    vertical middle
        6,  // ES    top-left corner
        11, // NES   left edge
        23, // W     right end of a horizontal strip
        18, // NW    bottom-right corner
        22, // EW    horizontal middle
        17, // NEW   bottom edge
        8,  // SW    top-right corner
        13, // NSW   right edge
        7,  // ESW   top edge
        12, // NESW  centre
    };

    private int _sourceId = -1;

    /// <summary>Build the layer's tile set from one 5x5 atlas.</summary>
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
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
    }

    /// <summary>
    /// Repaint the layer wherever <paramref name="isMaterial"/> holds.
    /// </summary>
    /// <param name="treatOutsideAsMaterial">
    /// Whether the world's edge counts as more of the same. True for the ground
    /// everything else is drawn on — otherwise the basin is ringed by a coastline
    /// it does not have — and false for a road or a pad, which really do end.
    /// </param>
    public void Repaint(WorldMap map, Func<Vector2I, bool> isMaterial, bool treatOutsideAsMaterial = false)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(isMaterial);

        if (_sourceId < 0)
            throw new InvalidOperationException("UseAtlas must be called before the layer can be painted");

        Clear();

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var cell = new Vector2I(x, y);

                if (!isMaterial(cell))
                    continue;

                int mask = 0;

                if (Holds(map, isMaterial, x, y - 1, treatOutsideAsMaterial)) mask |= 1;   // north
                if (Holds(map, isMaterial, x + 1, y, treatOutsideAsMaterial)) mask |= 2;   // east
                if (Holds(map, isMaterial, x, y + 1, treatOutsideAsMaterial)) mask |= 4;   // south
                if (Holds(map, isMaterial, x - 1, y, treatOutsideAsMaterial)) mask |= 8;   // west

                int tile = TileByMask[mask];

                SetCell(cell, _sourceId, new Vector2I(tile % AtlasColumns, tile / AtlasColumns));
            }
        }
    }

    private static bool Holds(WorldMap map, Func<Vector2I, bool> isMaterial, int x, int y, bool outsideCounts)
    {
        var cell = new Vector2I(x, y);

        return map.Contains(cell) ? isMaterial(cell) : outsideCounts;
    }
}
