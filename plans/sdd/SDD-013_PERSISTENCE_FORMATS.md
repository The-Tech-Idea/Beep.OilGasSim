# SDD-013 — Persistence Formats

**Status:** drafted · **Serves:** R19 · **Design docs:** [11](../design/11_PERSISTENCE.md), [R19](../phases/R19_PERSISTENCE.md), [09](../design/09_DIAGNOSTICS.md) §4.4

The on-disk truth: the container, the canonical JSON rules that make PV1
byte-exact, the digest, and — consolidated from every other SDD — **the list of
derived state that must never be saved.**

---

## 1. Container (PSD1 (c))

```text
save.ogsim = ZIP (deflate):
  manifest.json          header (§2)
  state/<module>.json    one canonical block per registered module, module-name order
  audit/trail.jsonl      the retained trail (09 §4.4) — excluded from digest
```

> **R20d.12.22 amendment: ONE audit file, not two — and the reason is that
> R20d.12.21 removed the second.** This listed `audit/summary.json` (the
> retained trail) beside `audit/full.jsonl` (the complete one), which is only
> writable by an engine that KEEPS everything and summarises on the way out.
>
> The engine no longer keeps everything. `TickPipeline` prunes at every tick
> boundary (finding 207), so what it holds IS the retained trail and there is no
> complete one to write. Shipping both files would mean either keeping the full
> trail in memory for forty years — which is the unbounded growth 207 was about —
> or writing the same content twice under two names.
>
> **This is a consequence of a fix rather than a change of intent.** DGD1 (c)
> asks that the trail leave the engine in a form a player or a developer can
> read afterwards, and one JSONL file does that. What is lost is the ability to
> recover detail that retention has already discarded, and that was never
> recoverable from a save — it was gone from memory the tick it aged out.
>
> **Excluded from the digest**, as `full.jsonl` always was: the trail is
> diagnostic rather than simulation state, so a container whose trail was
> truncated is still a playable game and refusing it would be refusing over a
> record (§1b).

## 2. Header

`schemaVersion, engineVersion, contentVersion, activeMods[{id, version, order}],
worldSeed, epoch{year, month}, tick, rngPositions{stream: ulong × 8},
moduleDigests{name: sha256}, stateDigest`

— digest = SHA-256 over the canonical module blocks concatenated in module-name
order; per-module digests localise PV divergence (R19 risk note).

> **R20d.12.19 amendment (S013-6): the EPOCH joins the header.** A `Tick` is a
> count and a `GameDate` is a label; the clock turns one into the other using the
> epoch it was built with, and that epoch lived only in `EngineSettings`. So a
> save carried tick 240 and nothing about what month that was, and a host that
> loaded it with different settings got a game whose entire history was
> relabelled — every audit entry, every belief's as-of, every objective deadline
> shifted by the difference, with the simulation itself unchanged and no
> indication anything had moved.
>
> Harmless while exactly one scenario ships, which is why it has sat here as an
> open item rather than a finding. It stops being harmless at the SECOND
> scenario, and the failure is a bad one to debug because the numbers are all
> right — only the dates are wrong.
>
> **The epoch is the saved game's, not the loader's**, on the same principle as
> the seed one line above it: a host asked to supply the epoch of a game it has
> not opened yet would be guessing, and `Load` already overrides
> `settings.WorldSeed` from the header for exactly this reason.

## 1b. The trail is RESTORED, not archived — S013-4/finding 202 decided (R20d.12.20)

Both items were blocked on one question nobody had asked out loud: **is an
`AuditId` engine-local or save-durable?** S013-4 could not decide whether a
reloaded trail is an archive or state; finding 202 could not chain a deferral to
the failure behind it because an id does not survive a load. **They are the same
question and it is answered here once.**

**The trail is RESTORED, ids and all.** Design 09 §4.3 promises a player can ask
"why?" of the current state, and §4.4 promises that *nothing which explains the
current state is ever discarded* — a guarantee about the STATE, not about the
session. A chain that stops at the last reload cannot answer why a well is shut
in today if the failure that shut it happened before the player saved, which is
the same shape as finding 198: a company that reloads and has forgotten what it
paid to learn.

