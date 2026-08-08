# SDD-008 — Information and Beliefs

**Status:** drafted · **Serves:** R14 · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §6, [06](../design/06_WORLD_AND_EXPLORATION.md), [R14](../phases/R14_INFORMATION.md), [SDD-005](SDD-005_CAPABILITIES_AND_EFFECTS.md) §5

The statistics, pinned. Without this document an implementer improvises
Bayesian machinery — and improvised statistics *look* right for months. Every
update rule here is conjugate or closed-form, so every posterior has an exact
expected value a test can assert (R14-V2).

---

## 1. Scope

`OGSim.Information`. Truth types `internal`; the public surface is beliefs,
observations-as-commands (survey operations deliver here), and projections.

## 2. Belief representation — one mechanism

```csharp
public enum BeliefSpace { Linear, Log }     // declared per property-kind in content

// Design 02 §1.2 / R2 §2.2 — ORDERED BY CONFIDENCE, and the order is the
// contract: it drives the default uncertainty, the §2.1 update weighting, and
// the player-facing "how do we know this?". ProductionHistory ranks near the
// top because the dynamic data is the most trustworthy thing about a reservoir,
// which is what makes the p/Z deduction of §6 as powerful as it is.
public enum Provenance
{
    Assumed, Analogue, Seismic, Log, WellTest, Core, ProductionHistory, Measured
}

public readonly record struct Belief(double Mu, double Sigma, BeliefSpace Space,
                                     Provenance BestSource, GameDate AsOf);
```

> **Contract pass 10.** `Provenance` was consumed by `Belief` here, by
> `IProperty` in [SDD-002](SDD-002_STREAMS_AND_FLOW.md) §2b and by `Observation`
> in §3, and declared in no SDD. It is declared here because this is where the
> confidence ordering is *used*; note that R2 needs it well before R14, so the
> committed type correctly lives in `OGSim.Contracts` and is available from the
> materials phase onward.

- **Every belief is Normal in its declared space.** Additive kinds (depth, net
  pay, saturation) are `Linear`; multiplicative kinds (permeability, area,
  volumes) are `Log` — a Log-space Normal *is* the log-normal the design
  requires (05 §1.4), and it makes every update the same conjugate formula.
- P10/P50/P90 are closed-form quantiles of the (log-)normal:
  `P50 = f(Mu)`, `P10/P90 = f(Mu ± 1.281552 · Sigma)` (constant in
  `PhysicalConstants`, cited).

### 2.1 The one update rule (Normal–Normal conjugate)

```text
Observation: value v with declared σ_obs, both transformed into the kind's space.
  precision_post = 1/σ_prior² + 1/σ_obs²
  μ_post = ( μ_prior/σ_prior² + v/σ_obs² ) / precision_post
  σ_post = sqrt(1 / precision_post)
Provenance: BestSource = max(confidence) of contributors; AsOf = now.
INV8 guard: σ_post has a per-kind floor (content) unless Provenance == Measured.
```

Staleness (02 §1.2 "properties can go stale"): σ grows by a per-kind drift per
year *for dynamic kinds only* (pressure, contacts — things production changes);
static rock properties do not drift. Drift is content; zero is legal.

## 3. Observation sampling

```csharp
// The ONLY shape that crosses the truth wall: a sampled value with an honest
// sigma, never truth itself. Audited on delivery (09 §4.2's fairness record).
public sealed record Observation(
    EntityRef Subject,
    ContentId PropertyKind,
    double Value,
    double Sigma,
    BeliefSpace Space,
    Provenance Source);

// What Get cannot answer: WHICH pairs are known. Ordered by first learning, so
// two runs of one save project the same list in the same order (D-5).
public readonly record struct HeldBelief(
    EntityRef Subject, ContentId PropertyKind, Belief Belief);

// Apply is the ONLY writer. There is deliberately no Set, no seed-from-truth
// and no bulk import: world generation delivers initial beliefs through this
// same door (R15-V10), so there is no belief-copy path for truth to leak down.
public interface IBeliefStore
{
    void Apply(Observation observation);                        // §2.1's conjugate update
    Belief? Get(EntityRef subject, ContentId propertyKind);     // null: nothing known yet
    IReadOnlyList<HeldBelief> Held { get; }                     // §8's projection walks this
}
```

> **R20d.3 note on the truth door.** `SubsurfaceState`'s internal
> `True…Of` members are this section's door and carry a comment saying nothing
> may be added to them without returning here. `TrueWaterCutOf` is added on the
> same terms as `TruePressureOf`, and for the same second reason: both are
> DYNAMIC compartment facts that a completion must be refreshed with before it
> solves (finding 137), so the door serves the belief sampler and the one
> composition that pushes numbers between two modules that cannot see each
> other. Neither member hands out a belief — a caller still has to put the
> number through `Sample` to learn anything, and one that applies it to a belief
> directly has walked through the wall.

