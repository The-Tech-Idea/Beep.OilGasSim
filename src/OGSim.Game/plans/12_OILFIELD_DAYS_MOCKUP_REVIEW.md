# Oilfield Days mockup review worksheet

The user created five mockups for the casual mechanical-only mode in:

`referenceart/Mockup/Oilfield Days/`

This worksheet records what each mockup is expected to show and the checks to
apply before turning them into Godot scenes.

## 1. File inventory

| Mockup | File | Dimensions |
|---|---|---|
| Casual top-down main scene | `CasualTopDownMainScenemockup.jpg` | 2752 x 1536 |
| Dispatch terminal / job board | `Dispatch TerminalJob Board mockup.jpg` | 2752 x 1536 |
| Field lease construction / placement | `Field Lease ConstructionPlacement mockup.jpg` | 2816 x 1536 |
| Vehicle / equipment garage | `VehicleEquipment Garage mockup.jpg` | 2752 x 1536 |
| Challenge result / leaderboard | `Challenge ResultLeaderboard mockup.jpg` | 2752 x 1536 |

## 2. Global constraint

Every Oilfield Days mockup must be checked for:

- [ ] No human is shown.
- [ ] No animal or creature is shown.
- [ ] No NPC or visible character is shown.
- [ ] The player is represented by a vehicle, cursor, drone, or other machine.
- [ ] Interaction is presented as terminal, dispatch, control panel, or vehicle
  action.
- [ ] The style is warm Stardew-like life-sim, not dark control-room UI.

## 3. Casual top-down main scene

Expected from the prompt:

- maintenance yard and nearby oilfield lease;
- dirt road connecting yard to lease;
- control-room cabin, maintenance workshop, covered warehouse, storage tanks,
  pumpjack, wellhead, separator, flare stack, parked service truck;
- square tile terrain;
- service truck as the mechanical player avatar;
- HUD with day/season/year, cash, reputation, actions-left;
- minimap and challenge timer;
- hotbar;
- job tracker;
- context prompt such as “Open Dispatch Terminal.”

Asset/engine checks:

- [ ] Uses extracted `assets/topdown` oilfield sprites or equivalent mechanical
  sprites.
- [ ] Terrain reads as square top-down tiles, not the existing isometric atlas.
- [ ] No human/animal/NPC artwork.
- [ ] Player vehicle is clearly distinct from parked vehicles.
- [ ] HUD values map to `FieldReadModel` or host casual state.

## 4. Dispatch terminal / job board

Expected from the prompt:

- left job board with task cards;
- right selected-job detail panel;
- task title, equipment icon, destination, deadline, reward, difficulty;
- dispatch truck and back buttons;
- top bar with day, cash, reputation, challenge timer;
- bottom vehicle/equipment slots;
- mechanical background with parked truck.

Asset/engine checks:

- [ ] Job cards are presented as dispatch tasks, not dialogue quests.
- [ ] No person or NPC appears.
- [ ] Job actions can eventually map to engine commands or host tasks.
- [ ] Deadline and reward are visible.
- [ ] Available/active/locked states are distinct.

## 5. Field lease construction / placement

Expected from the prompt:

- visible square-tile lease grid;
- dirt road and cleared pad;
- build menu with pumpjack, wellhead, separator, tank, flare, generator,
  water-injection pump, pipe manifold;
- selected-item detail and placement preview;
- cash/actions and confirm/cancel;
- minimap and challenge timer.

Asset/engine checks:

- [ ] Placement grid uses square tiles consistent with the mode plan.
- [ ] Build items map to engine/facility concepts, not invented buildings.
- [ ] Pumpjack/wellhead/separator/tank/flare are distinguishable.
- [ ] No human or animal appears.
- [ ] Confirm/cancel placement is clear.

## 6. Vehicle / equipment garage

Expected from the prompt:

- garage or equipment yard with parking bays;
- service truck, forklift, mobile crane truck, flatbed pipe truck,
  pipeline-construction excavator, workover rig;
- vehicle list with condition, fuel/energy, state, assigned task;
- selected-vehicle detail with meters and repair/upgrade buttons;
- bottom actions: repair, refuel, dispatch, park;
- top bar and mechanical background.

Asset/engine checks:

- [ ] Vehicles are mechanical and unoccupied.
- [ ] Condition/fuel/energy are represented as host/vehicle state, not engine
  reservoir truth.
- [ ] Assigned task field can link to a queued engine command or host task.
- [ ] No human/animal/NPC appears.

## 7. Challenge result / leaderboard

Expected from the prompt:

- challenge title, completed year/season, rank;
- scorecard with Field Value, Town Reputation, Efficiency, Clean Operations,
  Legacy;
- local leaderboard with vehicle/equipment icons instead of avatars;
- highlighted player entry;
- replay, next challenge, main menu buttons;
- golden-hour mechanical background.

Asset/engine checks:

- [ ] Score dimensions are readable and mechanical.
- [ ] Leaderboard entries use machine/equipment icons, not human avatars.
- [ ] Player entry is clearly highlighted.
- [ ] Buttons match the casual mode scene flow.
- [ ] No human or animal appears.

## 8. Overall review questions

For every Oilfield Days mockup:

1. Does it remain warm and approachable for a normal player?
2. Is the player presence purely mechanical?
3. Does every number map to host state, `FieldReadModel`, or a future bridge
   field?
4. Is the visual style consistent with the Stardew-style top-down reference?
5. Can the mockup be built from existing `assets/topdown` and terrain assets
   without requiring new humans/animals?

## 9. Next step

Once visual review is complete, create Godot scene stubs using:

- `11_CASUAL_TOPDOWN_GAME_MODE_PLAN.md`
- `08_GODOT_SCENE_PLAN.md`

Start with the casual main scene and field lease placement because those define
the tile grid, camera, and mechanical player interaction.

No engine or Godot code was changed by this document.
