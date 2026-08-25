#nullable enable

using System;
using Godot;

namespace OilfieldDays.World;

/// <summary>Where a unit is in the life of a job.</summary>
public enum UnitState
{
    /// <summary>Standing in the yard with nothing to do.</summary>
    Idle = 0,

    /// <summary>On its way out, with a job it has not started.</summary>
    Travelling = 1,

    /// <summary>At the site. The command has been submitted and is the engine's.</summary>
    Working = 2,

    /// <summary>Job finished or refused; on its way home.</summary>
    Returning = 3,

    /// <summary>At the site, clearing and preparing the ground before work starts.</summary>
    Preparing = 4,
}

/// <summary>
/// A crew or a vehicle: the thing that carries work out to the field.
///
/// <para><b>The state machine is the point.</b> Plans 17 turns on one rule — the
/// engine command is submitted when the unit ARRIVES, not when the button is
/// pressed — and a lifecycle spread across booleans has no single place to put
/// it. There is exactly one transition that raises <see cref="Arrived"/>, and
/// exactly one listener that submits. A second path to submitting is the defect
/// this shape exists to make impossible.</para>
///
/// <para>Behaviour is here, kind is data (plans 21 §P2). Subclasses differ in how
/// they move and are drawn; which unit it is comes from <see cref="Kind"/>.</para>
/// </summary>
public abstract partial class Unit : Node2D
{
    /// <summary>How near counts as arrived, in pixels.</summary>
    private const float Touching = 12.0f;

    private Vector2 _home;
    private Vector2 _target;

    [Signal]
    public delegate void ArrivedEventHandler(Unit unit);

    [Signal]
    public delegate void PreparedEventHandler(Unit unit);

    [Signal]
    public delegate void HomeEventHandler(Unit unit);

    public UnitKind Kind { get; private set; } = null!;

    public UnitState State { get; private set; } = UnitState.Idle;

    /// <summary>What it will do on arrival. Meaningless while idle.</summary>
    public JobKind Job { get; private set; } = JobKind.None;

    /// <summary>What the job is aimed at — a prospect, a well, a chain element.</summary>
    public ulong Subject { get; private set; }

    /// <summary>The month the engine accepted the work, once it is working.</summary>
    public int StartedOn { get; private set; }

    private float _prepareLeft;

    public bool IsIdle => State == UnitState.Idle;

    /// <summary>Put it in the yard and give it its kind.</summary>
    public void Station(UnitKind kind, Vector2 home)
    {
        ArgumentNullException.ThrowIfNull(kind);

        Kind = kind;
        _home = home;
        Position = home;
        AddToGroup("units");
        Dress(kind);
    }

    /// <summary>
    /// Send it out. Nothing is submitted here — that is what arrival is for.
    /// </summary>
    public void SendTo(Vector2 site, JobKind job, ulong subject)
    {
        _target = site;
        Job = job;
        Subject = subject;
        State = UnitState.Travelling;
        Show(moving: true);
    }

    /// <summary>Called by the dispatcher once the engine has taken the work.</summary>
    public void Settle(int month)
    {
        StartedOn = month;
        State = UnitState.Working;
        Show(moving: false);
    }

    /// <summary>Start the visible site-prep phase before the command is submitted.</summary>
    public void Prepare(float seconds)
    {
        _prepareLeft = Mathf.Max(0.0f, seconds);
        State = UnitState.Preparing;
        Show(moving: false);

        if (_prepareLeft <= 0.0f)
            EmitSignal(SignalName.Prepared, this);
    }

    /// <summary>Send it home — job done, refused, or recalled before it arrived.</summary>
    public void GoHome()
    {
        Job = JobKind.None;
        Subject = 0;
        State = UnitState.Returning;
        _target = _home;
        Show(moving: true);
    }

    /// <summary>
    /// Put a unit back where a save left it.
    /// </summary>
    /// <remarks>
    /// A unit that was TRAVELLING when the game was saved is stood down to idle
    /// at the yard rather than resumed. Resuming would submit its command on
    /// arrival — a command the player last saw as "on its way" in a session that
    /// has since been reloaded — and a save that quietly commits work after
    /// loading is the worst kind of surprise. What was already the engine's
    /// survives, because the engine saved it.
    /// </remarks>
    public void Restore(UnitState state, JobKind job, ulong subject, int startedOn, Vector2 at)
    {
        if (state is UnitState.Travelling or UnitState.Preparing)
        {
            State = UnitState.Idle;
            Position = _home;
            Show(moving: false);

            return;
        }

        State = state;
        Job = job;
        Subject = subject;
        StartedOn = startedOn;
        Position = state == UnitState.Idle ? _home : at;
        _target = state == UnitState.Returning ? _home : at;
        Show(moving: state == UnitState.Returning);
    }

    /// <summary>Bring it back with nothing submitted. Only legal before arrival.</summary>
    public bool Recall()
    {
        if (State != UnitState.Travelling)
            return false;

        GoHome();

        return true;
    }

    public override void _Process(double delta)
    {
        // Paused means paused, for preparation and travel both: a yard that
        // keeps moving while the clock is stopped would let a player queue
        // arrivals — or finish site preparation — for free.
        float pace = Host.SimulationController.Instance.Multiplier;

        if (State == UnitState.Preparing)
        {
            if (pace <= 0.0f)
                return;

            _prepareLeft -= pace * (float)delta;

            if (_prepareLeft <= 0.0f)
                EmitSignal(SignalName.Prepared, this);

            return;
        }

        if (State is UnitState.Idle or UnitState.Working)
            return;

        if (pace <= 0.0f)
            return;

        Vector2 step = _target - Position;

        if (step.Length() <= Touching)
        {
            Land();

            return;
        }

        Vector2 by = step.Normalized() * Kind.Speed * pace * (float)delta;
        Position += by;
        Face(by);
    }

    private void Land()
    {
        Position = _target;

        if (State == UnitState.Travelling)
        {
            // THE one transition. Whoever listens submits the command; the unit
            // does not know what a command is (plans 21 §P8).
            EmitSignal(SignalName.Arrived, this);

            return;
        }

        State = UnitState.Idle;
        Show(moving: false);
        EmitSignal(SignalName.Home, this);
    }

    /// <summary>Build the art for a kind. Subclasses decide what that means.</summary>
    protected abstract void Dress(UnitKind kind);

    /// <summary>Switch between the still and the strip.</summary>
    protected abstract void Show(bool moving);

    /// <summary>Point it the way it is going, if it has a facing.</summary>
    protected abstract void Face(Vector2 by);
}
