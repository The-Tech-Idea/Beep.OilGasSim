# Phase R25 — Advisor and Reality Profiles

**Arc IV · Executes after R21** · Status ⬜
Depends on: R21 · Enables: the non-engineer audience

---

## 0. Purpose

Make the game playable by someone who has never heard of an IPR curve, without
building a second, simpler game. The design is
[18_GAME_MODES](../design/18_GAME_MODES.md) §5b: three independent axes —
fidelity, assists, forgiveness — with the **Advisor** as the assist axis's
engine: a flight-sim autopilot for an oil company.

**R25 executes after R21 by necessity:** the Advisor is architecturally a
client. It consumes the host surface R21 publishes and nothing else — the
reference client, generalised.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | No engine assist branches | Architecture test: no simulation assembly references an assist level or the Advisor (GM14) |
| G2 | The Advisor is a pure client | It reads only the R21 surface and acts only through the command bus; at *Advise* level a run is digest-identical to one without it (GM15) |
| G3 | Assists never leak truth | The Advisor cannot know more than the player — structurally, because the read model is belief-built |
| G4 | Per-domain, per-level, mid-game | Manual / Advise / Confirm / Auto per decision domain, changeable at any time |
| G5 | Every recommendation explains itself | Reasoning in domain terms, from read-model data the player can see |
| G6 | Judgement is never automated | Exploration and sanction decisions are capped at *Advise* (the cap is Advisor policy, tested) |
| G7 | Scores are honestly labelled | Every score carries its reality profile; preset changes are logged (GM17) |

---

## 2. Design decisions

### 2.1 The Advisor lives outside the engine — the whole design in one rule

`OGSim.Advisor` references only the composition surface (`IEngine`, the read
model, the command bus, the audit query API). To the engine, an Advisor command
*is* a player command: validated, audited, replayable.

*Rationale:* every alternative — assist flags in the engine, simplified paths,
difficulty branches — creates the class of bug where the assisted game and the
real game quietly diverge. This design makes divergence impossible: **there is
only one game**, and the Advisor is a hand on the same controls.

### 2.2 Determinism without a new mechanism

The Advisor is a pure function of (read model, configuration): no RNG, no wall
clock. Replay does not even need to know it existed — commands were recorded on
the bus like any others. Save/load stores only its configuration and pending
proposals.

### 2.3 The recommendation pipeline

Per domain, each tick close: read the projections → evaluate the domain's
decision rules (the same arithmetic the design already specifies: economic
limit, envelope matching, value of information, bottleneck ranking) → emit
recommendations with reasoning → act on them per the domain's level.

**The rules are content** (`advisor-rule` definitions bound to read-model
fields), so tuning the Advisor's judgement is a content change, and modders can
write their own advisors.

### 2.4 The judgement cap is policy, not capability

The Advisor *computes* bid valuations and prospect rankings — it must, to
advise. It *refuses to act* on DEX2/3/6/7/10 and DDV1 even at *Auto*.

*Rationale:* per PD-D2's line — automate arithmetic, never judgement. A game
whose central bets place themselves is a screensaver. The cap is enforced in the
Advisor and verified by test, not merely documented.

### 2.5 Forgiveness is composition, not Advisor behaviour

The forgiveness levers ([18](../design/18_GAME_MODES.md) §5b.4) are model and
content selections applied at composition — hazard model choice, lender
content, licence terms, price model. R25 wires the profile to the selections;
it adds no mechanisms.

### 2.6 Reasoning strings are the tutorial

Every recommendation carries: the trigger ("well W-014 died at tick 214"), the
diagnosis ("IPR ∩ VLP has no intersection at current reservoir pressure"), the
proposal ("install an ESP — envelope fits: rate, depth, water cut"), and the
trade ("₤2.1M capital, ~14 months payback at current prices"). All four from
read-model fields the player can inspect.

*Rationale:* [18](../design/18_GAME_MODES.md) §5b.3 — the *Advise* level is the
tutorial that never ends. A player who reads the reasoning becomes the engineer.

---

## 3. Deliverables

`OGSim.Advisor`: domain agents for the eight decision domains, recommendation
pipeline with reasoning, per-domain levels, the judgement cap, proposal queue
for *Confirm*. `IRealityProfile` content type, preset definitions
(Story / Tycoon / Engineer / Simulation), forgiveness wiring, score stamping.
Content: `advisor-rule` catalogue, `reality-profile` presets.

---

## 4. Verification

GM14–GM17 from [18](../design/18_GAME_MODES.md) §7, plus:

| # | Test | Passes when |
|---|---|---|
| R25-V1 | Client purity | Architecture test: `OGSim.Advisor` references only the composition surface |
| R25-V2 | Digest identity at Advise | A scripted run with the Advisor advising equals the same run without it, byte for byte |
| R25-V3 | Auto plays a full game | At full Auto (with the judgement cap), the Advisor operates a discovered field from development to abandonment without player input — the flight-sim "watch the autopilot fly" test |
| R25-V4 | Judgement cap | At Auto, DEX2/3/6/7/10 and DDV1 produce proposals, never commands |
| R25-V5 | Mid-game level change | Switching a domain Manual↔Auto mid-game is seamless and logged |
| R25-V6 | Reasoning completeness | Every recommendation's four reasoning parts reference resolvable read-model fields |
| R25-V7 | Preset equivalence | A preset produces exactly the model selections, levels and levers its content declares |
| R25-V8 | Profile stamping | Scores carry the profile; a mid-game preset change appears in the score record |
| R25-V9 | Determinism | Same read model + configuration → same recommendations, across platforms |
| R25-V10 | Advisor rules as content | A modified `advisor-rule` changes behaviour with no engine or Advisor code change |

**R25-V3 is the phase's acceptance test.** If the Advisor can run a field on its
own, then every level between "watch" and "manual" is guaranteed to be playable —
the player can always hand any domain back to it.

---

## 5. Out of scope

Any UI for the Advisor (host concern — R25 ships recommendations as data).
Advisor personality/voice (GM-D8, host concern). Difficulty tuning of content
(R20). Machine-learned advice — the rules are explicit and inspectable by
design, because a recommendation the designer cannot explain teaches nothing.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| The Advisor becomes a shadow engine — duplicating logic that then drifts | It evaluates *read-model fields* the engine already publishes (economic limit, envelopes, deferred volumes); it computes nothing the engine computes. R25-V6 keeps every input resolvable |
| Auto mode plays better than the player and feels deflating | GM-D7: it plays competently but refuses the judgement calls that decide the game's outcome — the player's edge is exactly the part that matters |
| Reasoning strings rot as the read model evolves | They are templated on read-model fields; R25-V6 fails on a dangling field |
| Per-domain levels overwhelm the settings screen | Presets are the surface; Custom is behind one door (18 §5b.5) |
