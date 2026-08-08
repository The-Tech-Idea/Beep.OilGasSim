# 19 — Glossary

**Status:** draft · **Date:** 2026-08-06

> **Affects:** 01, 02, 10, phases — a term enters here before it enters any other document (N7) · **Affected by:** everything — bidirectional by design
> *(strong couplings only — the full row is in [22](22_DESIGN_COHERENCE.md) §2)*

A design tool, not a player manual. Its job is **naming discipline**: one term,
one meaning, one contract. Where the industry uses a word loosely, this document
picks the strict meaning and the whole design uses it.

**Rule: if a concept is in [01_CONCEPT_MATRIX](01_CONCEPT_MATRIX.md), its name
here is the name used in code, content and UI text.** No synonyms.

---

## Terms the industry uses loosely — our strict meanings

These six cause the most confusion and are pinned first.

| Term | **Our strict meaning** | Commonly also means | Contract |
|---|---|---|---|
| **Well** | The surface/regulatory entity: one surface location, one name, one licence | "the hole", "the producing string" | `IWell` |
| **Wellbore** | A physical hole. The original hole and each sidetrack are separate wellbores | "well" | `IWellbore` |
| **Completion** | The producing configuration on a wellbore | "the act of completing", "the perforations" | `ICompletion` |
| **Reservoir** | The rock volume holding an accumulation | "field", "pool", "the whole asset" | `IReservoir` |
| **Compartment** | A hydraulically connected volume — **the unit pressure is simulated on** | *(rarely distinguished)* | `IReservoirCompartment` |
| **Facility** | A site and container. **Owns no process behaviour** | "a processing plant" | `IFacility` |

---

## A — Subsurface and geology

| Term | Meaning |
|---|---|
| **Accumulation** | Hydrocarbons collected in a trap. Present or absent — the thing exploration looks for |
| **API gravity** | Density scale: `141.5/SG − 131.5`. Higher is lighter and usually worth more. **Nonlinear — never average two API values** |
| **Aquifer** | A water body connected to a reservoir, providing pressure support and eventually water |
| **Basin** | A sedimentary region; the largest exploration unit |
| **Bubble point (`Pb`)** | The pressure at which gas begins to come out of solution. **The most consequential number in a reservoir** |
| **Charge** | Whether hydrocarbons actually migrated into a trap |
| **Compartmentalisation** | A reservoir divided into pressure-isolated blocks. Usually discovered from data, not known in advance |
| **Condensate** | Light liquid that drops out of gas as pressure falls |
| **Contact** | The surface between fluids: gas-oil contact, oil-water contact. Moves during production |
| **Drive mechanism** | How a reservoir maintains pressure as fluid is withdrawn. Determines recovery factor |
| **Field** | An administrative grouping of one or more reservoirs plus surface infrastructure |
| **Formation volume factor (`Bo`, `Bg`)** | Reservoir volume per surface volume. Why a reservoir barrel is not a stock tank barrel |
| **GOR** | Gas-oil ratio. Rises sharply once pressure falls below the bubble point |
| **Lead** | An immature prospect needing more data |
| **Net pay (`h`)** | The thickness of rock that will actually produce |
| **OOIP / STOOIP / GIIP** | Oil or gas initially in place, before any recovery factor |
| **Permeability (`k`)** | How easily fluid flows through rock. Millidarcies |
| **Petroleum system** | Source, reservoir, seal, trap, timing — **all five must work** |
| **Play** | A family of prospects sharing a geological model. **Succeed and fail together, partially** |
| **Porosity (`φ`)** | Fraction of rock that is void space |
| **Pool** | A single hydrocarbon accumulation. In this design, folded into reservoir/compartment |
| **Prospect** | A mapped, drillable target |
| **`p/Z`** | Pressure over gas compressibility factor. Plotted against cumulative production it is a straight line for a volumetric gas reservoir — **so the player can deduce gas in place** |
| **Recovery factor (RF)** | Fraction of in-place hydrocarbon produced. **Emergent, never stored** |
| **Saturation (`Sw`, `So`, `Sg`)** | Fraction of pore space occupied by each fluid |
| **Seal** | Impermeable rock preventing escape |
| **Source rock** | Organic-rich rock that generated the hydrocarbons |
| **Trap** | A geometry that collects migrating hydrocarbons |
| **Water cut** | Fraction of produced liquid that is water. **Rises through field life; kills most fields** |
| **Z-factor** | Gas deviation from ideal behaviour |

