# C15 — Consumables & Treatments

**Catalogue sheet** · chain position: slot-assigned, cross-cutting ([07](../design/07_TECHNOLOGY.md) §4b.3b) · phases: R12, R18, R23
**Authoring spec** for `treatment` content ([10](../design/10_CONTENT_AND_UNITS.md) §2). Unlike equipment, a treatment's **datasheet is its scoped-effect list** — the parameters it changes on the instance it is assigned to, plus a consumption rate and unit cost. The `Fits` column is the SlotKind ([SDD-005](../sdd/SDD-005_CAPABILITIES_AND_EFFECTS.md) §4.0b). Eras E1–E4; capex bands relative.

The station where "a new material" lands: mud for the drill floor, chemicals
for the lines, injectants for the reservoir — each declaring *where it fits*
and *what it moves*, so a tech unlock is never an orphan.

| Consumable (visible to the player) | Tier ladder | Tech gate | Era | Cost | Fits (SlotKind) | Scoped effects — what it changes on its owner |
|---|---|---|---|---|---|---|
| Water-based mud | basic → inhibitive | — | E1→E2 | $/day drilled | DrillingFluid | Baseline wellbore-stability threat rate; cheap cuttings disposal |
| Oil / synthetic mud | OBM → SBM | Synthetic drilling fluids | E2→E3 | $$$/day | DrillingFluid | Wellbore-stability threat ↓↓, **HPHT drilling capable**; cuttings disposal liability ↑ ([14](../design/14_HSE.md) §5.2) |
| Frac fluid system | slickwater → crosslinked gel | Hydraulic fracturing | E3→E4 | $$/job | CompletionFluid | Achievable frac half-length → the skin the frac delivers; **water demand per job** ([13](../design/13_ENVIRONMENT.md)) |
| Demulsifier | one | — | E1 | $/vol treated | ProcessAdditive (treater) | `emulsionPenalty` ↓ — treating efficiency held as water cut climbs ([SDD-006](../sdd/SDD-006_FACILITIES_AND_TRANSPORT_ELEMENTS.md) §2) |
| Hydrate inhibitor | methanol/MEG → **LDHI** | LDHI tier: Low-dosage hydrate inhibitors | E1→E3 | $$/vol → $/vol | ChemicalInjection (line, wellhead) | Hydrate margin +ΔT on that line; LDHI: same margin at 1/10th dose |
| Corrosion inhibitor | one | — | E2 | $/vol | ChemicalInjection | Corrosion severity term ↓ on the treated element ([SDD-012](../sdd/SDD-012_HAZARDS_AND_DEGRADATION.md) §1) |
| Scale inhibitor | continuous → squeeze programme | Scale management | E2→E3 | $/vol · $$/job | ChemicalInjection / squeeze operation | Scale hazard ↓; a squeeze is a periodic R12 operation on the well |
| Biocide | one | — | E2 | $/vol injected | InjectionStream additive | **Souring-curve rate ↓** ([SDD-012](../sdd/SDD-012_HAZARDS_AND_DEGRADATION.md) §5) — the DHS3 metallurgy bet, hedgeable |
| Drag-reducing agent | crossref | Flow improvers | E3 | $/vol | ChemicalInjection | Liquid line capacity ↑ without steel ([C11](C11_PIPELINES_AND_STATIONS.md)) |
| Polymer | one | Polymer / chemical EOR | E3 | $$$/vol | InjectionStream (material) | Mobility ratio → the polymer-drive plugin's recovery uplift; it **flows and conserves** as an `IMaterial` |
| Purchased CO₂ | one | CO₂ flood | E3→E4 | $$/t | InjectionStream (material) | The EOR injectant (drive plugin declares acceptance); E4: the sequestration-credit tie-in ⚑ |

**Couplings & notes**
- **Two shapes, one rule** ([07](../design/07_TECHNOLOGY.md) §4b.3b): chemicals apply scoped effects while assigned; stream injectants (polymer, CO₂) are ordinary materials whose reservoir behaviour lives in the drive plugin — no material-identity branches anywhere.
- Every ChemicalInjection assignment needs the **injection skid** on the element ([C11](C11_PIPELINES_AND_STATIONS.md)) — the hardware and the consumable are separate purchases, as in reality.
- Consumption is metered by the owner and lands in the OPEX chemicals line ([08](../design/08_ECONOMICS.md) §2.2); mud and frac fluids are consumed by their operations ([SDD-007](../sdd/SDD-007_OPERATIONS_ENGINE.md) §3 — the consumables that stop during standby).
- Biocide is the quiet star: it is the only *preventive* lever on souring, bought years before the H₂S it prevents would have arrived.
