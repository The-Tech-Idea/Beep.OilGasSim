# SDD-015 — The Advisor

**Status:** drafted · **Serves:** R25 · **Design docs:** [18](../design/18_GAME_MODES.md) §5b, [R25](../phases/R25_ADVISOR.md)

The autopilot as code shape: a rule engine over the read model that issues
commands — reusing SDD-014's predicate AST, so the accessibility layer adds
almost no new machinery.

---

## 1. Placement and purity

`OGSim.Advisor` references **only** the composition surface (R25 §2.1). Pure
function of `(read model, configuration)`: no RNG, no clock, no state beyond
its configuration and open proposals. GM15's digest-identity test follows from
this shape, not from care.

## 2. Advisor rules — content, on the same AST

```csharp
public sealed record AdvisorRule(
    ContentId Id, DecisionDomain Domain,
    Predicate Trigger,                        // SDD-014 §1 AST — REUSED, one predicate engine
    CommandTemplate Proposal,                 // command type + parameter bindings (§3)
    ReasoningTemplate Reasoning);             // §4
```

**Reusing the objective AST is the design's economy**: triggers get load-time
`ReadModelPath` validation for free (SDD-014 §2), so an advisor rule can never
reason from data the player cannot see — GM-level fairness by construction.

## 3. Proposals

```text
CommandTemplate binds command parameters to read-model paths and content
lookups, e.g. rule "dead-well-lift":
  Trigger: OnEvent(well.diedNaturally) ∧ envelope-fit exists in catalogue
  Proposal: InstallLiftCommand(well ← event.subject,
            tier ← cheapest catalogue tier whose envelope fits well's
                   (rate-estimate, depth, GOR, waterCut, temperature))
"Cheapest fitting tier" and similar selectors are a closed selector vocabulary
(Cheapest | HighestMargin | FirstFitting) — no scripting language, no Turing
trap; a selector the vocabulary lacks is an SDD change (rule F-1's spirit).
Ordering (determinism): proposals sorted (Domain, RuleId, SubjectId).
```

## 4. Reasoning — four bound parts (R25 §2.6)

`Trigger` (the event/condition, rendered), `Diagnosis` (bound read-model
facts), `Proposal` (the command, rendered), `Trade` (cost from the catalogue
entry; payback = simple division of bound fields). All four are **path-bound
templates** — R25-V6's "every part resolvable" is the same registry validation
again. Localised via `$loc:` ids (SDD-004).

## 5. Levels and the cap

```text
Per DecisionDomain level ∈ {Manual, Advise, Confirm, Auto} (config, mid-game
changeable, logged). Auto: submit via the command bus; Confirm: queue for the
host; Advise: publish recommendation only —
directly to the host: Advisor output lives BESIDE the read model, never inside
it (the engine's ReadModel carries no AdvisorView — SDD-017 §2).
THE JUDGEMENT CAP is data + an assert: cappedDomains = {ExplorationJudgement,
Sanction} ship in engine constants (not content — modders may add rules, not
uncap judgement); any Auto-submit whose command type maps to a capped decision
throws in strict policy and clamps to Advise in resilient (R25-V4).
```

## 6. Reality profiles

`reality-profile` content binds: model selections (fidelity), Advisor levels
per domain, forgiveness lever selections, alert profile — applied at
composition / on preset change (logged, stamped into scores — SDD-014 §4 note,
GM17).

## 7. Test mapping

R25-V1 (client purity — architecture) · V2 (digest identity at Advise) · V3
(full-Auto plays a field: the reference-client script driven by rules alone) ·
V4 (§5 cap) · V5 (level changes) · V6 (§4 binding) · V7 (profile equivalence)
· V8 (stamping) · V9 (§3 ordering ⇒ cross-platform identity) · V10 (rule
content swap changes behaviour with no code change).

## 8. Open items

| # | Item | Trigger |
|---|---|---|
| S015-1 | Selector vocabulary sufficiency across all eight domains — audit against the 61-decision catalogue during rule authoring | R25.3 |
| S015-2 | Advisor explanation of *inaction* ("why is nothing proposed?") — likely a host rendering of trigger states | R25 review |