---

## B — Wells and drilling

| Term | Meaning |
|---|---|
| **Appraisal well** | Drilled after a discovery to size and delineate it |
| **Artificial lift** | Any method of helping fluid to surface once natural flow is insufficient |
| **Choke** | Surface restriction controlling flow. In critical flow, rate is independent of downstream pressure |
| **Christmas tree** | The valve assembly at the wellhead |
| **Deviation survey** | The measured path of a wellbore |
| **Drawdown** | `Pr − Pwf`. The pressure difference driving inflow |
| **Dry hole** | A well finding no commercial hydrocarbons. **Must produce a diagnosis of which element failed** |
| **ESP** | Electric submersible pump. High rate, high water cut, power-hungry, gas-intolerant |
| **Gas lift** | Injecting gas to lighten the fluid column |
| **IPR** | Inflow Performance Relationship. Rate versus bottomhole flowing pressure, from the reservoir side |
| **Operating point** | Where IPR meets VLP. **The well's actual rate. No intersection means the well is dead** |
| **`Pwf`** | Bottomhole flowing pressure |
| **Perforation** | The connection between reservoir and wellbore. Can be isolated individually |
| **Productivity index (`J`)** | Rate per unit drawdown |
| **Recompletion** | A new completion on an existing wellbore, often into a different zone |
| **Rod pump** | Mechanical lift. Low rate, shallow, late life |
| **Sidetrack** | A new wellbore kicked off from an existing one |
| **Skin (`s`)** | Near-wellbore damage (positive) or stimulation (negative). **+10 costs roughly half of productivity** |
| **Spud** | To begin drilling |
| **Stimulation** | Acidising or fracturing to reduce skin |
| **Tubing** | The pipe production flows up. **Too narrow: friction-limited. Too wide: the well loads up and dies** |
| **VLP** | Vertical Lift Performance. Pressure needed at bottomhole to deliver to surface |
| **Wildcat** | An exploration well in unproven territory. Success rate typically 10–35% |
| **Workover** | An intervention on an existing well |

---

## C — Surface, processing and transport

| Term | Meaning |
|---|---|
| **Associated gas** | Gas produced with oil, whether wanted or not. Sell it, re-inject it, or flare it |
| **Backpressure** | Downstream pressure propagating upstream and reducing rate. **Reaches all the way to the reservoir** |
| **Battery** | A group of tanks and treating equipment at a lease |
| **BS&W** | Basic sediment and water. The contaminant limit in export crude |
| **Custody transfer** | The metered, contractual change of ownership. **The only revenue event in this design** |
| **Dehydration** | Removing water from gas to meet dewpoint spec |
| **Erosional velocity** | The flow speed above which a pipe begins to erode |
| **Facility unit** | One process unit. **All process behaviour lives here, never in the facility** |
| **Flare** | Controlled burning of gas that cannot be sold or re-injected |
| **Flow assurance** | Keeping fluids flowing: hydrates, wax, scale, corrosion, slugging |
| **Heater-treater** | Breaks emulsion and removes water from oil |
| **Hydrate** | An ice-like solid forming from gas and water at low temperature and high pressure. Blocks lines |
| **LACT** | Lease automatic custody transfer — an automated metering skid |
| **Linefill** | Material inside a pipeline. Real, owned inventory |
| **NGL** | Natural gas liquids: ethane, propane, butane, pentanes+ |
| **Sales gas** | Gas meeting pipeline specification |
| **Separator** | Splits a stream by phase. **Has two independent capacities — gas and liquid** |
| **Slug catcher** | Absorbs intermittent liquid surges from a pipeline |
| **Sour** | Containing H₂S. Requires special metallurgy and sweetening |
| **Specification** | Limits a stream must meet at a point. **Off-spec does not pass** |
| **Stabilisation** | Removing light ends so crude meets vapour-pressure spec |
| **Sweetening** | Removing H₂S and CO₂, usually with amine |
| **Ullage** | Space remaining in a tank. **When it reaches zero, wells shut in** |
| **Vapour recovery** | Capturing tank vapours instead of venting them. Cuts emissions and adds revenue |