**The digest exclusion is not evidence for the other answer.** §1 leaves
`audit/full.jsonl` out of the digest because the trail is diagnostic rather than
simulation state: a container whose trail was truncated is still a playable
game, and refusing to load one would be refusing over a record. That is a
statement about INTEGRITY CHECKING, not about lifetime, and it was being read as
though it settled lifetime.

**What follows from it:**

```text
· AuditId is save-durable. Entries restore with their own ids, and the next-id
  counter resumes above the highest restored — so a Cause written before a save
  still resolves after one.
· The sidecar is written from the RETAINED trail (09 §4.4), not the raw one.
  Retention computes the cause closure that keeps a prunable entry alive when
  something durable depends on it; a save writing the unpruned trail would be a
  second retention policy.
    (There was no retained trail when this was first written: `Prune` was called
      by nothing and retention was configured and inert — finding 207. The
      pipeline prunes at the tick boundary since R20d.12.21, so the bound this
      line assumes now exists. Kept as a note because the assumption was made
      before it was true, which is how a spec comes to depend on machinery
      nobody had wired up.)
· PV1 and PV2 are untouched. The trail is outside the digest, so byte-identity
  is unaffected; it is outside the read model, so continuation identity is too.
```

**And it unblocks finding 202 without a route-law change**, which is worth
saying because that is what the finding currently names as its blocker: with
durable ids, a `ConstraintBinding` can cite a failure recorded in an earlier
tick, and only the WITHIN-tick link still needs stage 4 to hand stage 5 what it
learned. The route law still has to report *why* an element became unreachable
before the chain is complete — but the id problem, which looked like the harder
half, is a decision rather than a feature.

## 2b. The restore order is DECLARED — S013-5 (specified R20d.12.14, built R20d.12.15)

Capture walks owners in **state-key order**, and that is right for capture: it
makes the bytes independent of the order modules happened to compose, which is
what PV1 rests on. **Restore cannot use the same order**, because restoring is
not symmetric with capturing — an owner's `Restore` may need facts another owner
holds, and key order says nothing about which. Design 11 §2.1 asks for declared
dependencies, topologically sorted.

**It was three phases hand-written in `SaveGame.Restore`**, and that had already
failed once in a way nothing could catch: `world.decisions` sorts after
`wells.completions`, so the field rebuild measured each reopened well's gathering
line against a header that had not been restored and every tieback fell to its
floor (finding 201).

**And a second constraint existed only as a sentence in that loader's docstring**
— "obligations land in the third phase ON PURPOSE", because reopening a well
registers an abandonment obligation exactly as drilling one does and the save's
record has to win, or a company gets back an obligation it had discharged. Key
order disagrees. The first walk of the declared order faulted on it immediately,
which is the whole argument for the change demonstrated rather than asserted:
prose the loader happened to implement correctly became a claim a sort enforces,
and the one place it was wrong said so at once.

### 2b.1 What is declared

`IStateOwner` gains one member:

```csharp
/// The keys this owner must be restored AFTER. Empty for most owners,
/// and empty is a statement rather than an omission.
IReadOnlyList<StateKey> RestoreAfter { get; }
```

`StateRegistry` gains `RestoreOrder` beside `Owners`: the same set, sorted so
every owner follows everything it names. **Key order is the tie-break**, so the
result is total and deterministic (rule D-5) — two owners with no dependency
between them are ordered by key exactly as capture orders them, and the sort
adds an order only where a dependency states one.

A cycle is a **composition-time refusal** naming the whole cycle, not a
load-time fault. It is a fact about the module set, so it is knowable when the
set is validated, and `IModuleRegistry` already refuses a set it cannot build
rather than starting a degraded engine (design 03 §3.1). A key naming a
non-existent owner is refused the same way and for the same reason.

### 2b.2 The rebuild is not a step beside the owners — it is part of one

The loader has an action that is not an owner: rebuilding the field by reopening
every completion the save records. A naive ordering cannot place it, because it
is neither before nor after "the owners" as a group — it must follow the
subsurface and the world and precede the wells' own block.

