#nullable enable

using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OilfieldDays.Host;

/// <summary>
/// The one owner of the engine (plan 05 §1, plan 02 §3).
///
/// <para>Every number the game shows comes through here and every player intent
/// goes back through here as a <see cref="Command"/>. No scene script holds an
/// engine reference, and nothing in the game computes a simulation value for
/// itself — plan 09 §14: "the engine remains the source of truth for every
/// number shown".</para>
///
/// <para>The engine is referenced <b>in process</b>. Plan 02 §1 left that open
/// because the Godot project was net8.0 and the engine net10.0; the project now
/// targets net10.0 and Godot 4.7.1 loads it, which is the cheaper of the two
/// patterns that document offers.</para>
/// </summary>
public sealed partial class EngineHost : Node
{
    /// <summary>The autoload instance. Set in <see cref="_EnterTree"/>, before any scene runs.</summary>
    public static EngineHost Instance { get; private set; } = null!;

    private readonly List<string> _startupProblems = new();

    private OGSim.Composition.Engine? _engine;

    [Signal]
    public delegate void SnapshotChangedEventHandler();

    /// <summary>Raised when a tick could not be trusted — <c>TickAbandoned</c> or <c>TickHalted</c>.</summary>
    [Signal]
    public delegate void TickFaultedEventHandler(string detail, bool fatal);

    /// <summary>The month just closed, as the player is entitled to see it.</summary>
    public FieldReadModel? Snapshot { get; private set; }

    /// <summary>Whether an engine exists and its run has not ended.</summary>
    public bool Running => _engine is not null && !Halted && Snapshot is not null
        && Snapshot.Outcome == ObjectiveState.Pending && !Snapshot.Insolvent;

    /// <summary>Set when a tick reported untrustworthy state. The run cannot continue.</summary>
    public bool Halted { get; private set; }

    /// <summary>The seed this basin was generated from — a run is only comparable
    /// with another on the same one (plan 11 §5).</summary>
    public ulong Seed { get; private set; }

    /// <summary>How many kilometres across the generated basin is. The world is
    /// drawn to this, so the ground and the coordinates cannot disagree.</summary>
    public int BasinKilometres { get; private set; } = 16;

    /// <summary>Why composition or world generation refused, if it did.</summary>
    public IReadOnlyList<string> StartupProblems => _startupProblems;

    public override void _EnterTree() => Instance = this;

    /// <summary>
    /// Compose an engine, generate its world, and run the opening month.
    /// </summary>
    /// <remarks>
    /// The opening tick is not a formality: <c>Engine.ReadModel</c> is null until
    /// a tick has closed, because — in the engine's own words — "a game that has
    /// not started has nothing to show and a zeroed model would be a lie about a
    /// month that never happened".
    /// </remarks>
    public bool NewGame(ulong seed, string realityProfile, int basinCells)
    {
        _startupProblems.Clear();
        Seed = seed;
        BasinKilometres = basinCells;
        Halted = false;
        Snapshot = null;
        _engine = null;

        var settings = new EngineSettings(
            Epoch: new GameDate(1965, 1),
            WorldSeed: seed,
            Retention: new AuditRetention(DetailWindowTicks: 120),
            LogSink: new GodotLogSink(),
            MinimumLogLevel: LogLevel.Warning,
            FaultHandling: FaultHandling.Resilient,
            RealityProfile: new ContentId(realityProfile));

        var world = new WorldParameters(
            new ContentId("world-template-basin"),
            WidthCells: basinCells,
            HeightCells: basinCells,
            LandFraction: 0.6,
            ResourceRichness: 1.0,
            BasinMaturity: 0.5,
            ClimateSeverity: 0.5,
            RivalCount: 3,
            StartEra: Era.E1);

        BuildResult result = EngineBuilder.CreateNew(settings, world);

        if (result is not Built built)
        {
            // Composition is all-or-nothing and a refusal names every problem
            // (plan 00 §3). Showing one of them would hide the rest.
            RecordRefusal(result);
            return false;
        }

        _engine = built.Engine;

        return AdvanceMonth() is TickCompleted;
    }

    /// <summary>Submit one player intent. Both outcomes are normal feedback.</summary>
    public CommandResult Submit(Command command)
    {
        if (_engine is null)
            return new Rejected([new RejectionReason("host.noengine", "No game is running.")]);

        CommandResult result = _engine.Commands.Submit(command);

        // A command does not close a tick, so the read model has not changed —
        // but what a command CAN do is change what the player may do next, so
        // the screens are told to re-read.
        if (result is Accepted)
            EmitSignal(SignalName.SnapshotChanged);

        return result;
    }

    /// <summary>Advance exactly one month. The only place the clock moves.</summary>
    public TickResult AdvanceMonth()
    {
        if (_engine is null)
            return new TickHalted(new Fault(FaultClass.Host, "host.noengine", null, "No game is running."));

        TickResult result = _engine.Pipeline.AdvanceTick();

        switch (result)
        {
            case TickCompleted:
                Snapshot = _engine.ReadModel;
                EmitSignal(SignalName.SnapshotChanged);
                break;

            case TickAbandoned abandoned:
                // The month was discarded whole and the previous state stands
                // (plan 03 §2). The run continues.
                EmitSignal(SignalName.TickFaulted, Describe(abandoned.Fault), false);
                break;

            case TickHalted halted:
                Halted = true;
                EmitSignal(SignalName.TickFaulted, Describe(halted.Fault), true);
                break;
        }

        return result;
    }

    /// <summary>The events this tick sealed, for the feed. Only the latest tick is retained.</summary>
    public IReadOnlyList<EngineEvent> EventsThisTick()
    {
        if (_engine is null || Snapshot is null)
            return [];

        return _engine.Events.Sealed(Snapshot.Tick);
    }

    private void RecordRefusal(BuildResult result)
    {
        if (result is BuildRefused refused)
        {
            for (int i = 0; i < refused.Problems.Count; i++)
            {
                CompositionProblem problem = refused.Problems[i];
                _startupProblems.Add($"{problem.Kind} in {problem.Module}: {problem.Detail}");
            }
        }

        if (_startupProblems.Count == 0)
            _startupProblems.Add($"the engine refused to start: {result.GetType().Name}");

        for (int i = 0; i < _startupProblems.Count; i++)
            GD.PushError($"[engine] {_startupProblems[i]}");
    }

    private static string Describe(Fault fault) => $"{fault.Class} ({fault.Rule}): {fault.Detail}";

    /// <summary>
    /// The host's log sink. The engine writes typed records and only a sink
    /// renders them, which is why this is the host's job rather than the
    /// engine's (design 09 §3).
    /// </summary>
    private sealed class GodotLogSink : ILogSink
    {
        public void Emit(LogRecord record)
        {
            if (record.Level >= LogLevel.Error)
                GD.PushError($"[engine] {record.EventName}");
            else
                GD.Print($"[engine] {record.Level} {record.EventName}");
        }
    }
}