---

## D — Company, economics and information

| Term | Meaning |
|---|---|
| **Abandonment / decommissioning** | Plugging wells, removing facilities, restoring sites. **Provisioned from first production** |
| **Belief** | What the player knows, as a distribution with provenance. **Decisions are made on beliefs, never on truth** |
| **Borrowing base** | Lending capacity determined by booked reserves. The link that makes discovery immediately valuable |
| **Chance of success (POS)** | `P(source) × P(reservoir) × P(seal) × P(trap) × P(timing)` |
| **Contingent resources** | Discovered but not currently commercial |
| **Cost oil / profit oil** | Under a PSC: production allocated to cost recovery, then to the profit split |
| **Economic limit** | Where incremental revenue falls below incremental cost. **The abandonment trigger** |
| **Farm-out** | Selling part of a licence interest to fund work. The small company's survival tool |
| **Fiscal regime** | How revenue splits between company, state and partners |
| **Lifting** | Loading a cargo. Also, "lifting cost" — cost to produce a barrel. **Scales with gross liquid, not oil** |
| **Netback** | Realised price less transport and processing |
| **1P / 2P / 3P** | Proved / proved+probable / +possible reserves |
| **P10 / P50 / P90** | Percentiles of an estimate. Hydrocarbon volumes are log-normal, so the mean exceeds the median |
| **Provenance** | How a fact is known: assumed, seismic, log, core, test, production history |
| **PSC** | Production sharing contract |
| **Relinquishment** | Giving back licence acreage on a schedule |
| **RRR** | Reserve replacement ratio: added ÷ produced. **Below 1.0 the company is liquidating itself** |
| **Take-or-pay** | A contract obliging the buyer to take, and the seller to deliver or pay |
| **Truth** | What is actually underground. **Structurally unreachable outside `OGSim.Information`** |
| **Value of information** | The expected improvement in a decision from buying data |
| **Work commitment** | Obligations attached to a licence. Failing forfeits the bond |
| **Working interest** | Share of costs and revenues in a licence |

---

## E — HSE and environment

| Term | Meaning |
|---|---|
| **Barrier** | A defence preventing or mitigating a hazard. **Barrier condition is equipment condition** |
| **Bow-tie** | Threats → preventive barriers → top event → mitigating barriers → consequences |
| **Emissions intensity** | Emissions per unit produced. The comparable measure |
| **Flaring intensity** | Gas flared per unit produced |
| **Lagging indicator** | The incident itself. Too late to act on. *(For leading indicators see section F — the term covers both HSE barriers and slow couplings.)* |
| **Loss of containment** | The top event: hydrocarbons where they should not be |
| **Near miss** | A threat that passed some barriers but not all. **A free warning** |
| **NORM** | Naturally occurring radioactive material, concentrating in scale |
| **Personal safety** | Injuries to individuals. Common, low severity, **cheap to improve** |
| **Process safety** | Preventing major accidents. Rare, catastrophic, **expensive to improve** |
| **Social licence** | The community's practical acceptance of operations. Distinct from legal compliance |

---

## F — Engine terms