**So it is not placed separately.** Rebuilding is what materialises the things
`wells.completions` describes, and design 11 §2.1's own wording makes it that
owner's loader. The order therefore stays purely over owners:

```text
wells.completions     RestoreAfter = [subsurface.compartments, world.decisions]
company.obligations   RestoreAfter = [wells.completions]
everything else       RestoreAfter = []        → key order among themselves
```

and restoring `wells.completions` means *rebuild, then restore* — one unit,
because the check the owner performs is a check on what the rebuild just did.
This is why the two halves of S013-5 "land together and neither is useful
alone": the rebuild needs the order to know when to run, and the order needs the
rebuild to have anything non-trivial to sequence.

**What this buys beyond tidiness** is that finding 201's correction stops being
a comment. `world.decisions` before `wells.completions` becomes a declaration
the sort enforces, so the next owner whose restore depends on another says so in
its own file and cannot be silently mis-ordered by an edit to the loader.

### 2b.3 Verification

**PV4b** — the declared order is a topological extension of key order: for any
two owners with no declared dependency, `RestoreOrder` agrees with `Owners`.
**PV4c** — a module set declaring a cycle, or a key nobody owns, is refused at
composition, naming every key involved and attributing each to the module that
declared it. Both are cheap and neither needs a save.

**A dangling key must not also report a cycle.** An owner naming a missing key
never becomes ready, so a naive sweep names it twice — once truthfully and once
as part of a loop that does not exist, sending the reader to look for one. A
refusal that invents a second, wrong cause is worse than a longer one.

## 3. Canonical JSON — the PV1 rules

```text
· UTF-8, no BOM, LF, no trailing whitespace
· object keys ordinal-sorted; arrays ordered by entity id (D-5)
· doubles: shortest round-trip ("G17"-equivalent, invariant) — never localised,
  never fixed-point; NaN/Inf are unrepresentable (they were faults upstream)
· Money as integer cents; ids as strings (SDD-004 §6 — ordinals never persist)
· timestamps as (year, month) records — no date strings to parse ambiguously
Writer and reader live in ONE class; there is no second serialisation path to
drift (the L5 principle applied to bytes).
```

## 4. Derived — never saved (consolidated)

The continuation-identity failure class (PV2) is "restored as a value, not as a
live dependency". The inverse trap is saving derived state that then shadows
its source. **The authoritative never-save list**, gathered from every SDD:

| Derived | Rebuilt from | Source |
|---|---|---|
| Catalogue ordinals | id-sorted content | SDD-004 §6 |
| `EffectState` | tech nodes + profiles | SDD-005 §6 |
| Segment plans | availability at stage 4 | SDD-013/R19 §2.8 |
| Barrier strengths | condition + competency + procedure | 14 §2.2 / INV10 |
| Perforation standoff | trajectory + contacts | SDD-003 §5 |
| Environment profiles (generated worlds) | the surface layers | 06 §5.1a step 9.8 |
| Reserves, RRR, borrowing base | beliefs + plans + prices | SDD-009 §4–5 |
| Read model | everything | R21 |

A module attempting to register a state key for any of these fails the
**derived-state review** — enforced as a checklist item on R19's PV4 test plus
this table (a new mechanical check for [22](../design/22_DESIGN_COHERENCE.md)
§6.1: every never-save row names its rebuild source).

> **R20d.12.33 amendment (finding 210): the borrowing base is derived and the
> COVENANT IS NOT, and the row above hides the difference.** "Reserves, RRR,
> borrowing base" is right — `Bank.Settle` recomputes `Terms` from today's
> proved reserves, today's debt and today's ESG standing before it does anything
> else with them, so nothing about the facility's PRICE survives a tick. But
> `Bank.Covenant` is assessed as `_lender.Assess(Terms, Drawn, Covenant)` —
> **it takes its own previous value** — and that makes it a clock rather than a
> quantity: a breach opens a cure window and the months elapsed are what the
> window counts.
>
> Read as one line, the row invites exactly the mistake that was made: the
> facility looked derived end to end and no block carried any of it, so a
> reloaded company came back `Clear` with zero months however deep in breach it
> was. **A player could cure a covenant breach by saving and loading** — the same
> class as the abandonment obligation §2b is careful not to hand back.
>
> **`company.facility` — `Bank` is the owner**, holding `covenant-state` and
> `covenant-ticks-remaining` and nothing else. Not folded into `company.ledger`:
> the ledger owns money that has moved and the facility owns the company's
> standing with its lender, and one fact has one owner (L5). `RestoreAfter` is
> empty — two scalars depend on nothing.
>
> **`Terms` is deliberately NOT in the block.** It is recomputed at the top of
> the first `Settle` after a load, from state that is itself restored, so saving
> it would be storing a value beside the inputs that produce it — the shadowing
> trap this very section warns about, and the one that produced finding 206.

