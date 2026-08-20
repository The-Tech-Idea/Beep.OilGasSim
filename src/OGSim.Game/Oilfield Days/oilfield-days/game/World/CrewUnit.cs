#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// A unit that walks: same lifecycle, no facing beyond a mirror, and a small
/// bob so a crew on the road does not read as a sticker sliding across it.
///
/// <para>The second of two <see cref="Unit"/> subclasses. It exists because
/// crews MOVE differently, not because a survey crew is a different kind of
/// thing from a coring unit — those differ in their <see cref="UnitKind"/>
/// (plans 21 §P2).</para>
/// </summary>
public partial class CrewUnit : Unit
{
    /// <summary>How far the walk cycle lifts, in pixels.</summary>
    private const float Bob = 3.0f;

    private WorkingProp _art = null!;
    private float _stride;

    protected override void Dress(UnitKind kind)
    {
        _art = new WorkingProp
        {
            Name = "Art",
            Texture = kind.Art,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        };

        if (kind.Art is not null)
        {
            float scale = kind.DrawHeight / Mathf.Max(1.0f, kind.Art.GetHeight());
            _art.Scale = new Vector2(scale, scale);
        }

        AddChild(_art);
        Rest();
    }

    protected override void Show(bool moving)
    {
        if (!moving)
            Rest();
    }

    protected override void Face(Vector2 by)
    {
        if (Mathf.Abs(by.X) > 0.5f)
            _art.FlipH = by.X < 0.0f;

        // A crew with no walk strip still has to look like it is walking. The
        // bob is the cheapest honest answer: it is motion the art can carry
        // without inventing frames nobody drew.
        _stride += by.Length() * 0.05f;
        _art.Position = new Vector2(
            0.0f,
            -(Kind.DrawHeight * 0.5f) + (Mathf.Sin(_stride) * Bob));
    }

    private void Rest() =>
        _art.Position = new Vector2(0.0f, -(Kind.DrawHeight * 0.5f));
}
