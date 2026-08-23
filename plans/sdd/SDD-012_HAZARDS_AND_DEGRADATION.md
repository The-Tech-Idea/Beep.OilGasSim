# SDD-012 — Hazards and Degradation

**Status:** drafted · **Serves:** R18 and R23 · **Design docs:** [05](../design/05_SIMULATION_MODELS.md) §7–8, [R18](../phases/R18_HAZARDS.md), [14](../design/14_HSE.md) §2

Condition, failure and maintenance in implemented form — the decay law, the
hazard law, and exactly when the dice are rolled.

---

## 0. The two replaceable models

Both are [03](../design/03_ARCHITECTURE.md) §3.2 slots — the hazard model is
named there explicitly as the "off ↔ realistic ↔ punishing" difficulty dial —
and this document pinned both algorithms without declaring either interface
(contract pass 10).

```csharp
// The §1 severity sum, as an argument rather than as ambient state: both models
// are BLIND to everything but their declared inputs, which is what lets a
// scenario swap them without the engine noticing.
public sealed record ServiceSeverity(
    double WaterCut,
    double SourFraction,
    double DutyFraction,
    double OverTemperature,       // max(0, T_amb − T_rated) / T_span
    double TicksSinceService);

public interface IDegradationModel      // §1: severity in, condition out
{
    ContentId Id { get; }
    double NextCondition(double condition, ServiceSeverity severity, Duration dt);
}

public interface IHazardModel           // §2: condition in, probability out
{
    ContentId Id { get; }
    double FailureProbability(double condition, Duration dt);
}
```

**Neither model draws.** `IHazardModel` returns a probability and the engine
performs the draw at stage 4, consuming only the `Hazard` stream — the same
separation §3 of [SDD-008](SDD-008_INFORMATION_AND_BELIEFS.md) applies to
`IObservationModel`, and for the same reason: a plugin that drew its own numbers
could consume a different count and shift every later draw in that stream,
breaking the independence R1-V5 guarantees.

## 1. Condition decay

Per component instance, per tick, at stage 4, from **previous-tick service**
(the 03 §6.1 lag rule):

```text
c ∈ [0,1];   Δc = − baseRate(tier) · (1 + Σ severityTerms) · Δt(years)
severityTerms (coefficients per equipment class, content):
  k_w · waterCut  +  k_s · sourFraction  +  k_d · dutyFraction
  + k_T · max(0, T_amb − T_rated)/T_span  +  k_i · yearsSinceService
Environmental terms (marine air, ice) enter as the location profile's declared
severity contributions — the R18 §2.7 factors, same sum.
```

Clamped at 0; **never restored implicitly** — only a completed maintenance/
repair operation sets `c = restoreLevel(opTemplate)`.

## 2. Failure hazard — the pinned law

```text
λ(c) = λ_base(tier) · exp( k_h · (1 − c) )          [1/year]   k_h content per class
p_fail(tick) = 1 − exp( −λ · Δt )
One draw per component per tick from the `hazard` stream (fixed component
order: ascending EntityId). On failure:
  failure day d ~ UniformInt{0..29} from the SAME stream (next value) —
  integer days, so the segment boundary lands on the /30ths grid exactly
  → segment boundary at d/30 (SDD-002); repair operation auto-proposable (Advisor)
  → audited (component, c, λ, draws) — the fairness record
Repair duration/cost from the tier datasheet; early-generation tiers carry
higher λ_base (07 §4b.3), which is the whole proven-vs-new trade.
```

Exponential-in-(1−c), not a threshold: the player who "sits just above the
line" is not rewarded, deferral cost grows smoothly (R18 §2.2 rationale).