> **R20d.12.34 amendment (finding 208): the same cut, one row up — RRR is
> derived and its HISTORY is not.** "Reserves, RRR, borrowing base" is right
> about the ratio: it is computed from reserves the belief store already carries
> and production the loop already counts, so no block writes the number. But the
> ratio is defined over a TRAILING TWELVE MONTHS (SDD-009 §4), and *what proved
> reserves stood at a year ago* is not recoverable from anything the save holds
> — beliefs restore as they are TODAY, and a reloaded company would have no
> history to measure against.
>
> **`company.reserve-history` — two rings of twelve, and a counter**, on the
> pattern `company.market` already uses for prices: proved reserves and
> cumulative production as they stood at each of the last twelve month-ends. A
> forty-year game is 480 ticks and nothing needs the other 468.
>
> **The indicator reads UNDEFINED until the ring fills**, which is deliberate and
> is not the same as reading zero: a company nine months old has no twelve-month
> window, and a projection that answered 1.0 would be reporting a replacement
> that was never measured. This is the second time in one phase that the
> never-save list has needed a carve-out for the STATE BEHIND a derived value,
> after the covenant clock — the pattern is that a quantity recomputed each tick
> is derived, while a quantity recomputed each tick *from its own past* is not.

> **R20d.12 note (finding 196) — pressure was suspected of being
> path-dependent, and it is not. §4 stands as written.**
>
> The reasoning ran: the engine reaches a pressure through one clamped solve per
> tick while `RestoreTo` performs a single unbounded solve, so the two would
> agree only where `MaxTickPressureDropFraction` never bound — which would make
> pressure a third kind of state, neither stored nor derived, and would explain a
> reloaded field's injector reporting no headroom.
>
> **It was built, and it was wrong twice over.** Storing the pressure did not
> close the gap; and `ReservoirCompartment`'s own header says why it could not:
> *"Pressure is RE-SOLVED from initial conditions every tick, never stepped from
> last tick's value: §3.1 measures every expansion term from Pi, so a rounding
> error in one month cannot compound into the next, and a save that restores
> cumulative production restores the pressure exactly rather than
> approximately."* Every tick already starts from Pi. There is no path to depend
> on, by deliberate design, and `The_save_carries_no_pressure` pins it with a
> rationale storing it would have broken: a save that could assert a pressure
> lets a hand-edited file claim one the material balance never produced.
>
> **Reverted, and recorded rather than quietly dropped** — the next reader to
> notice those two solve calls differ deserves to find this note instead of
> repeating the day. The flood residual (S013-8) has some other cause.

## 5. Migrations

```csharp
public interface IMigrationStep { int From { get; } JsonNode Migrate(JsonNode block, string module); }
```

Chain composition v→v+1; every step ships with a real fixture save of version
`From` (PV5). A gap in the chain = composition fault at startup. Saves from
newer versions: refused by header check with both versions named.

## 6. Corruption and refusal (PV6)

Refusals are specific: digest mismatch names the module whose block digest
diverged; missing mod names the mod and version; truncated zip reports the
entry. **No partial load exists as a code path** — `LoadResult` is
`Loaded | Refused(reasons)`, mirroring the content loader's shape.

## 7. Test mapping

PV1 (canonical rules §3) · PV2 (never-save table §4 — each row gets a targeted
continuation test) · PV3 (digest across the CI matrix) · PV4 (+ derived-state
review) · PV5 (fixtures per step) · PV6 (§6 specificity) · PV7 (SDD-010 §1) ·
PV8 (digest sensitivity) · R19-V9..V15 as specified in the phase doc.