`Get` returning null rather than a wide prior is deliberate: "we have never
looked" and "we looked and learned little" are different states, and only the
first should leave a map region unrendered.

> **R20d.7 amendment.** §8 has always said beliefs project as
> `(P10, P50, P90, BestSource, AsOf)` **per kind** — and this interface could not
> produce that list. `Get` answers about a pair the caller already holds, so the
> stage-13 projection had to be given the (subject, kind) pairs from somewhere
> else, and the only place they exist is inside the store. The consumer was
> specified and the door was not, which is finding 147's shape seen from the
> store side: a host promised a belief panel that nothing could enumerate.
>
> **`Held` is not a widening of what a player may see.** A pair enters it only
> through `Apply`, so it lists exactly what `Get` would already answer, one call
> at a time — the door does not decide what is knowable, it decides whether the
> known can be walked. A store that could be enumerated *before* anything was
> observed would be the leak, and there is no such state: an unobserved subject
> has no entry to find.
>
> It carries the whole `Belief` rather than the key alone because a caller handed
> a key it must then re-`Get` can be answered `null` for a pair the store just
> named — a state the type should not be able to express.

```text
For each property kind a source can see (content: kinds + σ_obs per kind):
  1. Detectability gate first (SDD-005 §5) — may yield NOTHING
  2. truth value → kind's belief space → v = value + Normal(0, σ_obs)
     drawn from `exploration` (surveys) or `measurement` (logs/tests/meters)
  3. audited (source, kind, σ, draw) — the fairness record (09 §4.2)
  4. conjugate update (§2.1)
```

Sources never return truth, never return bias — σ honest, centre honest. What
distinguishes a core from a log is *which kinds* and *how small the σ*.

> **Ninth contract pass (finding 82).** The step-2 draw above *is* the
> `IObservationModel` slot — one of the eleven replaceable models in
> [03](../design/03_ARCHITECTURE.md) §3.2, "per source; tunes how much
> uncertainty survives" — and it was never declared, so the algorithm had no
> contract to sit behind. Pass R1-C5's claim that every §3.2 slot was a compiled
> type was wrong on this one as well as on SDD-006's two.

```csharp
/// Design 03 §3.2 — the per-source error model. Replacing it is how a scenario
/// makes seismic sharper or logs noisier without touching truth or the update.
public interface IObservationModel
{
    ContentId Id { get; }        // finding 132

    /// The honest sigma for this source reading this kind, before the draw.
    /// Returns null when the source cannot see the kind at all — absence is a
    /// legitimate answer here, and NOT the same as a wide sigma.
    double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject);
}
```

**Why the model owns σ and not the draw:** the draw consumes a named RNG stream
(`exploration` or `measurement`) and must stay in the engine, where the stream
and the audit record live. A plugin that drew its own numbers could silently
consume a different count and shift every later draw in that stream — the exact
independence property R1-V5 exists to protect.

> **R12b amendment (finding 149) — the truth door, and σ's units.** Three things
> this section pinned had nothing behind them, and the first activity that
> measured anything walked straight past all three.
>
> **(a) The subsurface declared one door.** `SubsurfaceState.TruePressureOf` was
> the whole of it, so a build-up was the only measurement that *could* be built;
> a log, a core and a survey each measure something else and would otherwise have
> had an `internal` member invented for them at the call site. The door is now
> the whole set, `internal` to `OGSim.Subsurface` and visible only to
> `OGSim.Composition`:
>
> ```csharp
> internal Pressure      TruePressureOf(EntityId<IReservoirCompartmentEntity> c);   // dynamic — moves as it is produced
> internal double        TruePorosityOf(EntityId<IReservoirCompartmentEntity> c);   // RockTruth, static
> internal Permeability  TruePermeabilityOf(EntityId<IReservoirCompartmentEntity> c);
> internal SurfaceVolume TrueOilInPlaceOf(EntityId<IReservoirCompartmentEntity> c); // N, INITIAL conditions
> ```
>
> Every one returns a bare number the caller must still push through
> `ObservationSampler` to learn anything: there is no path from any of them to a
> belief that skips the draw. `TrueOilInPlaceOf` reads initial conditions rather
> than remaining volume deliberately — a survey sees the accumulation, not what
> is left of it, and a company cannot deduce cumulative production by re-shooting.
>
> **(b) σ is in the KIND's space, and a source is priced per kind.** The prose
> above already said it — "content: kinds + σ_obs per kind", "what distinguishes
> a core from a log is *which kinds* and how small the σ" — but the first
> `IObservationModel` keyed on source alone. Every source therefore saw every
> kind at one number, which let a build-up measure oil-in-place at 0.03 and made
> shooting seismic pointless. `SigmaFor(source, kind, subject)` is a **table over
> both**, and the σ it returns is absolute in that kind's declared space:
> porosity in porosity units, pressure in pascals, permeability and oil-in-place
> in natural-log units where an absolute σ *is* a relative one. `null` where the
> pair is not in the table — a log cannot see the areal extent of an accumulation
> and must return nothing rather than a wide guess.
>
> INV8's floor is per kind for the same reason: one flat floor either erases the
> difference between a core and a log or is meaningless against a pressure in
> pascals.
>
> **(c) `ObservationSampler` is composed, or it is not the door.** R14.3 built
> it — stream selection, the σ sanity check, the 09 §4.2 fairness record — and no
> module provided it, so composition's first measuring activity sampled truth by
> hand and delivered a belief with no audit trail. `OGSim.Information` provides
> `ObservationSampler`; anything that measures resolves it. **A `beliefs.Apply`
> whose `Observation` did not come out of `Sample` is a defect** — that is the
> whole of the wall, and it is a review obligation until R14.12 can assert it.

