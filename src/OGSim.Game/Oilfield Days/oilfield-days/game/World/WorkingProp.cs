#nullable enable

using Godot;

namespace OilfieldDays.World;

/// <summary>
/// A structure that is doing something, drawn from its animation strip.
///
/// <para>The supplied art has two halves — a still for every structure and
/// twenty animation strips beside them — and the build had been using only the
/// stills. A flare that never lights and a pump that never turns are the same
/// picture whether the field is at plateau or shut in, which throws away the one
/// thing a top-down plant is good at showing.</para>
///
/// <para><b>It runs when the engine says it is running.</b> The animation is
/// switched on by <c>Throughput &gt; 0</c> — a published number — and off when it
/// is not. Nothing here decides that a thing is working; it renders that it is.
/// A stopped element is its own still frame, which is why the strips share frame
/// zero with the sprite beside them.</para>
/// </summary>
public sealed partial class WorkingProp : Sprite2D
{
    /// <summary>Frames a second. Slow enough to read, fast enough to live.</summary>
    private const double Fps = 9.0;

    private double _elapsed;

    /// <summary>Whether the strip advances, or holds on frame zero.</summary>
    public bool Running { get; set; }

    public override void _Process(double delta)
    {
        if (!Running || Hframes <= 1)
        {
            Frame = 0;

            return;
        }

        _elapsed += delta;

        // Modulo rather than a counter reset, so a long pause does not make the
        // strip jump when the clock resumes.
        Frame = (int)(_elapsed * Fps) % Hframes;
    }
}