> **R20d.12 review (finding 188) — every part of a save exists and nothing
> assembles them, and the reason it stayed invisible is that the missing piece
> is the only one no unit test can stand in for.**
>
> **What is built and correct.** `StateBlock` captures an owner into a flat
> ordinal-sorted block and stamps the schema version itself so an owner cannot
> forget it. `CanonicalJson` implements §3. `SaveFile.Digest` does §2's
> per-module SHA-256 in module-name order, `SaveFile.Validate` does §6's
> all-reasons-at-once refusal, and `MigrationChain` does §5 including the
> gaps-are-a-startup-fault rule. `StateRegistry.Owners` returns owners in
> state-key order **for exactly this walk** — its own comment says capture and
> restore walk this sequence. `IRandomStream` carries `Position`/`Seek` "saved
> and restored exactly", and `SimulationClock.RestoreTo` exists and says it is
> for load. Nine state owners implement `Capture`/`Restore`.
>
> **What is missing is the walk itself.** Nothing in `src/` calls
> `StateBlock.Capture`, builds a `SaveHeader`, or writes a container — so every
> one of those parts is verified by a unit test of itself and the composition of
> them by nothing. A save is not partially wired; it is absent, and R20d.25's
> imported-water history and R20d.26.4's monitoring kits are the newest facts
> riding on it.
>
> **Finding 188 names two gaps of very different cost, and they separate
> cleanly.** `IEngine.ReadModel` is SDD-017 §2's fifteen-projection `ReadModel`;
> composition publishes `FieldReadModel`, which draws 9 fields from 5 of the 16
> projections because the other eleven have no source until R20d wires their
> subsystems in. **So `IEngine` cannot be implemented today for reasons that have
> nothing to do with saving** — it is blocked on R21.6, and pretending otherwise
> would mean fabricating eleven views. **`WriteSave` is blocked on none of it**:
> a save needs the state owners, the RNG positions, the tick and the container,
> all of which exist. The save path is therefore built against composition's
> `Engine` now, and adopting `IEngine` waits for the read model it names.
>
> **Load composes a NEW engine** (SDD-017 §1b, and PV2's continuation-identity
> rule): build the module set from the header's seed, then restore each owner
> into it. Restoring into a live engine would be mutating a graph whose
> dependencies were wired against the old values, which is precisely the
> "restored as a value, not as a live dependency" failure §4 opens with.
>
> **Two header fields have no honest source yet** and are declared rather than
> invented: `engineVersion` and `contentVersion` are constants until there is a
> release process to stamp them, and `activeMods` is empty because no mod system
> exists. Each is a real value with a stated provenance, not a placeholder
> standing in for work (L3) — and a save that refused to name its versions would
> be worse than one that names honest ones.

> **R20d.12 review, second half (finding 194) — the read side needs two things
> this engine does not have, and design 11 §2.1 already specifies both.**
>
> **1. A restore order that is not the capture order.** `StateRegistry.Owners`
> returns owners in STATE-KEY order, and its own comment gives the reason:
> composing modules differently must not change a byte of the save. That is
> correct for capture and wrong for restore. Design 11 §2.1: *"Modules declare
> their restore dependencies (facilities before the pipelines that connect them;
> reservoirs before the perforations that drain them). The registry topologically
> sorts them, and a cycle is a composition error caught at startup, not a
> mysterious load failure."* **Two different orders over one registry**, and the
> engine currently has one.
>
> **2. A loader that rebuilds the field.** `WellsState.Capture` records which
> completions are open and what each drains, and says the completion's own
> configuration — tubing, choke, lift — *"is CONTENT, restored by the loader
> rather than copied into every save"*. **That loader is the missing piece.** The
> save carries the decision (which wells, draining what); content carries the
> design; and nothing yet puts the two together, so `Restore` treats the record
> as a checksum against a rebuild nobody performs.
>
> **What a rebuild has to do, from `FieldControl.OpenWell`:** take a header slot,
> route the trunk and place the manifold if it is the first tie-in, open the
> completion, register the abandonment obligation, lay the gathering line at that
> field's distance from the header, and connect both ends into the network. All
> of it is already written — a rebuild wants that path with the SAVED id instead
> of `NextWellId()`, not a second one beside it (L5).
>
> **Which fixes the ordering constraints in place:** world and subsurface must
> restore BEFORE the wells are reopened, because the run's length is read from
> where the field is; and the obligation registry must restore AFTER, because
> reopening registers an obligation and the save's own record is the one that
> should stand. Those are exactly the "declared restore dependencies" §2.1 asks
> for, discovered by trying to write the load rather than by reasoning about it.
>
> **Not built ahead of need.** The topological restore order has no caller until
> the rebuild exists, and shipping it first would be one more mechanism joined to
> nothing — which is the defect this project has now recorded fourteen times.
> They land together or not at all.

