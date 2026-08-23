#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// A unit that drives: it faces the way it is going and runs its strip while
/// under way.
///
/// <para>One of exactly two <see cref="Unit"/> subclasses, and the split is by
/// BEHAVIOUR rather than by kind — a wireline truck and a rig convoy are both
/// this class holding different <see cref="UnitKind"/> resources. Plans 21 §P2.</para>
/// </summary>
public partial class VehicleUnit : Unit
{
    private WorkingProp _art = null!;

    protected override void Dress(UnitKind kind)
    {
        _art = new WorkingProp
        {
            Name = "Art",
            Texture = kind.Art,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };

        Fit(kind.Art, kind.DrawHeight, frames: 1);
        AddChild(_art);
    }

    protected override void Show(bool moving)
    {
        bool strip = moving && Kind.Working is not null && Kind.WorkingFrames > 1;

        _art.Texture = strip ? Kind.Working : Kind.Art;
        _art.Hframes = strip ? Kind.WorkingFrames : 1;
        _art.Running = strip;

        Fit(_art.Texture, Kind.DrawHeight, _art.Hframes);
    }

    /// <summary>
    /// Turn to face travel, by mirroring rather than rotating.
    /// </summary>
    /// <remarks>
    /// The art is drawn from one side, so rotating it would show a truck driving
    /// on its roof. Mirroring is the whole of the facing this art supports, and
    /// pretending otherwise would look worse than not turning at all.
    /// </remarks>
    protected override void Face(Vector2 by)
    {
        if (Mathf.Abs(by.X) > 0.5f)
            _art.FlipH = by.X < 0.0f;
    }

    /// <summary>Scale to the drawn height, measured off the FRAME not the sheet.</summary>
    private void Fit(Texture2D? texture, float tall, int frames)
    {
        if (texture is null)
            return;

        float scale = tall / Mathf.Max(1.0f, texture.GetHeight());
        _art.Scale = new Vector2(scale, scale);
        _art.Position = new Vector2(0.0f, -tall * 0.5f);
    }
}
