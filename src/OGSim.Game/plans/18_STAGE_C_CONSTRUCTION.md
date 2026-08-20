# 18 — Stage C: construction

**Goal: building the plant is work someone does, not a menu entry.**

Six of the engine's commands add capacity — a separator, a manifold, a gas plant,
a treater, a tank, more export. Today each is a row on a board and the change
appears the instant it is accepted. After this stage a construction crew leaves
the yard, the unit rises on the plant site over the months the engine takes, and
the chain the world draws grows a piece.

Parent: [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md).
Built per [21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md).

---

## C1. What a build is

A build is a Stage B job whose subject is the plant instead of a structure:

1. commissioned at the office, from a catalogue of what can be added
2. the construction crew drives to the plant site
3. **on arrival the install command is submitted** — same rule, same reason
4. the engine runs its activity; the host draws a site under construction
5. when the chain view shows the new element, the scaffold becomes the unit

Step 5 is the important one. **The host does not decide when the thing is
finished** — it watches for the element to appear in `snapshot.Chain` and swaps
the art then. A build that finished on a host timer would be the host guessing at
an engine duration, and the two would drift apart the first time a fault
abandoned a tick.

## C2. Where it goes

**The player does not choose.** Gap G-02/G-14: OGSim has no coordinate for a
facility, so every separator is the same separator wherever it is drawn.

The host lays the plant out itself — it already does, in two rows at the plant
site — and a build takes **the next free bay** in that layout. The catalogue says
what will be added; the world shows where the host is putting it; nothing implies
the player had a say, because they did not.

This is the honest version of the build mockup. The tile grid and the placement
ghost in `gameplay_2` are a real screen for a real mechanic and that mechanic
does not exist yet. Drawing the ghost anyway would be the one thing
[15](15_GAMEPLAY_REDESIGN.md) §2d forbids.

## C3. The catalogue

Per P1, the catalogue is data — one `BuildKind` resource per addition:

```csharp
[GlobalClass]
public partial class BuildKind : Resource
{
    [Export] public string DisplayName { get; set; } = string.Empty;
    [Export] public Texture2D? Icon { get; set; }
    [Export] public PackedScene? Scene { get; set; }   // what rises on the bay
    [Export] public string ChainMatch { get; set; } = string.Empty; // element id fragment
    [Export] public string Explains { get; set; } = string.Empty;   // what it unblocks
}
```

`ChainMatch` is how step 5 knows the build landed: the element the engine adds
carries a display id, and the resource says which fragment to watch for. **No
`switch` on build kind anywhere** — adding the seventh addition is a `.tres`, an
icon and a scene.

What the resource must **not** carry: a price, a duration, or a capacity. All
three are the engine's, and a designer who could edit them here would be editing
the simulation from the client.

## C4. What it looks like

| State | Drawn as |
|---|---|
| Bay empty | flat gravel, a survey peg |
| Under construction | scaffold, a crane, the crew's vehicle parked, dust |
| Built | the unit's own art, joined to the chain the world already draws |

The chain the host draws between units is presentation — the engine's chain is an
ordered list with no geometry — and this is said out loud in the build log so
nobody later mistakes the pipe runs for a network the engine solves.

---

## Acceptance

- [ ] Every install command in the engine appears in the catalogue as a resource.
- [ ] Commissioning a build sends the crew; the command is submitted on arrival.
- [ ] The scaffold becomes the unit **when the element appears in the chain**,
      not on a host timer. Verified by forcing a tick fault mid-build: the art
      must not advance.
- [ ] No placement choice is offered anywhere.
- [ ] A seventh addition can be added with a `.tres`, an icon and a scene.
- [ ] `--play` over forty years produces the same cash, wells and production as
      before this stage.
