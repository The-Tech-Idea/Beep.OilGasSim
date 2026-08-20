# 21 — Game code patterns

**How the client is built, in Godot's own idiom.**

The engine has laws and a house style; the client has had neither, and it shows
— screens that hand-roll their own chrome, unit types that would be a `switch`,
and content that would be C# constants. This document is the client's equivalent
of SDD-000: the patterns everything in `game/` is expected to follow, and the
reasons, so a departure is a decision rather than an accident.

It is referenced by [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md) and binds
the stage documents.

---

## P1. Data is a `Resource`, not a constant

**Anything a designer would want to change is a `.tres` file.**

Unit kinds, job definitions, yard building roles, camera steps, art sets — these
are data. In Godot, data is a `Resource` subclass with `[Export]` fields, saved
as a `.tres`, edited in the inspector, and loaded by path.

```csharp
[GlobalClass]
public partial class UnitKind : Resource
{
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public Texture2D? Portrait { get; set; }
    [Export] public SpriteFrames? Frames { get; set; }
    [Export] public float MetresPerSecond { get; set; } = 900.0f;
    [Export] public JobKind Carries { get; set; }
    [Export] public string YardStand { get; set; } = string.Empty;
}
```

`[GlobalClass]` is what makes it appear in the editor's *New Resource* list and
in an `[Export]` picker by name. Without it the type exists only to code, which
defeats the point.

**Why this and not a C# table.** The engine learned this the hard way and wrote
it down: its facility ladders were C# constants while a content pipeline sat
beside them fully built and bypassed, so a rebalance meant a recompile. The same
mistake is available here and costs the same. A new unit kind should be a `.tres`
and an art set — not a recompile, and never a new `case`.

**Where it stops.** A `Resource` holds *presentation and pacing*: what a unit
looks like, how fast it drives, which job it carries. It never holds a cost, a
duration, a probability or a yield — those are the engine's, and a designer
editing a `.tres` must not be able to change what the simulation does.

## P2. Behaviour is a node hierarchy; kind is data

Two axes, and confusing them is the classic mistake:

- **What a thing IS** — a vehicle, a crew — differs in *behaviour* and is a
  class.
- **Which one it is** — a wireline truck, a coring unit — differs in *data* and
  is a `Resource`.

```
Node2D
└── Unit                 abstract: state, subject, the dispatch lifecycle
    ├── VehicleUnit      drives roads, has facing and wheels that turn
    └── CrewUnit         walks, no facing beyond left/right
```

There is **no** `WirelineTruck : VehicleUnit`. A wireline truck is a
`VehicleUnit` holding the wireline `UnitKind`. The moment a subclass exists per
kind, adding a kind means writing a class, and the data-driven half is dead.

**Inherit for behaviour, compose for capability.** If a future unit needs to both
drive and carry a crew, that is a component node under it, not a third subclass —
Godot's node tree is the composition mechanism and it is better at it than C#
inheritance.

## P3. Scene inheritance for the art

`Unit.tscn` carries the shape every unit has — sprite, shadow, selection ring,
label. A kind's scene is an **inherited scene** of it (`Ctrl+Shift+S` → *New
Inherited Scene*), overriding only what differs.

The alternative — one scene per unit built from scratch — means a change to the
selection ring is a change in nine files, and the ninth gets missed. Inherited
scenes make the shared part shared.

Instantiation is by `PackedScene` held on the `UnitKind`, so spawning a unit is
`kind.Scene.Instantiate<Unit>()` and the roster is a list of resources.

## P4. State is a machine, and it is explicit

A unit's lifecycle — idle, travelling, working, returning — is a state machine.
It is written as one, not as a pile of booleans:

```csharp
public enum UnitState { Idle, Travelling, Working, Returning }
```

with one method per transition and every transition in one place. The reason is
Stage B's central rule: **the engine command is submitted on exactly one
transition** — `Travelling → Working`. A lifecycle spread across booleans has no
such single place, and the rule becomes a thing that is true today.

## P5. Signals out, method calls down

A unit does not know about the dispatch board and must not reach for it. It
raises `Arrived`, `Refused`, `Home`, and whoever cares connects.

```csharp
[Signal] public delegate void ArrivedEventHandler(Unit unit);
```

Downward is direct: the dispatcher tells a unit to `SendTo(...)`, because it owns
it. **Upward and sideways is a signal.** This is the same shape as the engine's
own rule that a host submits commands down and reads a model back rather than
subscribing to events, and it keeps the world from depending on the UI.

## P6. Groups for "all of a kind", not a static list

`AddToGroup("units")` and `GetTree().GetNodesInGroup("units")` replace the static
registry a client usually grows. The tree is already the registry; a second one
goes stale the first time a node is freed.

## P7. `_Ready` is not a constructor for what the owner set

A control positioned by its owner and then re-anchored in its own `_Ready`
discards what it was given. This cost this build a card that kept reappearing
under the side column. **A node configures what only it knows in `_Ready`, and
never re-applies a layout its owner already chose.**

## P8. The engine boundary is one class deep

`EngineHost` is the only thing that touches `OGSim.*` for state. Units, the
dispatcher, and the yard talk to it and to each other. Nothing in `game/World/`
or `game/Ui/` builds a command out of thin air and reaches past it.

This is already true and is written down because Stage B is exactly the change
that would break it: a unit that submitted its own command would be a second
place that talks to the engine, and the first thing to go wrong would be two of
them submitting at once.

## P9. What is already right, and stays

- **Chrome is one file.** `SlateChrome` over `KitTheme`; no screen builds its own
  stylebox. Nine-patch geometry lives in three named plates.
- **Deterministic art.** Scatter and layout are hashed from the seed, never
  `Random`, so a basin is dressed the same way every time it is drawn.
- **Dev tools are flags, not builds.** `--audit`, `--play`, `--slice`,
  `--shot`; none of them run in a game.

---

## Acceptance for any new client code

- [ ] No `switch` on a unit kind, a job kind or a building role — the data
      carries the difference.
- [ ] No new subclass per kind.
- [ ] Anything a designer would tune is `[Export]` on a `Resource`.
- [ ] Nothing in a `Resource` changes a simulation outcome.
- [ ] Cross-tree communication is a signal.
- [ ] `OGSim.*` state is reached only through `EngineHost`.
