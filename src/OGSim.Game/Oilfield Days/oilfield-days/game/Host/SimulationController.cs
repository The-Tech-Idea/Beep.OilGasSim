#nullable enable

using Godot;
using OGSim.Kernel;

namespace OilfieldDays.Host;

/// <summary>
/// Pause and pacing (plan 05 §1, plan 06 phase 2).
///
/// <para>The engine is turn-based and the game is real-time-with-pause: a month
/// is a tick whatever speed the player picked, and speed only decides how long
/// the host waits before asking for the next one. Plan 07 §3 is explicit that
/// the addon's <c>GameSpeedComponent</c> must not be used for this — it is wired
/// to a demo economy — so the pacing is ours.</para>
///
/// <para>Nothing else in the game may call <see cref="EngineHost.AdvanceMonth"/>.
/// One clock, one caller.</para>
/// </summary>
public sealed partial class SimulationController : Node
{
    public static SimulationController Instance { get; private set; } = null!;

    /// <summary>Seconds of real time per simulated month, by speed.</summary>
    private static readonly double[] SecondsPerMonth = { 0.0, 6.0, 3.0, 1.2 };

    private double _elapsed;

    public enum Speed
    {
        Paused = 0,
        Slow = 1,
        Normal = 2,
        Fast = 3,
    }

    [Signal]
    public delegate void SpeedChangedEventHandler(int speed);

    public Speed Current { get; private set; } = Speed.Paused;

    public override void _EnterTree() => Instance = this;

    public override void _Process(double delta)
    {
        if (Current == Speed.Paused || !EngineHost.Instance.Running)
            return;

        _elapsed += delta;

        if (_elapsed < SecondsPerMonth[(int)Current])
            return;

        _elapsed = 0.0;
        EngineHost.Instance.AdvanceMonth();
    }

    public void SetSpeed(Speed speed)
    {
        if (Current == speed)
            return;

        Current = speed;
        _elapsed = 0.0;
        EmitSignal(SignalName.SpeedChanged, (int)speed);
    }

    public void TogglePause() =>
        SetSpeed(Current == Speed.Paused ? Speed.Normal : Speed.Paused);

    /// <summary>Advance one month now, whatever the speed. Pauses first, so the
    /// player who asked for one month gets exactly one.</summary>
    public TickResult StepOneMonth()
    {
        SetSpeed(Speed.Paused);
        _elapsed = 0.0;

        return EngineHost.Instance.AdvanceMonth();
    }
}