## 4. POS — Beta-Bernoulli, per factor

```csharp
public readonly record struct FactorBelief(double Alpha, double Beta);   // mean = α/(α+β)

// The five petroleum-system factors (06 §2.2). POS = product of the five means.
public enum PosFactor { Source, Reservoir, Seal, Trap, Timing }
```

- **Play-shared factors** (source, reservoir-presence, seal) live on the play;
  **prospect-local factors** (trap, timing) live on the prospect
  ([06](../design/06_WORLD_AND_EXPLORATION.md) §2.1–2.2).
- `POS(prospect) = Π factor means` — five numbers multiplied, displayable and
  decomposable (open decision W6).
- **Drill outcome update:** the dry-hole diagnosis names the failed element
  (truth-derived, R14 §2.5). That factor gets `Beta += w_hard`; factors the
  well *proved* get `Alpha += w_hard`; undiagnosed factors get `w_soft`
  updates from the outcome. `w_hard`, `w_soft` are content (defaults 2.0,
  0.5). A discovery updates every element's `Alpha` — de-risking the play.
- **Survey updates** target the factors the source sees (seismic → trap hard,
  reservoir soft; basin modelling → timing) with `w` scaled by the source's
  declared strength. Conjugate, auditable, and the play-correlation mechanism
  *is* the shared Beta — no separate correlation machinery (R14-V6).

> **R20d.7.5 amendment. Survey updates, and the weights.** This section
> specifies `w_hard` and `w_soft` (content defaults 2.0 and 0.5) and
> `ProspectRisk.Observe` had no weight at all — every piece of evidence counted
> as exactly one well. So a seismic survey and a drilled hole would have moved a
> factor by the same amount, which is not a balance question but a category
> error: a survey images a structure and a well proves it.
>
> ```text
> Observe(factor, present, weight)   α or β += weight
> seismic:  Trap      hard   — it images the closure
>           Reservoir soft   — amplitude hints at the rock, and no more
> ```
>
> **This is what makes the five factors worth showing** (SDD-017's
> `ProspectView`). A player reading "one in six, and it is the trap we doubt"
> can buy the survey that addresses the trap; a player reading the same about
> source cannot, and has to drill or walk away. Without per-factor updates the
> decomposition is decoration.

> **R20d.7.6 amendment. The re-key needs a method.** This paragraph has
> specified re-keying since R14 and `IBeliefStore` has no way to do it, so a
> discovery stranded everything the company had learned: the structure-capacity
> belief a survey was bought for stayed on the prospect while the field it
> described became a compartment nobody knew anything about.
>
> ```csharp
> void ReKey(EntityRef from, EntityRef to);   // same Mu/Sigma/provenance/as-of
> ```
>
> **A MOVE, not a copy.** Leaving the original would double-count: the prospect
> and the accumulation would each answer for the same fact, and an appraisal
> updating one would leave the other stale — which is law L5 in the information
> layer. Re-keying onto a subject that already holds that kind is a fault rather
> than a merge, because two beliefs about one fact have no correct combination
> and picking either silently discards evidence.

**On discovery, beliefs re-key, never reset:** the prospect's property beliefs
and volumetrics become the accumulation's (same Mu/Sigma/provenance, new
entity), and appraisal continues updating them through §2.1. Nothing is thrown
away and nothing double-counts — one belief line per fact, before and after
the strike.

## 5. Volumetrics

In-place = product of Log-space beliefs (A, h, φ, 1−Sw, 1/Boi):
`Mu_prod = Σ Mu_i`, `Sigma_prod = sqrt(Σ Sigma_i²)` — exact for log-normals,
which is why the kinds are Log. Recoverable multiplies the RF belief (Log),
conditioned on the drive-mechanism hypothesis (§6). P10/50/90 closed-form.