## 8. Open items

| # | Item | Trigger |
|---|---|---|
| S013-1 | Audit sidecar rotation for very long games (size cap + oldest-summarised) | R19.4 |
| S013-2 | Save-diff tool (R19-V15) — ships as a dev utility over the canonical form; scope | R19.5 |
| S013-3 | `engineVersion` / `contentVersion` stamped from a release process rather than declared as constants | a build pipeline |
| S013-4 | ~~The audit sidecar~~ **DONE** (R20d.12.23). `audit/trail.jsonl`, written from the retained trail and excluded from the digest, restored on load with ids verbatim so a `Cause` written before a save still resolves after one. **The restore REPLACES rather than refusing a non-empty trail**, and the guard that first refused was right to fire: a load REGENERATES before restoring and regeneration records as it goes, so the assumption behind the refusal — that a fresh engine writes nothing while being loaded — is false for any save that regenerates. Same resolution as `BeliefStore` for the same reason (SDD-008 §4b.1). **`RestoreFrom` and `Prune` live on the concrete `AuditTrail`**, never on `IAuditTrail`, so a module can record and query and cannot rewrite history; `DiagnosticsModule` provides both, exactly as it already did for the clock. Pinned by `S013V4_a_reloaded_game_remembers_why`. **The original note is kept below** because it is what the item was for. The audit sidecar (§1's `audit/`) — the container ships state and header first; the trail is its own task with its own retention policy. **The design question it opens, stated before anyone starts it** (R20d.12.19): is a reloaded trail an ARCHIVE of what happened, or is it RESTORED so the cause chain still resolves? §1 lists `audit/full.jsonl` as excluded from the digest, which reads like an archive — but 09 §4.3's "why?" walks `Cause` links, and a chain that stops at the last reload answers nothing about the month a player is actually asking about. **Restoring it means preserving `AuditId`s and the next-id counter across a load**, which is precisely the constraint that blocks finding 202's cause chain: an id cannot survive a reload today, so a deferral cannot cite the failure behind it. **S013-4 and finding 202 are the same problem** and are more related than their numbering suggests — whichever is done first should decide the id question for both — and S013-4 was, at SDD-013 §1b | ✅ |
| S013-5 | ~~**The rebuild + declared restore order**~~ **DONE** (R20d.12 / R20d.12.15). The rebuild reopens saved completions through `FieldControl` with their saved ids; `IStateOwner.RestoreAfter` and `StateRegistry.RestoreOrder` give §2b's topological order beside the key-ordered capture. **It proved itself on the first run**: `company.obligations` sorts before `wells.completions` and must be restored after it — reopening a well registers an abandonment obligation, so the save's record has to win or a company gets back one it had discharged. That had been a sentence in the loader's docstring; the declared order turned it into a claim a sort enforces and the one place it was wrong faulted immediately. **Cycles and dangling keys refuse at COMPOSITION**, attributed to the module that declared the key | ✅ |
| S013-6 | ~~The EPOCH is not in the header~~ **CLOSED** (R20d.12.19, specified at §2 first). The header carries `epoch{year, month}` and `Load` takes it from the save alongside the world seed, on the same stated principle: a host asked to supply either for a game it has not opened yet would be guessing. Asked of the CLOCK rather than a caller's settings, since the clock is what turns a tick count into months — and `SimulationClock.Epoch` is deliberately NOT on `ISimulationClock`, because a module needs to know what month it is while only the thing writing a save needs to know where counting started. **Verified by disabling it**: a save made in March 1967 reloaded as September 1992, a twenty-five-year relabelling with the simulation bit-identical. That is why this waited as an open item rather than a finding — every number right and every date wrong is the worst shape a defect can take, because nothing looks broken | ✅ |
| S013-7 | ~~Reservoir pressure is path-dependent and must be stored~~ **Withdrawn** — built, measured, reverted: every tick already re-solves from Pi, so there is no path, and storing it would break the hand-edit guarantee `The_save_carries_no_pressure` exists for (see §4's note) | ✅ |
| S013-8 | **The flood residual, narrowed by measurement.** Probed over three ticks: a reloaded field imports **0 in the first month and 43,904 m³ in the second** against the original's steady 35,495 → 35,527 → 35,558. It skips exactly one month and then OVER-imports. So the intake and the injector are wired correctly by the rebuild and the cause is a stale first-tick input to `CommandTheIntake` — `target = VRR·voidageLastTick − producedWaterLastTick`, of which the set point demonstrably restores and the other two are the suspects. **Both of those were then eliminated by printing the block**: `field.flood` is in the container carrying `voidage-last-tick 43909.35`, `produced-water-last-tick 4.40`, `voidage-replacement 1.0` — the flood's own bookkeeping survives intact. **So the zero is the OTHER input**: `ReservoirRoom()` multiplies by the room the reservoir has left to its ceiling, and that room is 0 in the reloaded engine's first month and 62,419 m³ in its second. It is compartment state, and it is not the pressure (built, measured, reverted — finding 196). **Closed by reading `ReservoirRoom()` rather than theorising about it a fourth time**: it walks `_floodShares`, a FOURTH cross-tick list on the production loop, and an empty one leaves the cap at infinity and returns exactly 0. Saved with the other three. **A reloaded game now continues identically for two years — production to the cubic metre and cash to the cent, every month, with every account balance agreeing.** The measurements that led here cost minutes each; the one theory that was built instead cost a great deal more | ✅ |
| S013-10 | ~~**A facilities state block — the category sweep, and the largest gap left**~~ (finding 197). Six fitted tiers (`Manifold`, `Separator`, `Tank`, `GasCapture`, `Treater`, `ExportTerminal`) plus a tank's contents, provenance and promised mass, a pipeline's linefill and an intake's commanded rate. Every tier is a ladder bought with money, so a reload today returns the starting equipment and keeps the spending. **The fixture blind spot ships with it**: PV2 drills and floods but never installs, so it passes while this is broken — whatever owner lands must come with a fixture that BUYS something. **DONE** (R20d.12): `FacilitiesState` owns `facilities.units` — the manifold, separator, tank, gas-plant and treater tiers, the intake's commanded rate, the tank's provenance shares and every inventory — and the export terminal owns `field.export` beside it, which is where the sixth tier lives. **The fixture blind spot was closed with it**: PV2 now installs and buys, which is what made the tiers testable at all. Recorded because this row kept an open trigger after the work landed and the consolidated register inherited the error — closing an item is two edits and nothing enforces the second (finding 212) | ✅ |
| S013-11 | **The lagged-input sweep on `ProductionLoop`, and its one hit.** Eight mutable fields. FIVE are scratch and provably so — `_stored`, `_importedThisTick`, `_reservoirRoom`, `_disposedThisTick` and `_sale` are each cleared or reassigned at the top of the tick that reads them. TWO are saved (`_voidageLastTick`, `_producedWaterLastTick`), as are `Delivered`, the flood shares and the cumulative totals. **`_tankProvenance` looked like a hit and is not one — corrected here rather than left standing.** It is assigned only when oil passes custody, so it does retain a stale value across a barren month. But it is READ in the same tick that fills it, by `StoreAndExport` calling `_tank.Receive(_stored, _tankProvenance, tick)` — and `_stored`, its partner, IS zeroed at the top of every tick. A stale provenance is therefore only ever handed over with zero mass. It is the INPUT to a receipt, not a mirror of the tank's state, so there is no second owner and nothing to save. **And `Tank.Receive` was checked rather than assumed**: it returns at `arrivingKg <= 0.0`, before the blend, so a stale provenance can never reach `_provenance`. The guard is there for its own reasons and it closes this too. **The sweep's result is eight fields, no hit, and no loose end** — `ProductionLoop` is fully accounted for, and that is recorded so the next reader starts somewhere else | ✅ |
| S013-9 | **Two more cumulative totals were unsaved and are now closed**: `CumulativeFlared` — what a company has flared over its life, which the ESG record is scored on and its debt priced against — and `CumulativeProduced`, which the bank lends against. A reload reset both, so a forty-year field came back with the flaring record of a new one: 200,013 tonnes against 3,287. **What is left is `Chain`, narrowed to two of a row's six parts**: the network is identical — same rows, same order, same identities, nothing failed differently, all asserted — and what parts is `condition` on the wells (0.5523 against another value, so equipment ages slightly differently after a reload) and `throughput` on `water-disposal`. **CONFIRMED by measurement** — the original carries a water cut of 7.77e-6 into the first tick after a save and the reloaded engine carries 0.000000000, so the two age their plant on different service for exactly one month. Found by reading and then checked before being acted on, which is the order the pressure detour taught. Stage 4 ages equipment on the PREVIOUS tick's service, which is design 03 §6.1's declared one-tick lag and the right way round: metal corrodes in the duty it has had. `ProductionLoop.WaterCut` is derived from `Delivered`, last tick's rates — and `Delivered` is not saved. A reloaded engine would therefore age its plant for one month as though the field were dry, which is a condition difference on exactly the wells the probe named. **The declared lag makes last tick's delivered composition CROSS-TICK state**, however derived it looks from inside one tick; §4's derived column and design 03 §6.1's lag disagree about it, and that is the thing to settle. **The fix is the disagreement, not a key.** Saving `WaterCut` would store a ratio the loop already derives and give one fact two owners (L5). What is genuinely cross-tick is `Delivered` — last tick's rates, which the lag makes an input to the next month — so that is what a block carries, and the cut stays derived from it. §4's derived column gains the qualifier it was missing: **a quantity derived from THIS tick is never saved; one that a declared lag makes an input to the NEXT tick is state, and is saved at its source rather than at its ratio.** | ✅ |

**Both are on the WATER side**, which is consistent with every barrel of oil and every cent already agreeing. **One water-side defect was found and fixed on the strength of that and did NOT close it**: the disposal well's plugging IS its cumulative injection (§6c's impairment scales with it), nothing saved it, and a restored injector came back with a clean formation however many years it had been used. Real, fixed, and the chain still parts — so there is at least one more. **Facilities own no state block at all**, which is the shape to suspect next: tank contents, fitted tiers and any other element carrying a number between ticks are in the same position the injector was. **CLOSED** (R20d.12.18). Facilities got their block (S013-10), the delivered rates got theirs, and the LAST piece was none of the above: connate water saturation had TWO OWNERS — derived as `1.0 - OilSaturation` on the compartment and declared as `swc` on the rock curve — differing in the last bit, and `Capture` wrote one key that `Restore` read into both. **The save never lost a value; it UNIFIED two that were never equal**, so a reloaded compartment had them agreeing where the original had them differing, and `krw`'s `(Sw − swc)` turned that into a sixtieth of the produced water just above connate. Fixed by giving the fact one owner (the rock — SDD-003 §3.1's R20d.12.18 amendment), which also exposed that `WorldSink` was handing one fixed curve to every compartment in a basin whose saturations it drew individually. **PV2's `Chain` exception is withdrawn**: every read-model field now agrees, month after month for two years. **The method note worth keeping**: every instrument built to hunt this — the per-module digest diff, the RNG position check, the double-reload self-consistency test — compares what the CONTAINER holds, and none of them could ever have seen this, because the container was a faithful record of one of the two values. What they did was eliminate six families by measurement until a twenty-line subsurface unit test became the obvious move, and that test found this and finding 205 in one run | ✅ |
