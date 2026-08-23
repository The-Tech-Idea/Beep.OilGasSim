#nullable enable

using System;
using System.Collections.Generic;
using Godot;
using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;

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

    /// <summary>
    /// The draft this run was created from, kept so the host can build the same
    /// ground it previewed. The engine has already consumed it; this is the
    /// host's copy, and reading it is not reading engine state.
    /// </summary>
    public NewGameDraft Draft { get; private set; } = NewGameDraft.Default(1UL);

    /// <summary>
    /// The structures this run has ordered a hole into. Host memory of its own
    /// commands, not a second copy of engine state — see <see cref="DrilledSites"/>
    /// for why the read model cannot answer this.
    /// </summary>
    public DrilledSites Drilled { get; } = new();

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
    /// <summary>
    /// The world knobs a new game is created from.
    /// </summary>
    /// <remarks>
    /// A client-side draft (GAME-SDD-001 §7A.7) whose fields map one-to-one onto
    /// the engine's <c>WorldParameters</c>. It is not a new engine contract, and
    /// the engine still refuses anything illegal by name rather than the client
    /// clamping it quietly (§7A.3).
    /// </remarks>
    public sealed record NewGameDraft(
        ulong Seed,
        string RealityProfile,
        string WorldTemplate,
        int Cells,
        double LandFraction,
        double ResourceRichness,
        double BasinMaturity,
        double ClimateSeverity,
        int RivalCount,
        Era StartEra)
    {
        /// <summary>
        /// What the player calls their company. Presentation, and only that: no
        /// engine contract carries a company name, so it is shown on the HUD and
        /// in the run history and nothing reads it as state.
        /// </summary>
        public string CompanyName { get; init; } = "Beep Energy Co.";

        /// <summary>
        /// The calendar year the run opens in — real, and the engine's, because
        /// <c>EngineSettings.Epoch</c> is host-supplied. Months are 30/360
        /// inside the engine; only the label is a real date.
        /// </summary>
        public int StartYear { get; init; } = 1965;

        public static NewGameDraft Default(ulong seed) => new(
            seed, "arcade", "world-template-basin",
            Cells: 24,
            LandFraction: 0.6,
            ResourceRichness: 1.0,
            BasinMaturity: 0.5,
            ClimateSeverity: 0.5,
            RivalCount: 3,
            StartEra: Era.E1);
    }

    public bool NewGame(NewGameDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        _startupProblems.Clear();
        Drilled.Clear();
        Draft = draft;
        Seed = draft.Seed;
        BasinKilometres = draft.Cells;
        Halted = false;
        Snapshot = null;
        _engine = null;

        GodotContentSource content = GodotContentSource.Shipped();
        GD.Print($"[content] {content.Count} files read from res://content");

        if (content.Count == 0)
        {
            _startupProblems.Add(
                "no content was found under res://content — the engine will not start without it");

            GD.PushError($"[engine] {_startupProblems[0]}");

            return false;
        }

        var settings = new EngineSettings(
            Epoch: new GameDate(draft.StartYear, 1),
            WorldSeed: draft.Seed,
            Retention: new AuditRetention(DetailWindowTicks: 120),
            LogSink: new GodotLogSink(),
            MinimumLogLevel: LogLevel.Warning,
            FaultHandling: FaultHandling.Resilient,
            RealityProfile: new ContentId(draft.RealityProfile),

            // The host reads the files; the engine reads no disk (SDD-004 §7).
            // Content that will not load is a refusal to start, not a warning.
            Content: [content],

            // BARE GROUND (plans 22 §4, S2). A yard, a licence and a bank
            // balance — the processing train is something the company builds,
            // and until it does, a well has nowhere to flow. This is the line
            // that stops the game handing a player a refinery on day one.
            StartingState: StartingStates.BareGround,

            // FRONTIER RULES (plans 23). A company out on ground nobody has
            // worked may survey and drill before it holds a reservoir — which
            // an operator may not, and correctly so. This is the line that used
            // to be a deleted check in the engine.
            Rules: RuleSets.Frontier.Id);

        var world = new WorldParameters(
            new ContentId(draft.WorldTemplate),
            WidthCells: draft.Cells,
            HeightCells: draft.Cells,
            LandFraction: draft.LandFraction,
            ResourceRichness: draft.ResourceRichness,
            BasinMaturity: draft.BasinMaturity,
            ClimateSeverity: draft.ClimateSeverity,
            RivalCount: draft.RivalCount,
            StartEra: draft.StartEra);

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
    /// <summary>
    /// Write the running game to a slot.
    /// </summary>
    /// <remarks>
    /// The engine writes its own payload into a stream the host opened, and the
    /// host writes the draft beside it. Both or neither: a payload with no
    /// sidecar would reload under ground the player never saw, and a sidecar with
    /// no payload is a slot that cannot be opened, so the sidecar goes second and
    /// a failed payload stops before it.
    /// </remarks>
    /// <summary>
    /// The yard's own state, for the sidecar.
    /// </summary>
    /// <remarks>
    /// Set by the gameplay screen while a world is up. The engine saves the
    /// ACTIVITY, which is its state; this is which unit was carrying it and
    /// where the unit had got to, which is the client's. Neither is a copy of
    /// the other.
    /// </remarks>
    public System.Func<string>? PackYard { get; set; }

    /// <summary>What the last opened save said the yard looked like.</summary>
    public string RestoredYard { get; private set; } = string.Empty;

    public bool Save(out string problem)
    {
        problem = string.Empty;

        if (_engine is null || Snapshot is null)
        {
            problem = "there is no running game to save";

            return false;
        }

        string name = SaveSlots.NameFor(Snapshot.Tick.Value);
        string path = SaveSlots.SavePathOf(name);

        DirAccess.MakeDirRecursiveAbsolute("user://saves");

        try
        {
            // Qualified, not imported: System.IO.FileAccess and Godot.FileAccess
            // are both in scope by name, and this project disables implicit
            // usings precisely so that collision has to be resolved on purpose.
            using var payload = new System.IO.MemoryStream();
            SaveGame.Write(_engine, Seed, payload);

            using FileAccess? handle = FileAccess.Open(path, FileAccess.ModeFlags.Write);

            if (handle is null)
            {
                problem = $"cannot write {path}: {FileAccess.GetOpenError()}";
                GD.PushError($"[saves] {problem}");

                return false;
            }

            handle.StoreBuffer(payload.ToArray());
        }
        catch (SaveDataFault fault)
        {
            // The engine refused to serialise itself. That is a fault the player
            // is entitled to see by name rather than a save that silently did not
            // happen (L4: no failure is discarded).
            problem = $"the engine would not write a save: {fault.Message}";
            GD.PushError($"[saves] {problem}");

            return false;
        }

        if (SaveSlots.WriteSidecar(
                name,
                Draft,
                PackYard?.Invoke() ?? string.Empty,
                Snapshot.Tick.Value,
                Snapshot.Cash.Cents / 100.0,
                Snapshot.Wells))
        {
            GD.Print($"[saves] wrote {name}");

            return true;
        }

        problem = "the engine payload was written but the world settings beside it were not";

        return false;
    }

    /// <summary>
    /// Open a saved game.
    /// </summary>
    /// <remarks>
    /// <para>The seed and the epoch are the save's, not this call's — the engine
    /// takes them from the header and overrides whatever settings say, because a
    /// host supplying either for a game it has not opened would be guessing.
    /// Everything else in the settings is a host choice being made afresh: where
    /// the log goes, how much audit to keep, whether faults halt.</para>
    ///
    /// <para>The container is validated first and its reasons reported, which is
    /// what <c>Read</c> exists for; loading a container that did not validate
    /// throws rather than returning, so asking first is the difference between a
    /// message and a crash.</para>
    /// </remarks>
    public bool Load(SaveSlots.Slot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);

        _startupProblems.Clear();

        // A loaded run has its own drilling history and this process does not
        // know it: the save carries engine state, and which structures were
        // drilled is not among what the read model publishes. Starting empty is
        // the honest position — the picker will offer a structure that has
        // already been drilled at worst, which is a legal order.
        Drilled.Clear();
        Halted = false;
        Snapshot = null;
        _engine = null;

        byte[] bytes = FileAccess.GetFileAsBytes(slot.SavePath);

        if (bytes.Length == 0)
        {
            _startupProblems.Add($"cannot read {slot.SavePath}: {FileAccess.GetOpenError()}");
            GD.PushError($"[saves] {_startupProblems[0]}");

            return false;
        }

        GodotContentSource content = GodotContentSource.Shipped();

        if (content.Count == 0)
        {
            _startupProblems.Add("no content was found under res://content — a save cannot open without it");

            return false;
        }

        var settings = new EngineSettings(
            Epoch: new GameDate(slot.Draft.StartYear, 1),
            WorldSeed: slot.Draft.Seed,
            Retention: new AuditRetention(DetailWindowTicks: 120),
            LogSink: new GodotLogSink(),
            MinimumLogLevel: LogLevel.Warning,
            FaultHandling: FaultHandling.Resilient,
            RealityProfile: new ContentId(slot.Draft.RealityProfile),
            Content: [content],

            // BARE GROUND, as a new game opens (plans 22 §4). Composition builds
            // no plant either way here — the save restores whatever the company
            // had actually built by the month it was written.
            StartingState: StartingStates.BareGround,

            // FRONTIER RULES (plans 23). A company out on ground nobody has
            // worked may survey and drill before it holds a reservoir — which
            // an operator may not, and correctly so. This is the line that used
            // to be a deleted check in the engine.
            Rules: RuleSets.Frontier.Id);

        using var source = new System.IO.MemoryStream(bytes, writable: false);

        if (SaveGame.Read(source) is Refused refused)
        {
            for (int i = 0; i < refused.Reasons.Count; i++)
                _startupProblems.Add(refused.Reasons[i]);

            for (int i = 0; i < _startupProblems.Count; i++)
                GD.PushError($"[saves] {_startupProblems[i]}");

            return false;
        }

        source.Position = 0;

        BuildResult result = SaveGame.Load(source, settings);

        if (result is not Built built)
        {
            RecordRefusal(result);

            return false;
        }

        Draft = slot.Draft;
        RestoredYard = slot.Yard;
        Seed = slot.Draft.Seed;
        BasinKilometres = slot.Draft.Cells;
        _engine = built.Engine;

        // ONE TICK, AND WHY. The read model is published by the Close stage, so a
        // freshly restored engine has none — the state is all there and nothing
        // has yet projected it. There is no "project without advancing" on the
        // engine surface, and the tests that exercise a reload advance a tick for
        // the same reason. So a loaded game resumes on the month AFTER the one it
        // was saved on, which is the month that would have come next anyway; what
        // is lost is the chance to look at the saved month before playing it.
        // A projection callable at tick zero would remove this, and that is an
        // engine change rather than something a host can work around.
        if (AdvanceMonth() is not TickCompleted || Snapshot is null)
        {
            _startupProblems.Add(
                "the save opened but its first month would not run; the state is loaded and unplayable");

            GD.PushError($"[saves] {_startupProblems[^1]}");
            _engine = null;

            return false;
        }

        GD.Print($"[saves] opened {slot.Name}, resuming at month {Snapshot.Tick.Value}");

        return true;
    }

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
        if (result is BuildRefusedByContent content)
        {
            for (int i = 0; i < content.Failures.Count; i++)
            {
                LoadFailure failure = content.Failures[i];

                _startupProblems.Add(
                    $"{failure.Source}/{failure.File} {failure.JsonPath} [{failure.Stage}] {failure.Message}");
            }
        }

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
