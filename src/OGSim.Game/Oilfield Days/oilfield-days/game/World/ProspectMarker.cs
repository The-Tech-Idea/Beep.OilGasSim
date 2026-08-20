#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// An undrilled structure on the map: a survey stake, and a ring showing how
/// likely the company thinks it is to hold anything.
///
/// <para>Drawn rather than a sprite, because the thing being shown is a
/// <em>number</em> — the probability of success, which world generation risked
/// and every survey moves. A fixed picture could not show it changing, and plan
/// 09 §8 asks for exactly this on the map.</para>
///
/// <para>The colour follows the odds and nothing else: red under a fifth, amber
/// to a third, green above. A player choosing between seven of these is reading
/// the ring before the caption.</para>
/// </summary>
public sealed partial class ProspectMarker : Node2D
{
    private const float Radius = 26.0f;

    private double _probability;
    private bool _surveyed;

    public double Probability
    {
        get => _probability;
        set
        {
            _probability = Mathf.Clamp(value, 0.0, 1.0);
            QueueRedraw();
        }
    }

    /// <summary>Whether the company has shot seismic here. Marks the stake done.</summary>
    public bool Surveyed
    {
        get => _surveyed;
        set
        {
            _surveyed = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Color odds = ColourFor(_probability);

        // The stake: a post in the ground with a flag, so an undrilled structure
        // reads as "somebody has been here and marked it", not as plant.
        DrawLine(new Vector2(0, 0), new Vector2(0, -46), new Color(0.20f, 0.16f, 0.12f), 5.0f);
        DrawColoredPolygon(
            [new Vector2(0, -46), new Vector2(26, -38), new Vector2(0, -30)],
            _surveyed ? odds : odds.Darkened(0.25f));

        // The ring: full circle is certainty, and the arc is what is believed.
        DrawArc(new Vector2(0, -8), Radius, 0.0f, Mathf.Tau, 48, new Color(0, 0, 0, 0.45f), 6.0f);
        DrawArc(
            new Vector2(0, -8),
            Radius,
            -Mathf.Pi / 2.0f,
            (-Mathf.Pi / 2.0f) + (float)(Mathf.Tau * _probability),
            48,
            odds,
            6.0f);

        if (_surveyed)
            DrawArc(new Vector2(0, -8), Radius + 7.0f, 0.0f, Mathf.Tau, 48, new Color(1, 1, 1, 0.35f), 2.0f);
    }

    private static Color ColourFor(double probability) => probability switch
    {
        < 0.20 => new Color(0.85f, 0.32f, 0.26f),
        < 0.35 => new Color(0.93f, 0.72f, 0.29f),
        _ => new Color(0.40f, 0.78f, 0.40f),
    };
}