## 6. Production-history inference

- **`p/Z` line:** each pressure survey appends `(Gp, p/Z)`. Fit by **weighted
  least squares** (weights 1/σ_survey²); G-intercept becomes an observation of
  in-place gas with σ from the fit's standard error — then §2.1 as usual. Two
  mechanisms only: fit → observation → conjugate update.
- **Drive identification:** competing drive hypotheses scored by weighted SSE
  of predicted-vs-surveyed pressure; posterior over hypotheses by normalised
  `exp(−SSE/2)`; displayed as the drive panel.
- **Compartment inference (M1):** 1-tank vs 2-tank material-balance fits
  compared by **BIC**; `ΔBIC > threshold` (content, default 6) raises
  `reservoir.compartmentInferred` (R14-V12). Pinned: BIC, not eyeballing.

## 7. Value of information

```text
VOI(source, decision) = E[ max_a EV(a | posterior) ] − max_a EV(a | prior)
Computed over K = 128 scenarios drawn by HALTON sequence (bases 2,3) through
the prior — deterministic, reproducible, and consumes NO RNG stream (it is
advisory arithmetic, not world randomness — pinned so replay is untouched).
EV uses the player's current economics (prices, costs) — deliberately wrong
when their beliefs are wrong (06 §3.2).
```

> **R20d review amendment (finding 147) — the shape, declared.** The algorithm
> above was pinned and no type carried it, while
> `ExplorationView.PendingValueOfInformation` had sat on the read model since the
> contract passes — a promised output with no producer.
>
> ```csharp
> public sealed record InformationValue(
>     ContentId Source, EntityRef Subject, Money Cost, Money ExpectedValue)
> {
>     public bool WorthBuying { get; }        // ExpectedValue > Cost
> }
>
> public interface IInformationValueModel
> {
>     ContentId Id { get; }
>     IReadOnlyList<InformationValue> Rank(
>         IReadOnlyList<ContentId> availableSources,
>         EntityRef subject,
>         IBeliefStore beliefs);
> }
> ```
>
> `Rank` rather than a single evaluate: the player's question is "which survey,
> if any", and answering it one source at a time would push the comparison into
> every host. The no-RNG pin above is the contract's hardest requirement — a
> player opening the VOI panel must not shift a single later draw.

## 8. Projections (read model)

Beliefs project as `(P10, P50, P90, BestSource, AsOf)` per kind; POS as five
factor means + product; "beyond current imaging" as a boolean per play-region
with **no attached magnitude** (the finding-51 no-leak rule). Architecture
test: the projection assembly references no truth type.

The walk is `IBeliefStore.Held` (§3) and the shape is
[SDD-017](SDD-017_HOST_SURFACE.md) §2's `BeliefEntryView`. Quantiles are §2's
closed form, **P90 the low case and P10 the high** — the projection is where the
petroleum convention faces a host, and reading them the statistical way round
would render a possible case as a proved one. `Mu` and `Sigma` are deliberately
NOT projected: a host given the parameters could quote any quantile it liked,
including ones this document has not pinned, and the three that ship are the
three the design asks a player to decide on.

## 9. Error surface

| Situation | Response |
|---|---|
| Observation for a kind the source does not declare | Content fault at load |
| σ_obs ≤ 0, weights ≤ 0 | Content fault |
| Belief update yielding σ below the floor without Measured provenance | INV8 |
| Fit with < 3 points requested | Advisory unavailable — not a fault; the panel says "insufficient data" |

## 10. Test mapping

R14-V2 (conjugate exactness — closed-form asserts) · V3 (σ honesty over
samples) · V4 (floors) · V5 (provenance weighting = σ ordering) · V6 (shared
Beta correlation) · V7 (diagnosis hard-update) · V8 (five-factor product) ·
V9 (log-normal product moments exact) · V10 (VOI vs hand-computed two-action
case) · V11 (`p/Z` WLS recovers G within fit σ) · V12 (BIC compartment) ·
V13/MB4 end-to-end · V14 (detectability nothing) · R15-V10 (leak).

## 11. Open items

| # | Item | Trigger |
|---|---|---|
| S008-1 | Beta visualisation for the host (factor confidence, not just mean) | R21 read-model review |
| S008-2 | Whether staleness drift should pause while shut-in (no production ⇒ less change) | R14 model tests — start: drift only while the compartment produces |
| S008-3 | Bounded Linear kinds (porosity, saturation ∈ [0,1]): Normal quantiles can exceed the bounds near the edges — start with clamped display quantiles; move the kind to logit space only if MB-band tests object | R14 model tests |
