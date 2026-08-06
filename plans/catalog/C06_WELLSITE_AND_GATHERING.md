# C06 — Wellsite & Gathering

**Catalogue sheet** · chain position: wellhead → processing ([04](../design/04_MATERIAL_AND_FLOW.md) stage 3) · phases: R8
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Cheap steel with expensive consequences — shared-line backpressure lives here.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Choke | fixed bean → adjustable → remote | remote: Telemetry / SCADA | E1→E3 | $ | within completion / retrofit | Critical-flow control; remote = re-choke without a site visit |
| Flowline | size ladder 3–8 in | — | E1 | $/km | construction · weeks | Backpressure vs cost; erosional velocity margin |
| Manifold / header | fixed → test-header | — | E1→E2 | $$ | construction · weeks | Commingling; per-well test routing (allocation quality) |
| Test separator | portable | — | E1 | $$ | — | Per-well rates without shutting others in |
| Pig launchers/receivers | one | — | E2 | $ | with flowline | Enables pigging ([C11](C11_PIPELINES_AND_STATIONS.md)); wax management |

**Couplings & notes**
- A new high-pressure well on a shared line **kills weak wells** (R6-V14) — the manifold sheet is where the player sees why.
- Chokes are the player-policy layer over pro-rata throttling ([SDD-002](../sdd/SDD-002_STREAMS_AND_FLOW.md) §7).
