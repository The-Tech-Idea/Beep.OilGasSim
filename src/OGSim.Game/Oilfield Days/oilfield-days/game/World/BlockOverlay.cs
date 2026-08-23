#nullable enable

using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OilfieldDays.App;

namespace OilfieldDays.World;

/// <summary>
/// The licence drawn block by block: which ground has been shot, and which is
/// still dark.
///
/// <para><b>The veil is over the SUBSURFACE, never over the landscape.</b> A
/// company can plainly see its own coastline, so hiding the terrain would be
/// hiding something nobody is uncertain about. What an unshot block gets is a
/// wash and a dashed edge — enough to read as "not looked at yet" while the
/// ground underneath stays visible (SDD-010 §4b's S1 amendment, plans 22
/// §3d).</para>
///
/// <para>Belief only. Every block on screen comes from the read model, so a
/// block the engine has not published cannot be drawn and a structure the
/// company has not found cannot leak through the shading.</para>
/// </summary>
public sealed partial class BlockOverlay : Node2D
{
    private readonly List<BlockView> _blocks = new();

    /// <summary>The block under a point, or null between them.</summary>
    public BlockView? At(Vector2 point)
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            if (RectOf(_blocks[i]).HasPoint(point))
                return _blocks[i];
        }

        return null;
    }

    /// <summary>Which block the pointer is over, drawn brighter than the rest.</summary>
    public void Hover(Vector2? point)
    {
        BlockView? was = _under;

        _under = point is Vector2 at ? At(at) : null;

        if (!ReferenceEquals(was, _under))
            QueueRedraw();
    }

    private BlockView? _under;

    /// <summary>Take this tick's licence.</summary>
    public void Show(IReadOnlyList<BlockView> blocks)
    {
        _blocks.Clear();

        for (int i = 0; i < blocks.Count; i++)
            _blocks.Add(blocks[i]);

        QueueRedraw();
    }

    /// <summary>
    /// Watch the camera, because this layer is drawn in SCREEN terms on top of
    /// a world that scales.
    /// </summary>
    /// <remarks>
    /// A licence block is kilometres across. Strokes and type sized in world
    /// units are a finger thick standing in the yard and invisible from the
    /// whole-basin view — the two places this overlay actually has to be read.
    /// Dividing by the zoom holds them at one size on screen at every step.
    /// </remarks>
    public override void _Process(double delta)
    {
        float now = GetViewport().GetCamera2D()?.Zoom.X ?? 1.0f;

        if (Mathf.IsEqualApprox(now, _zoom))
            return;

        _zoom = now;
        QueueRedraw();
    }

    private float _zoom = 1.0f;

    /// <summary>World units per on-screen pixel.</summary>
    private float Pixel => 1.0f / Mathf.Max(_zoom, 0.001f);

    public override void _Draw()
    {
        for (int i = 0; i < _blocks.Count; i++)
        {
            BlockView block = _blocks[i];
            Rect2 rect = RectOf(block);
            bool under = ReferenceEquals(block, _under);

            if (!block.Surveyed)
            {
                // DARK, and the only thing on screen that is. The wash is what
                // makes a shot block feel bought.
                DrawRect(rect, under ? UnshotLit : Unshot, filled: true);
                Dashed(rect, under ? KitTheme.Amber : Edge);
            }
            else
            {
                // Shot ground carries a hairline and nothing else: the map is
                // the reward, so anything drawn over it is taking that back.
                DrawRect(rect, block.Structures > 0 ? Found : Barren, filled: false, width: EdgeWidth * Pixel);
            }

            Label(block, rect);
        }
    }

    /// <summary>
    /// What the block is called, and what came of it.
    /// </summary>
    /// <remarks>
    /// A shot block that found nothing says so in words. Left to the shading
    /// alone it would read as a block nobody had got to yet, which is the one
    /// thing a company that paid to rule it out must not be told.
    /// </remarks>
    private void Label(BlockView block, Rect2 rect)
    {
        Font font = ThemeDB.GetDefaultTheme().DefaultFont ?? ThemeDB.FallbackFont;

        float inset = Inset * Pixel;
        int size = Mathf.Max(1, (int)(FontSize * Pixel));
        var at = new Vector2(rect.Position.X + inset, rect.Position.Y + inset + size);

        string name = "BLOCK " + block.Block.Value.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

        DrawString(font, at, name, HorizontalAlignment.Left, -1, size,
                   block.Surveyed ? KitTheme.Muted : KitTheme.Amber);

        if (!block.Surveyed)
            return;

        string outcome = block.Structures > 0
            ? block.Structures + (block.Structures == 1 ? " structure" : " structures")
            : "nothing here";

        DrawString(font, at + new Vector2(0.0f, size + inset), outcome,
                   HorizontalAlignment.Left, -1, size,
                   block.Structures > 0 ? KitTheme.Sky : KitTheme.Muted);
    }

    private static Rect2 RectOf(BlockView block)
    {
        var centre = BasinWorld.ToWorld(block.Centre);
        var size = new Vector2(
            BasinWorld.ToWorld(new OGSim.Kernel.Coordinate(block.Wide.Metres, block.Tall.Metres)).X,
            BasinWorld.ToWorld(new OGSim.Kernel.Coordinate(block.Wide.Metres, block.Tall.Metres)).Y);

        return new Rect2(centre - (size * 0.5f), size);
    }

    /// <summary>
    /// A dashed edge, drawn by hand because Godot's rect outline is solid and a
    /// solid box reads as a border rather than as an invitation.
    /// </summary>
    private void Dashed(Rect2 rect, Color colour)
    {
        float dash = Dash * Pixel;
        float width = EdgeWidth * Pixel;

        for (float x = rect.Position.X; x < rect.End.X; x += dash * 2.0f)
        {
            float to = Mathf.Min(x + dash, rect.End.X);

            DrawLine(new Vector2(x, rect.Position.Y), new Vector2(to, rect.Position.Y), colour, width);
            DrawLine(new Vector2(x, rect.End.Y), new Vector2(to, rect.End.Y), colour, width);
        }

        for (float y = rect.Position.Y; y < rect.End.Y; y += dash * 2.0f)
        {
            float to = Mathf.Min(y + dash, rect.End.Y);

            DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.Position.X, to), colour, width);
            DrawLine(new Vector2(rect.End.X, y), new Vector2(rect.End.X, to), colour, width);
        }
    }

    private static readonly Color Unshot = new(0.04f, 0.07f, 0.10f, 0.55f);
    private static readonly Color UnshotLit = new(0.04f, 0.07f, 0.10f, 0.34f);
    private static readonly Color Edge = new(0.55f, 0.68f, 0.78f, 0.35f);
    private static readonly Color Found = new(0.35f, 0.66f, 0.85f, 0.30f);
    private static readonly Color Barren = new(0.50f, 0.58f, 0.65f, 0.16f);

    private const float EdgeWidth = 2.0f;
    private const float Dash = 14.0f;
    private const float Inset = 10.0f;
    private const int FontSize = 20;
}
