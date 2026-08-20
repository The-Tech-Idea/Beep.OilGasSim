#nullable enable

using System;
using Godot;
using OGSim.Composition;
using OilfieldDays.Host;

namespace OilfieldDays.World;

/// <summary>
/// Policies a player can hold without clicking them every month.
///
/// <para>The measurement harness encodes a policy a human would want — repair
/// what has stopped, then debottleneck what is jamming, then explore — and a
/// player should be able to hold it rather than re-issue it thirty times a
/// decade.</para>
///
/// <para><b>This is not the client playing the game.</b> A standing order chooses
/// WHEN to send a unit on a job the player could have sent it on manually. It
/// computes no outcome, every decision it makes is one the read model already
/// showed, and every command it causes goes out with a crew and is subject to the
/// same refusal. It is the same power as travel — pacing input — held for
/// longer (plans 19 §D2).</para>
///
/// <para>Each is off by default, and the HUD says which are on. An automation a
/// player cannot see is one they will blame the engine for.</para>
/// </summary>
public sealed partial class StandingOrders : Node
{
    /// <summary>How many months a jam must persist before it is worth building for.</summary>
    private const int Patience = 3;

    /// <summary>Cash kept back rather than spending to the floor, in dollars.</summary>
    private const double Reserve = 12.0e6;

    private Dispatcher _yard = null!;
    private BasinWorld _world = null!;
    private DrilledSites _drilled = null!;

    private string _jammed = string.Empty;
    private int _jammedFor;
    private int _lastTick = -1;

    /// <summary>Send a maintenance crew the moment something stops.</summary>
    public bool KeepRunning { get; set; }

    /// <summary>Build the thing that answers a jam, once it has lasted.</summary>
    public bool AnswerJams { get; set; }

    /// <summary>Drill the best undrilled structure whenever the rig is idle.</summary>
    public bool KeepRigBusy { get; set; }

    public bool AnyOn => KeepRunning || AnswerJams || KeepRigBusy;

    public void Serve(Dispatcher yard, BasinWorld world, DrilledSites drilled)
    {
        _yard = yard;
        _world = world;
        _drilled = drilled;
    }

    /// <summary>Act, at most once a month, in the order a player would.</summary>
    public void Consider(FieldReadModel snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Once a tick. Bind runs every frame, and an order that fired per frame
        // would empty the yard in a second.
        if (snapshot.Tick.Value == _lastTick || !AnyOn)
            return;

        _lastTick = snapshot.Tick.Value;
        Track(snapshot);

        if (snapshot.Insolvent)
            return;

        // Stopped first, then jammed, then exploration — the order the harness
        // measured, and the order a player gives for the same reason: a failure
        // shuts in everything behind it, so a month spent on it is never wasted.
        if (KeepRunning && Repair(snapshot))
            return;

        if (AnswerJams && Rich(snapshot) && Debottleneck())
            return;

        if (KeepRigBusy && Rich(snapshot))
            Explore(snapshot);
    }

    private static bool Rich(FieldReadModel snapshot) => snapshot.Cash.Cents / 100.0 > Reserve;

    private void Track(FieldReadModel snapshot)
    {
        string jam = snapshot.Bottlenecks.Count > 0 ? snapshot.Bottlenecks[0].DisplayId : string.Empty;

        if (jam == _jammed)
        {
            _jammedFor++;

            return;
        }

        _jammed = jam;
        _jammedFor = jam.Length == 0 ? 0 : 1;
    }

    private bool Repair(FieldReadModel snapshot)
    {
        for (int i = 0; i < snapshot.Chain.Count; i++)
        {
            ChainElementView element = snapshot.Chain[i];

            if (!element.Failed)
                continue;

            if (_world.WhereIs(element.Element.Value) is not Vector2 at)
                continue;

            return _yard.Send(JobKind.Repair, at, element.Element.Value);
        }

        return false;
    }

    /// <summary>
    /// Build the thing that answers the jam, once it has lasted long enough.
    /// </summary>
    /// <remarks>
    /// Patience matters: a chain reports a bottleneck the month a well comes on
    /// and again while a crew is walking to it, and building for every one of
    /// those would spend the company on plant it did not need.
    /// </remarks>
    private bool Debottleneck()
    {
        if (_jammed.Length == 0 || _jammedFor < Patience)
            return false;

        for (int i = 0; i < _yard.Catalogue.Count; i++)
        {
            if (!_jammed.Contains(_yard.Catalogue[i].ChainMatch, StringComparison.Ordinal))
                continue;

            _jammedFor = 0;

            return _yard.Send(JobKind.Build, _world.PlantSite, (ulong)i);
        }

        return false;
    }

    private bool Explore(FieldReadModel snapshot)
    {
        if (snapshot.ActivitiesRunning > 0)
            return false;

        ProspectView? target = _drilled.BestUndrilled(snapshot);

        if (target is not ProspectView aim)
            return false;

        _drilled.Record(aim);

        return _yard.Send(JobKind.Drill, BasinWorld.ToWorld(aim.At), aim.Prospect.Value);
    }
}