> **R20d.22 amendment (finding 180) — WHAT degrades, in the shipped composition.**
>
> §1 says "per component instance" and the R20d contract pass typed
> `ComponentState.Id` as `EntityId<IWellComponent>`, which is narrower than the
> rest of the section: the severity terms are stated per *equipment class*, the
> environmental terms come from a *location* profile, and neither is a property
> of downhole equipment in particular. A separator in marine air corrodes.
>
> **The subject is the registered flow element** — `EntityId<IFlowElement>`, the
> set stage 4 already walks to build the availability list. That is the whole
> surface chain and every completion: the things that can be absent from a
> network are exactly the things whose absence the segment plan can express, and
> a degradation model whose failures could not be expressed as absence would
> have nowhere to put its outcome.
>
> Well components in the §1 sense are not lost by this. A completion IS a flow
> element; a lift method installed in one is reached through it, and when the
> lift-method tiers become separately failable they enter this same set through
> their own registration rather than through a second parallel mechanism.
>
> **Integrity owns the conditions and stage 4 runs the pass.** Before this
> amendment the module composed two models, declared `ownsState: nothing` and
> `stages: none`, and was reachable from no tick — equipment in a running game
> never aged and never failed. The models were correct and joined to nothing.
>
> **A failure must be answerable in the same phase that introduces it.** A
> repair is an ordinary SDD-007 operation (§3's own rule: "all three produce
> ordinary operations — no special execution path"), costing money and a month,
> and it is not optional scope: an unrepairable failure is a field that dies of
> its first unlucky draw, which is the "cost with no response" shape findings
> 172 and 177 already cost this project twice.

## 3. Maintenance strategies

```text
Per asset class (inherited, overridable per asset — R18 risk note):
  RunToFailure:    nothing scheduled
  Scheduled:       maintenance op template every N ticks (content)
  ConditionBased:  requires monitoring tier installed (C14); trigger c < c_trig
All three produce ordinary SDD-007 operations — no special execution path.
```

> **R20d.26 amendment (finding 185) — planned work and emergency work are two
> operations, and until they were, §3 offered one strategy.**
>
> This composition shipped a single `repair-equipment` template: $0.8M and one
> tick, whether the equipment had failed or was merely worn. Measured across
> four seeds, cash at forty years falls **monotonically** with the repair
> threshold — 1465/1424/1301/886, 2220/2151/1998/1490, 3511/3546/3494/2961,
> 1550/1549/1313/791 — so run-to-failure wins outright and the other two
> strategies are ways of paying more for the same field. A choice where one
> option dominates on every seed is not a choice.
>
> **The missing fact is the asymmetry every maintenance organisation is built
> around: unplanned work is slower and dearer than planned work.** Parts are not
> on site, a crew has to be mobilised, the fault has to be diagnosed before it
> can be fixed, and the plant is down for all of it. Pricing the two the same
> makes waiting free, so of course waiting wins.
>
> ```text
> service-equipment  planned    on equipment that still works    cheap, one tick
> repair-equipment   emergency  on equipment that has FAILED     dear, several ticks
> ```
>
> Two ordinary SDD-007 operations, as §3 already requires — no special execution
> path, and the validators make them mutually exclusive so a player cannot buy
> the planned price for a broken thing.
>
> **DURATION looked like the lever that mattered, and it is not the one that
> shipped** — see the R20d.26.2 amendment below, which measured cost alone
> producing the interior optimum this paragraph expected only from months. What
> remains true is the physical asymmetry underneath: a planned service takes the
> element's condition back with the field still producing, while an emergency
> repair is served with the whole chain shut in behind the failure (SDD-002 §5),
> so a failure's months are production the company does not get. **The engine
> already charges that**, because availability keys off failure and not off work
> in progress; what R20d.26 tried to add was MORE of it, and what finding 187
> found is that this field's early years cannot pay for more.
>
> **Calibrated as physics first and measured second.** The asymmetry is real
> before it is convenient — emergency industrial work genuinely runs several
> times the cost and several times the duration of planned work — so setting it
> is not bending a constant until a feature fires (finding 175). Whether an
> interior optimum then exists is a question for the measurement, and whatever
> the measurement says is what gets written down: a mechanic that still has one
> dominant strategy after an honest asymmetry is a finding, not a reason to tune
> further.
>
> **R20d.26.2 amendment (finding 187) — the duration lever works and this field
> cannot afford it, so the cost lever ships alone first.**
>
> Built at 3× cost and 3 months against the planned job's 1, the split produced
> exactly what finding 185 said was missing — an interior optimum on every seed,
> margins 12–36% — and made the shipped field lose money in its fifth year,
> because a month of outage costs ~$12M of revenue at plateau, the worst seed's
> fifth year clears by $17M, and the field takes about one failure a year then.
> One extra month per failure spends most of that margin. Arithmetic, not bad
> luck, and dropping the duration toward it is a tuning loop this section
> already forbids. Reverted at the calibration; the two-operation shape and the
> measurement stand.
>
> **What ships instead, as the cheap experiment finding 187 names first:** the
> same two operations with the asymmetry in MONEY only.
>
> ```text
> service-equipment  planned    working equipment, worn     base cost, one tick
> repair-equipment   emergency  FAILED equipment            3× cost,   one tick
> ```
>
> The 3× is the same multiple the duration experiment used and sits inside the
> 2–5× range industrial emergency work genuinely costs — parts freighted rather
> than stocked, a crew mobilised rather than scheduled. It leaves the fifth year
> alone by construction, because no failure is down for longer than before.
>
> **Measured, and the envelope was wrong.** Four seeds × five triggers, cash at
> forty years, against a CONTROL taken on this same engine at the single price
> rather than remembered from the engine finding 185 measured (finding 179):
>
> ```text
> trigger        0.0     0.2     0.4     0.7     0.9
> one price     1486    1481    1443    1344     959   monotone — waiting wins
>               1519    1531    1478    1343     828
>                142     128      92     -28     -37
>               1127    1134    1140     966     628
> 3x emergency  1025    1028    1090    1103     782   interior peak
>               1010    1051    1066    1082     663
>                -56     -30     -47     -53    -105
>                730     772     801     785     500
> ```
>
> The control reproduces finding 185 on the current engine — the waterflood and
> the souring had not overtaken it — and the asymmetry inverts the shape on
> **every seed**: a 0.4 trigger beats waiting by 5.6–9.8%, and 0.9 still costs a
> quarter to a third of the company. §3 has three strategies again, and the
> middle one is the answer.
>
> **The envelope's error was counting only the bill.** It priced avoided
> failures at the $1.6M of extra parts and ignored that a company short of cash
> repairs LATER — the run-to-failure field ends $461M behind on the shipped seed
> having paid $270M of extra invoices, and the rest is production it did not
> make while it could not afford the crew.
>
> **The fifth year survives, which is the gate finding 187 set.** Cost-only
> leaves the outage budget untouched by construction and the measurement agrees:
> 76–84 months down of 480 under both prices. Year five costs $5–9M and clears
> zero on all four seeds — but the thinnest ends at **+$2.1M against $7.3M under
> the single price**, so a young field with two more early failures than these
> would go under. That is the number any further asymmetry is spent against, and
> it is why the duration lever stays reverted.

> **R20d.26.4 amendment — the monitoring gate this section has always stated is
> implemented by a record nothing calls, and the strategy that wins is free.**
>
> §3's line — *"ConditionBased: requires monitoring tier installed (C14)"* — is
> implemented, correctly, in `MaintenancePolicy.IsDue`, which returns false for
> condition-based work without monitoring and says in its own comment why: *"a
> policy that fell back to scheduled would make the monitoring purchase free"*.
> **`MaintenancePolicy` is called from its own unit test and from nowhere else.**
> Meanwhile R20d.26.2 shipped `service-equipment` with no gate of any kind, so a
> player reads every element's condition off the chain view for nothing and
> services on it — and C14's condition-monitoring kit, whose catalogue entry is
> literally *"enables condition-based maintenance"*, is content no one needs.
>
> **Where the gate belongs, and why not in `MaintenancePolicy`.** This
> composition expresses a strategy as the player's own COMMANDS, which is §3's
> own rule read straight — "all three produce ordinary SDD-007 operations, no
> special execution path". There is no engine-side scheduler reading a declared
> policy, and there should not be one: it would be a second way to express the
> same law and L5 allows one owner per fact. So the record is **deleted** and the
> gate moves to where the decision actually is — the command validator and the
> read model.
>
> **Both halves, because either alone is incoherent:**
>
> ```text
> information   an element's CONDITION is published only where monitoring is
>               fitted; elsewhere a host is told it is unknown
> action        service-equipment is REFUSED on an unmonitored element
> ```
>
> Without the action gate the refusals leak the information the other half is
> hiding — `nothing-to-repair` tells a player the element is as-new, so
> condition is binary-searchable through rejections for the price of submitting
> commands. Without the information gate a player is shown exactly what is worn
> and then forbidden to touch it, which is a tax with a diagnosis attached.
>
> **`repair-equipment` stays ungated.** A failure needs no instrument: the plant
> stopped, and the chain view has always reported `Failed`. That is exactly
> run-to-failure, which §3 says requires nothing — so an uninstrumented company
> still has a complete, playable, and now measurably worse strategy.
>
> **NOT era-gated or technology-gated in this slice, and the reason is recorded
> here so it is not silently lost.** C14 puts the kit at E3 behind a "Condition
> monitoring" technology. In this engine **nothing can acquire a technology and
> the era never advances** — `CapabilityState.Era` is written at construction and
> by `Restore` and by nothing else, and `Acquire`/`ApplyDiffusion` are called
> only from their own tests. Gating on either today would put condition-based
> maintenance permanently out of reach, which is the cost-with-no-response shape
> findings 172 and 177 already cost this project twice. **The kit is the gate
> now; the technology and the era become gates when R20d.10 makes them
> reachable**, and that is a prerequisite recorded against R20d.10 rather than a
> gap left to be rediscovered.
>
> **State.** Monitored-or-not is a property of the component, so it is owned by
> `AssetIntegrity` beside condition and failure (L5) and captured with them —
> which also means it is one more fact riding on a save path that does not exist
> (finding 188).

## 4. Non-equipment hazards

Hydrate/wax/erosion/blowout/spill triggers evaluate their condition margins
(SDD-006 §6 flags, SDD-007 §4 disaster hook) into **threat rates**. Composition
decides the consumer:

| Composition | Consumer |
|---|---|
| With R23 | Threats enter the bow-tie; barriers (derived from §1's conditions, competency, procedure — 14 §2.2) resolve near-miss / top event |
| Pre-R23 / arcade-HSE-off | Threats resolve directly through `IHazardModel` — a complete shipped configuration (the HS-D5 "off/simple" level), not a stub |

## 4b. The bow-tie, as arithmetic (R23)

```text
Barrier strength (derived, never stored — INV10):
  s_i = min( minCondition(elements in the barrier's element set),
             crewCompetency, procedureCompliance )        each ∈ [0,1]
  — weakest-link min, the safety doctrine; competency from crew training
  state (R12.7), procedureCompliance from open-findings backlog (content map)

Threat resolution (stage 4, per materialised threat, `hazard` stream,
fixed order: threat id):
  each preventive barrier holds with probability s_i (one draw per barrier)
  all fail        → TOP EVENT → mitigating barriers sampled the same way to
                    select the consequence tier (14 §8 table)
  some fail       → NEAR MISS naming exactly the failed barriers — the free
                    warning, at the price of the draws already made
  none fail       → suppressed-threat count ++ (a leading indicator, unshown
                    by default but queryable)
Barrier independence is a stated simplification (14 §... QRA exclusion) —
the bow-tie carries the decision, not the correlation structure.
```

**ESG standing** (0–100):
`standing = 100 − Σ_k w_k · bandScore_k(intensity_k) − incidentPoints`, where
intensities are emissions/methane/flaring per unit produced scored against
content band tables, and incidentPoints decay with a content half-life —
so a clean decade genuinely rehabilitates. The lender spread table (SDD-009
§5) reads this number.

> **R20d.16 amendment. The scale, reconciled, and what is computable today.**
> This section states standing on 0–100 and `IReserveBasedLending.Redetermine`
> takes a FRACTION. They are the same number: 0–100 is the presentation scale a
> host renders and 0–1 is what crosses a contract, like every other fraction in
> the engine. Stated so the next reader does not implement a spread against 40.
>
> **Of the three terms, one has a subject.** Flaring intensity is computable now
> — the separator's gas leg goes to the flare and stage 6 already accounts what
> it burned — and it is the term that dominates a producing field's record.
> Emissions and methane need equipment that vents rather than a flare that burns,
> and `incidentPoints` needs incidents, which is R18's. The formula is not
> truncated: the missing terms contribute nothing because nothing has happened
> yet, which is the correct answer rather than a placeholder for one.
>
> **Intensity is per unit PRODUCED, which is what makes it a record rather than a
> tally.** A big field that flares more gas than a small one in absolute terms
> may be the better-run of the two, and a lender charging it more for that would
> be pricing size instead of behaviour.

> **R23.1 amendment. The intensity window is FINITE, because one of the two
> exits this section promises did not exist.**
>
> The standing was implemented against LIFETIME cumulative flaring over LIFETIME
> cumulative production — deliberately, and for a stated reason: a record is what
> a lender has watched, and one bad month should no more re-price a facility than
> one good month should clear it. The reason is right and the memory was wrong.
>
> **A lifetime average cannot fall.** By the time a field has produced for a
> decade, a month of perfect behaviour moves the ratio by about a hundredth of
> what a month of bad behaviour moved it on the way up, and nothing a player can
> buy moves it at all. `EsgStanding` documents two exits per design rule CI4 —
> *reduce the intensities, or let the incident record decay* — and only the
> second was reachable. A loop with one exit is a trap, which is precisely what
> CI4 forbids, and the code asserting otherwise in a comment did not make it so.
>
> Three things followed, and they were read as three separate balance problems
> before they were recognised as one arithmetic one. The shipped field reports
> standing 0.0000 after twenty maintained years, so SDD-009 §5's lender spread
> table has exactly one reachable row. A player who buys a gas plant to stop
> flaring sees no improvement, so the mechanic meant to reward the purchase
> rewards nothing. And `incidentPoints` is subtracted from a number already
> clamped at zero, so an incident that genuinely happened is invisible to
> everyone — which is what made R23's bow-tie look unjoinable.
>
> **So intensity is measured over a decaying window, on the SAME half-life the
> incident term already uses.** Numerator and denominator are each aged by
> `0.5^(ticks / halfLife)` before the month's own flaring and production are
> added, which is an exponentially weighted ratio: O(1) state, no ring buffer,
> and no window edge for a player to game by timing a shutdown.
>
> This KEEPS the original reasoning rather than reversing it. It is still per
> unit produced, so size is still not priced; a single month still barely moves a
> number whose half-life is measured in years. What changes is only that the
> record forgets at the same rate the incident record does — one clock for both
> terms, and the rehabilitation §4b promises is now a property of the whole
> formula rather than of half of it (finding 228).
>
> **The band edges are NOT changed by this amendment**, and the alternative of
> re-expressing the term per unit of gas HANDLED is not adopted. Both were
> candidates while the diagnosis was "the scale is mis-calibrated"; neither was
> the defect. A scale that cannot move is not mis-calibrated.

> **Amendment (finding 263) — the top event's consequence, not only its
> occurrence.** §4b's own text already says mitigating barriers are "sampled
> the same way to select the consequence tier (14 §8 table)", and
> `BowTie.Resolve` has computed `MitigatingBarriersHeld` since R23.2 — but
> `ThreatStage` (`OGSim.Composition/Modules.cs`) never read it: every top
> event charged the identical flat `Defaults.TopEventPoints`, whether every
> mitigating barrier held or none did (finding 249). A player who lets every
> mitigating barrier rot paid the same ESG bill as one who kept them all —
> which is a cost with no response, the same shape as findings 172 and 177.
>
> **14 §8's five tiers stay a future content task, not this one's.**
> Distinguishing near miss/minor/serious/major/catastrophic needs more
> mitigating barriers than this composition ships — `Defaults.Barriers`
> declares exactly one (`response`), so `MitigatingBarriersHeld` is 0 or 1 and
> can express two states, not four. Inventing four named tiers against two
> reachable states would read as more precision than the content supports.
> Instead the points scale LINEARLY with the fraction of mitigating barriers
> that held, between two content-declared bounds:
>
> ```text
> heldFraction = mitigatingHeld / totalMitigating   (0 when none exist to hold,
>                                                     which ModelFault refuses —
>                                                     a top event with no
>                                                     mitigation at all is not
>                                                     modelled by this bow-tie,
>                                                     the same refusal §4b
>                                                     already makes for a threat
>                                                     with no preventive barrier)
> points = worstCasePoints − heldFraction · (worstCasePoints − bestCasePoints)
> ```
>
> `bestCasePoints` keeps the shipped `TopEventPoints = 25.0` unchanged — a
> field whose mitigation held in full costs exactly what every top event cost
> before this amendment, so nothing already tuned against that number moves.
> `worstCasePoints = 75.0`, three times it: the same asymmetry this
> composition already prices between planned and emergency maintenance
> (`repair-equipment` at 3× `service-equipment`, R20d.26.2) for the same
> reason — a consequence nothing stood against costs more than one every
> defence somewhat blunted. With one mitigating barrier this reaches exactly
> its two ends (25.0 held, 75.0 failed); a second mitigating barrier would
> land a real interior point on the same line without a code change.

> **Amendment (finding 273) — the top event costs real cash now, the same
> line in dollars.** Finding 263 priced how much a top event costs the ESG
> record; nothing priced what it costs the company directly. `EsgStanding`
> has no `Money` field and never did, and design 08 §7 ED6's own insurance
> decision — "insurable classes with premiums rated on the player's record"
> — has nothing to insure without one: `min(loss − deductible, limit)`
> needs a loss, and this composition produced none.
>
> **The SAME straight line finding 263 already drew, priced instead of
> pointed:**
>
> ```text
> loss = worstCaseLoss − heldFraction · (worstCaseLoss − bestCaseLoss)
> ```
>
> `bestCaseLoss = $2.0M`, `worstCaseLoss = $6.0M` (the SAME 3× asymmetry
> `worstCasePoints`/`bestCasePoints` already use, reused rather than a
> second invented ratio) — a well-contained loss of containment for a field
> this size: cleanup, regulatory response, investigation, some downtime.
> Scaled to the shipped scenario's own economics (opening cash $50M, a
> $600M/decade target) rather than a real-world catastrophic outlier — this
> bow-tie's single threat and single mitigating barrier span roughly design
> 14 §8's "Serious" to "Major" tiers, not "Catastrophic," the same
> two-reachable-states boundary finding 263 already named.
>
> **Posted as `Account.Opex` against `Account.Cash`, `MovementCategory.
> Operating`** — no new ledger vocabulary for the loss itself, the same
> reason the take-or-pay penalty needed none (finding 250): an incident's
> cleanup and response cost is an operating cost, which is what design 14
> §8's own "Response is an `IOperation`" line already says, even though the
> operation itself stays unbuilt (S012's own open item, not this
> amendment's to close).
>
> **The loss is real whether or not a policy exists** — the same way an
> uninsured operator still pays for a real spill. Insurance (SDD-009 §7's
> finding-273 amendment) is what a STANDING policy does about it, priced
> separately and reading this figure rather than re-deriving it.

**Social licence** (0–100): `SL += Σ driver deltas` per tick, clamped;
driver deltas are content per event class (visible flaring near settlements,
spills scaled by sensitivity, local employment, community investment).
Permitting time/probability tables read SL bands (R16 §5).

## 5. Souring (the long-arc hazard)

```text
Truth-side: H2S_ppm(t) = sourCurve( cumulativeInjectedWater / PoreVolume )
per waterflooded compartment; curve content per rock type. Rising H2S enters:
the fluid composition (sales spec), §1's sourFraction severity, and the
metallurgy envelope check — the DHS3 decision arriving on schedule, years late.
```

> **R20d review amendment (finding 147) — the shape, declared.**
>
> ```csharp
> public interface ISouringModel
> {
>     ContentId Id { get; }
>     double HydrogenSulphidePpm(ContentId rockType, double injectedWaterOverPoreVolume);
> }
> ```
>
> Monotonic in the ratio, and that is pinned: water already injected cannot
> un-sour a reservoir, so a curve that dipped would be a content error the
> loader refuses, not a modelling choice. Truth-side it stays — what a player
> knows about souring arrives through produced-fluid samples, like everything
> else.

> **R20d.25 amendment (finding 182) — it is IMPORTED water, not injected water,
> and the difference is the whole model.**
>
> The line above says `cumulativeInjectedWater / PoreVolume`. Building souring
> against that produced a mechanic that could not fire, and the reason is not
> that the number was small — it is that **produced water is the wrong fluid**.
> Reinjected produced water has already been through the reservoir: it is
> anoxic, reduced, and stripped of the sulphate the bacteria eat. It is the
> fluid that sours a reservoir LEAST. Sea water carries roughly 2,700 ppm of
> sulphate, and seawater flooding is what actually sours fields.
>
> The measurement said the same thing twice over. A field that only reinjects
> what it makes puts **0.0033 pore volumes** through in forty years, against the
> 0.1–1 PV of a real flood — three orders of magnitude short — and it makes
> almost none of it early, which is when a flood would be doing its souring. So:
>
> ```text
> H2S_ppm(compartment) = sourCurve( cumulativeIMPORTEDwater / PoreVolume )
> ```
>
> R20d.24's waterflood is what supplies the numerator, and it supplies **0.18
> PV** on a field flooded at VRR 1 — inside the real band, so the curve fires at
> honest content instead of at a constant bent a hundredfold to make a feature
> appear (which is finding 175's defect with the derivation written afterwards).
>
> **The compartment carries it, so `CompartmentWithdrawal` gains `Imported`.**
> Souring is per compartment because the pore volume is, and stage 6 already
> splits a tick's injection two ways — produced water pro rata by the water each
> compartment made, imported water pro rata by voidage (SDD-003 §3.1d's R20d.24b
> amendment §3). The second of those two numbers is exactly what this needs, and
> it is already computed.
>
> **`SourFraction` is NORMALISED, like every other term in `ServiceSeverity`.**
> `WaterCut` and `DutyFraction` are fractions of one; `OverTemperature` is
> divided by a span in §0's own declaration. A raw H2S mass fraction would sit
> at 1e-3 on a scale where the base term is 1, so `SourFactor: 2.0` would mean
> nothing and only a coefficient of order 1,000 could rescue it — a number whose
> only job is to undo a unit choice. It is therefore ppm against a **souring
> reference**: the concentration at which this model treats the service as fully
> sour, clamped at one.
>
> **What this phase delivers, and what it deliberately does not.** §5's list has
> three destinations; this is the first. §1's `sourFraction` severity — sour
> fluid eats the plant, so a company that floods hard pays for it in maintenance
> twenty years later. The other two, the sales spec and the metallurgy envelope,
> both need H2S as a MATERIAL and are their own task.
>
> That is not half a mechanic, and the test is lesson 2 — *a cost with no
> response is a tax rather than a decision*. The response to a soured field is
> the flood decision itself, taken twenty years earlier and now visible in the
> maintenance bill: flood a field that needed it and the recovery pays for the
> corrosion; flood one the aquifer already supported and there was never
> anything to gain. Repair is the near-term answer and it already exists, priced
> and strategic (R20d.22).
>
> **Members this adds** (rule F-1). `ISouringModel` moves from this document
> into `OGSim.Contracts`, as every replaceable model does (03 §3.2).
> `OGSim.Integrity.SaturatingSourCurve : ISouringModel` implements
> `ppm = ultimate·r/(half + r)`, validating both parameters positive — a
> half-ratio of zero is a step function and an ultimate of zero is a curve that
> does nothing. `CompartmentWithdrawal` gains `Imported`;
> `ReservoirCompartment` accumulates it and `SubsurfaceState` answers
> `TrueSourFractionOf`. `AssetIntegrity.Advance` takes the field's sour fraction
> beside its water cut, and `FieldReadModel` reports it — a player who could not
> see their reservoir souring would experience it as equipment that inexplicably
> started breaking.

## 6. Test mapping

R18-V1 (decay law with declared factors) · V2 (exponential hazard shape — no
threshold behaviour) · V3/V4 (absence + attribution via SDD-002) · V5
(strategy outcomes over long runs) · V6 (mitigations lower the specific term)
· V7 (fixed-order draws ⇒ determinism) · V8 (audit tuple) · V9 (souring curve
→ severity → envelope) · V10 (restore ops) · HS1/HS2 consume §1–§2 through the
bow-tie.

## 7. Open items

| # | Item | Trigger |
|---|---|---|
| S012-1 | Collateral damage on failure (a failed compressor damaging adjacent units) — currently absent; add as a bow-tie consequence class if SC7 feels too clean | R20 balance |
| S012-2 | Per-component vs per-class draw batching if the per-tick draw count matters | R18 benchmarks |
