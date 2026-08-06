# Phase R13 — Economics

**Arc III** · Status ⬜ · Depends on: R11, R12 · Enables: R16, R20

---

## 0. Purpose

Money: the constraint that makes every other decision matter. R13 closes the loop
from custody transfer to cash to the next well, and introduces the game's real
scoreboard — **reserves**.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Cash is conserved | Every movement has a counterparty; balance = opening + all movements, asserted every tick (INV2) |
| G2 | Lifting cost scales with gross liquid | Cost per barrel of oil at 90% water cut is roughly ten times that at 10% (MB6) |
| G3 | Fiscal regimes change the project | The same field has materially different economics under royalty/tax versus PSC |
| G4 | Reserves are derived and price-sensitive | A price crash writes down reserves, which shrinks borrowing capacity (SC6) |
| G5 | RRR is the headline metric | Computed each period and reported as the primary score |
| G6 | Abandonment is provisioned from first production | The liability accrues per barrel; it is never a surprise |

---

## 2. Design decisions

### 2.1 Full accrual accounting, not just cash

Per open decision ED1: cash, P&L and balance sheet. Reserve-based lending needs a
reserves-backed borrowing base; tax needs depreciation and allowances;
abandonment needs a provision. **None of these is expressible in a cash-only
model**, and they are the difference between a tycoon game and a business
simulation.

### 2.2 Revenue originates only at custody transfer

Enforced structurally: `ITreasury` accepts revenue only from an
`ICustodyTransferPoint` event. There is no other path.

*Rationale:* it makes the double-credit class of bug impossible, and it keeps the
physical and financial models honest with each other.

### 2.3 Fiscal regime is a plugin over the revenue split

Four shipped: royalty/tax, PSC with cost recovery cap, service contract, sliding
scale. Each takes gross revenue plus the cost history and returns the company's
share.

*Rationale:* the regime is a legitimate difficulty and flavour axis, and the same
discovery being a different project under each is real, teachable and interesting.

### 2.4 Reserves are computed, never stored

Each tick, from beliefs + development plan + current prices + economic limit.
Classes per SPE-PRMS.

**Consequence, and it is the point:** a price crash mechanically writes down
reserves → shrinks the borrowing base → forces capital discipline. That chain
reaction emerges from definitions rather than from a scripted event, and the
player feels a market shock transmit into their company within a tick or two.

### 2.5 Insolvency leads to restructuring, not a game-over screen

Per open decision ED2: asset sales, forced farm-outs, debt restructuring,
takeover. A fire-sale ending is more interesting, more instructive and more
recoverable than a modal dialog.

### 2.6 Cost inflation tracks the price cycle

Per open decision ED4. **Costs boom exactly when prices boom** — which is why the
industry reliably destroys value at cycle peaks. Rarely modelled, cheap to
implement, and one of the most useful lessons the game can teach.

### 2.7 Costs this phase accounts for but does not produce

| Cost | Produced by | Note |
|---|---|---|
| Weather standby | [R22](R22_ENVIRONMENT.md) / R12 | Accrues without progress |
| Carbon price | [R23](R23_HSE.md) | Rises across a long campaign (HS-D3) |
| HSE programme | [R23](R23_HSE.md) | Barrier testing and inspection |
| Environmental liability | [R23](R23_HSE.md) | Accrued, not merely fined |
| Abandonment provision | R12 / setting | **Offshore is a large multiple of onshore, per barrel, from first production** |

### 2.8 ESG is an input to the cost of capital

The borrowing *base* comes from reserves; the borrowing *rate* comes from ESG
standing ([14_HSE](../design/14_HSE.md) section 7). This closes the slowest loop
in the game, and under rule IR1 it obliges a **leading indicator published every
tick** — ESG standing plus its current rate effect.

### 2.9 Events this phase raises

`finance.cashThreshold` · `.covenantRisk` · `.borrowingBaseChanged` /
`.insolvencyRisk` · `market.priceMove` · `.shock` · `contract.*` /
`reserves.replacementRatio` · `well.economicLimit`.

**`reserves.replacementRatio` below 1.0 is the liquidation spiral's entry
event** — an annual `N`-severity notice that must nonetheless be impossible to
miss, because the loop it announces takes one to three years to become fatal.

---

## 3. Deliverables

`OGSim.Company`: `ICostLedger`, `IPriceModel` plugins, quality and location
differentials, `ISalesContract` (spot, term, take-or-pay, hedge),
`IFiscalRegime` (×4), `ITreasury` (cash, debt, equity, reserve-based lending),
P&L and balance sheet, `IReservesBooking` and RRR, economic-limit detection,
abandonment provision, `IWorkingInterest` and farm-outs, insolvency and
restructuring.
Content: `fiscal-regime`, `contract-template`, cost catalogues, price model
parameters.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R13-V1 | Cash conservation (INV2) | Holds every tick over full-lifecycle runs |
| R13-V2 | Revenue provenance | Architecture test: revenue enters `ITreasury` only from a custody-transfer event |
| R13-V3 | Lifting cost (MB6) | ~10× at 90% water cut versus 10% |
| R13-V4 | Fiscal comparison | The same field under four regimes yields four materially different NPVs, each matching a hand calculation |
| R13-V5 | PSC cost recovery | The cap behaves correctly; unrecovered cost carries forward |
| R13-V6 | Reserves classes | 1P/2P/3P computed from belief percentiles and the economic limit |
| R13-V7 | Price sensitivity (SC6) | A 60% crash writes down reserves and shrinks the borrowing base |
| R13-V8 | RRR | Correct for known addition and production sequences |
| R13-V9 | Abandonment provision | Accrues per barrel; the accumulated provision covers the eventual cost |
| R13-V10 | Take-or-pay penalty | Under-delivery triggers the contractual penalty |
| R13-V11 | Farm-out | Cost and revenue split by working interest, summing to 100% |
| R13-V12 | Insolvency | Triggers restructuring, not termination; assets are disposed of at a discount |
| R13-V13 | Cost inflation | Costs rise with the price cycle at the declared elasticity |

---

## 5. Out of scope

Licence bidding (R16). Technology costs (R17). Multi-currency (open decision ED3,
declined). Player-declarable reserves (open decision ED5, deferred).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Accrual accounting is a large surface | It is mostly bookkeeping with clear rules; R13-V1's conservation invariant catches errors immediately |
| Reserves computation is expensive per tick | Compute quarterly rather than per tick, and on demand after material events; cache with explicit invalidation |
| Fiscal regimes are hard to verify | Each is verified against a hand-computed worked example committed as a fixture |
| Economics tuning makes the game trivially easy or impossible | Band tests (MB6, MB7) plus the SC1 acceptance scenario constrain the space |