| Term | Meaning |
|---|---|
| **Audit trail** | Append-only record of every decision and failure. Player-facing; saved with the game |
| **Command** | The only way anything changes. Validated, audited, applied, published |
| **Compartment** | See above — the unit of pressure simulation |
| **Contract** | An interface. The only kind of dependency permitted |
| **Deferred volume** | Production lost to a binding constraint. **Attributed to the element that caused it** |
| **Fault** | A failure routed through `IFaultPolicy`. Never discarded |
| **Fidelity level** | Which model implementation is registered. Arcade / standard / simulation |
| **Flow element** | Anything a stream passes through. The solver knows nothing else |
| **Invariant** | A condition asserted every tick. Violation halts the engine |
| **Material** | A registered substance. **The engine never branches on its identity** |
| **Module** | A unit of composition declaring what it provides, requires and owns |
| **Objective** | A testable predicate over game state. Observes; never influences |
| **Scenario** | A situation with a goal, as content: a world, a starting position, objectives, hard limits, a deadline. Never code |
| **Scenario runner** | What evaluates a scenario against a tick and reports how the run stands. It reports; the engine acts |
| **Occurrence vs notification** | A state change versus a message about it. Notifications never carry control flow |
| **Property** | A typed, unit-bearing, provenanced, uncertain fact |
| **Quantity** | A magnitude bound to a unit of a dimension. **No bare numbers for physical values** |
| **Read model** | The immutable snapshot the host renders. Built from beliefs, never truth |
| **Stream** | Material in motion: composition, pressure, temperature, phase split, provenance |
| **Tick** | One simulation step. One month |
| **Segment** | A within-tick interval over which availability and constraints are constant. Up to four per tick; solved separately and duration-weighted |
| **Propagation class** | How long a coupling takes to land: `P0` intra-solve through `P6` decades ([21](21_INTEGRATION.md) §2) |
| **Loop period** | How long one turn around a feedback loop takes. **Period sets difficulty, not strength** |
| **Loop entry event** | The alert that fires when a player *enters* a downward loop, while exits remain — not when its consequence arrives |
| **Leading indicator** | A published signal that precedes a slow coupling's effect. Mandatory for every P5/P6 coupling |
| **Cause chain** | The audit path from a critical event back to the decision that started it |
| **Standing indicator** | A value visible without being sought, required for any loop over two years |
| **Auto-pause** | The host stopping the clock on an event at or above a threshold. Changes what is seen, never what happened |
| **Reality profile** | The bundle of fidelity selections, Advisor levels, forgiveness levers and alert profile a game runs under. Stamped on every score |
| **Advisor** | The player-side agent providing recommendations and automation. Reads only the read model, acts only through the command bus, never sees truth |
| **Assist level** | Per-domain: Manual · Advise · Confirm · Auto. Changeable at any time; exploration judgement is capped at Advise |
| **Forgiveness** | The axis controlling how hard failure bites — hazard intensity, financial safety nets, licence grace, save policy. Model and content selection, never multipliers |
| **Equipment tier** | One entry in an equipment family's product ladder, gated by `requiresTech`. **Its datasheet is its effect** — a higher head curve is more flow; there is no tier bonus field |

## G — Environment and HSE terms

| Term | Meaning |
|---|---|
| **Access window** | The season in which an operation is physically possible. Missing one typically costs a year |
| **Derating** | Loss of equipment capacity due to ambient conditions — notably compressors in heat |
| **Environment profile** | The static description of a location: terrain, water depth, climate, access, ground, sensitivity |
| **Sensitivity** | A designation (protected area, settlement, aquifer, fishery) that **multiplies the consequence** of a release |
| **Setting** | Shorthand for the environment profile of a location. "The setting prices the project" |
| **Weather state** | The per-tick conditions and days lost, from the seasonal baseline plus stochastic variation |
| **Winterisation** | Freeze protection, heat tracing, enclosure — the cold-climate capital premium |

---

## Naming rules

| # | Rule |
|---|---|
| N1 | One concept, one name, everywhere — code, content, UI text, documents |
| N2 | Contract names are `I` + the domain noun. `IWell`, not `IWellEntity` or `IWellManager` |
| N3 | No `Manager`, `Helper`, `Util`, `Service`, `Handler`, `Data`, `Info` in any contract name. If a name needs one, the concept is not clear |
| N4 | Industry terms win over invented ones. `Perforation`, not `ReservoirConnection` |
| N5 | Strict meanings from the table at the top are binding, even where the industry is loose |
| N6 | Abbreviations only where the industry always abbreviates: IPR, VLP, GOR, POS, NGL, RRR, PSC, BS&W |
| N7 | A new term goes in this glossary **before** it goes in code |
