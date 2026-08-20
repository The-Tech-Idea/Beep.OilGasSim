# Godot host integration

This document is the bridge plan for connecting `src/OGSim.Game` to the .NET
engine without changing engine code.

## 1. The current mismatch

`src/OGSim.Game/OilandGasSim.csproj` targets `net8.0` under the Godot .NET SDK.
The engine projects target `net10.0` through `Directory.Build.props`. A direct
`ProjectReference` from the current Godot project is therefore not the safe
default until the target frameworks are made compatible.

Do not edit the engine to work around this. Instead, choose one of the host
patterns below.

## 2. Recommended pattern: out-of-process engine host

The cleanest current approach is to keep the engine in its own .NET process and
let Godot talk to it over a small JSON protocol.

Suggested boundaries:

```text
Godot UI/Scene layer
        |
        v
Godot EngineClient (JSON over stdout/stdin, named pipe, HTTP, or WebSocket)
        |
        v
OGSim.Host.Bridge (net10.0, references OGSim.Composition)
        |
        v
Headless engine
```

The bridge process owns one `Engine` and translates messages:

```text
new_game / load_game
get_snapshot
submit_command
advance_tick
get_events
query_audit
save_game
```

Godot never references domain assemblies. It only sees stable DTOs.

## 3. Alternative: direct in-process reference

If the Godot runtime and project target are later updated to allow `net10.0`,
the game can reference `OGSim.Composition` directly. Keep a thin `EngineHost`
autoload so no scene script talks to the engine directly.

Do not spread `Engine` access through scene scripts. One owner:

- builds/loads the engine;
- exposes read-only snapshots to the rest of the game;
- submits commands;
- advances ticks;
- emits Godot signals for snapshot/events/results.

## 4. Concrete current engine API

The currently playable concrete surface is in `OGSim.Composition`.

Build:

```csharp
EngineSettings settings = new(
    Epoch: new GameDate(1965, 1),
    WorldSeed: seed,
    Retention: new AuditRetention(DetailWindowTicks: 120), // choose the host policy
    LogSink: myLogSink,
    MinimumLogLevel: LogLevel.Info,
    FaultHandling: FaultHandling.Resilient,
    RealityProfile: new ContentId("simulation"));

BuildResult result = EngineBuilder.Build(settings);
// or EngineBuilder.CreateNew(settings, worldParameters)
```

If `result` is `Built built`, use `built.Engine`.

The concrete `Engine` exposes:

- `Pipeline` — `AdvanceTick()`;
- `Commands` — `Submit(Command)`;
- `ReadModel` — current `FieldReadModel?`;
- `Audit` — audit trail;
- `Events` — event bus;
- `State` — state registry;
- `Provided` — resolved contracts.

## 5. Current host loop

```csharp
FieldReadModel? previous = engine.ReadModel;

if (previous is not null)
{
    DecideCommandsFromSnapshot(previous);
}

TickResult result = engine.Pipeline.AdvanceTick();

if (result is TickCompleted)
{
    FieldReadModel snapshot = engine.ReadModel
        ?? throw new Exception("Read model missing after a completed tick");

    EmitSnapshotChanged(snapshot);
}
else if (result is TickAbandoned abandoned)
{
    ShowFault(abandoned.Fault);
}
else if (result is TickHalted halted)
{
    ShowFatalFault(halted.Fault);
}
```

The read model before the first tick is `null`. Do not fabricate a zeroed
starting snapshot.

## 6. Command usage

Submit commands only between ticks.

Example:

```csharp
CommandResult drillResult = engine.Commands.Submit(
    new DrillWellCommand(
        Target: new EntityId<IProspect>(prospectId),
        TotalDepth: new Length(2000.0)));

switch (drillResult)
{
    case Accepted accepted:
        ShowAcceptedFeedback(accepted);
        break;
    case Rejected rejected:
        foreach (RejectionReason reason in rejected.Reasons)
            ShowLocalisedReason(reason.LocId, reason.Detail);
        break;
}
```

`Rejected` is normal game feedback, not an exception. It means the simulation
correctly said no.

The rig/scheduler is exclusive. Submit one drilling command at a time and read
`FieldReadModel.ActivitiesRunning` before scheduling another.

## 7. Snapshot-to-Godot mapping

| `FieldReadModel` field | Godot representation |
|---|---|
| `Date`, `Tick` | Calendar UI and turn counter |
| `Cash` | Company dashboard currency |
| `Wells` | Counter in company/summary UI |
| `ActivitiesRunning` | Active operations badge |
| `ProducedThisTick` | Production chart / summary |
| `Insolvent`, `Outcome` | End-of-run state and victory/failure screen |
| `Beliefs` | Belief inspector, uncertainty ranges |
| `Chain` and `Bottlenecks` | Production flow diagram with throughput/deferral labels |
| `Wellbores` | Well list and selection for well commands |
| `Prospects` | Map markers and prospect cards |

The engine returns domain value types such as `Money`, `Length`, and
`SurfaceVolume`. Convert these to display values only in Godot presentation
code.

## 8. Events and audit

After a completed tick:

```csharp
IReadOnlyList<EngineEvent> events = engine.Events.Sealed(engine.Pipeline.CurrentTick);
```

Use events to build a message log or toast queue. Keep event history in the
game UI only; the engine intentionally evicts old events and keeps audit history.

Audit queries are available through `engine.Audit`. Use them for detailed
diagnostics screens, not the main read model.

## 9. Saving and loading

Current persistence primitives exist (`SaveFile`, `StateBlock`, `CanonicalJson`,
`StateRegistry`). The host should own file paths and streams.

Recommended host responsibilities:

- enumerate save slots;
- write/read file bytes;
- store metadata such as display name, timestamp, and seed outside engine state;
- never mutate engine state while saving.

The contract-level `IEngine.WriteSave(Stream)` is the target when the concrete
engine is wired to the full public surface.

## 10. Pacing and pause

Godot controls pacing. The engine is synchronous and has no timers.

Recommended autoload:

```text
SimulationController
    enum Speed { Paused, Normal, Fast, VeryFast }
    float secondsPerTick
    _process(delta): accumulate and call AdvanceTick when due
```

At `Paused`, do not advance. At faster speeds, advance multiple ticks only if
the UI can absorb the resulting snapshot stream.

## 11. Threading rule

Keep the engine on one thread. If using the out-of-process bridge, keep message
handling single-threaded per engine instance. Do not call engine methods from
multiple Godot threads.

The engine is deterministic and does not use wall-clock time. Godot must not
pass wall-clock time or its own `Random` into engine state.

## 12. Error handling

Do not catch and hide engine faults. Distinguish:

- `Rejected` command: show reason, continue;
- `TickAbandoned`: show fault, retain previous snapshot, allow next tick;
- `TickHalted`: stop, offer save/report/debug;
- composition `BuildRefused`: show every problem and do not start the game.

The engine uses exceptions for invariant faults and configuration defects. The
Godot host should not recover from them silently.
