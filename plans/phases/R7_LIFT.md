# Phase R7 — Artificial Lift

**Arc II** · Status ⬜ · Depends on: R6 · Enables: R18

---

## 0. Purpose

R6 ends with wells that die when reservoir pressure can no longer lift fluid to
surface. R7 is the answer to that, and it is one of the game's best capital
decisions: **spend now to keep a well alive, or let it go.**

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Lift modifies the VLP, not the rate | Each method shifts the outflow curve; the new rate is still the IPR ∩ VLP intersection |
| G2 | A dead well can be revived | A well failing R6-V6 flows again after an appropriate lift installation |
| G3 | Each method has a real envelope | Installing an unsuitable method underperforms or fails — it is not simply a weaker upgrade |
| G4 | Lift has running costs | Power, maintenance and failure rates are modelled, not just capital |

---

## 2. Design decisions

### 2.1 Lift is an `IWellComponent` that implements `ILiftMethod`

It lives on the completion, is installed and pulled by a workover operation, and
degrades like any other component.

### 2.2 Envelopes are declared, and violating one is not a hard block

Each method declares its operating envelope: rate range, depth range, deviation
tolerance, gas fraction tolerance, temperature limit, solids tolerance.

**Decision: operating outside the envelope degrades performance and raises the
failure hazard — it does not refuse installation.**

*Rationale:* the interesting failure is the player who installs an ESP in a gassy
well, sees good rates for eight months, and then loses the pump. A hard block
teaches nothing; a consequence teaches the envelope. **The information is
available before installing** — this is a trap the player can avoid by reading.

### 2.3 Gas lift couples to the gas system

Gas lift consumes compressed gas, so it competes with sales gas and depends on
compression that R9 builds. Until R9, R7 models the injection gas as an external
supply with a cost; **R9 connects it to the real gas system** and the coupling
becomes physical.

This is a declared, temporary external boundary — not a stub. It is a complete,
tested model of purchased lift gas, which remains a legitimate option after R9.

### 2.4 ESP power draw is real

ESPs consume significant power, feeding the facility power balance (R8.8). A
field that installs ESPs on twenty wells needs generation capacity, and if it
does not have it, **something else goes offline.** That coupling is the point.

### 2.5 Events and couplings

Lift installation and failure both change network topology, so both create
**segment boundaries**. ESP power draw feeds the facility power balance at tick
stage 4, which means an ESP fleet can take *other* equipment offline — the
coupling resolves before the solve, not after.

Envelope violation raises a `W` event rather than blocking installation
(section 2.2), making it a warning the player can act on rather than a refusal
they must work around.

---

## 3. Deliverables

`OGSim.Wells` extension: `ILiftMethod`, gas lift, ESP (pump curve, power, gas
sensitivity), rod pump, PCP; envelope declaration and evaluation; a lift-selection
advisory that matches well conditions against envelopes.
Content: `lift-method` catalogue with real equipment ranges.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R7-V1 | VLP shift | Each method lowers the outflow curve by the amount its model predicts |
| R7-V2 | Revival | A well that cannot flow naturally flows after lift installation, at the new intersection |
| R7-V3 | ESP pump curve | Head versus rate matches the catalogue curve |
| R7-V4 | Gas lift optimum | Injection rate has an optimum — too little does not lighten the column, too much adds friction |
| R7-V5 | Envelope violation | Out-of-envelope operation degrades performance and raises the hazard rate, and is reported |
| R7-V6 | Rod pump ceiling | Rate is capped by displacement, independent of reservoir deliverability |
| R7-V7 | Power coupling | ESP installation increases facility power demand; a shortfall takes equipment offline |
| R7-V8 | Economics | Lift capital and running costs flow to the cost ledger *(deferred assertion until R13)* |

---

## 5. Out of scope

The workover *operation* that installs it (R12) — R7 installs directly in tests.
Failure and degradation (R18). Optimisation advice beyond envelope matching.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Gas lift's external gas supply becomes a permanent shortcut | R9 has an explicit task (R9.6) to connect it; tracked as a dependency, not a TODO |
| Lift models are individually simple and become "just a multiplier" | Each must shift the VLP curve, verified by R7-V1; a rate multiplier would fail it |
| Four methods is a lot of surface for one phase | They share `ILiftMethod` and differ only in how they alter the outflow curve; the shared harness is written once |
