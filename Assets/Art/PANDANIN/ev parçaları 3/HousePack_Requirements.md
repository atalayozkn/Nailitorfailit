# House Pack – change requests from the game team

This file answers `HousePack_Spec.md`. It lists what the pack needs so the house can be built in Unity
**with every part at its original scale** (no scaling inside prefabs). Written for people and AI agents.

## 0. How the game builds a house (the constraints)

- Floor: **1 × 1 m tiles** (our own `FloorConstruct` tiles; the pack's `Floor_Tile_*` are not used for this).
- Rooms are self-contained prefabs: **3 × 3 m** and **3 × 6 m**. Houses are 2–4 rooms side by side:
  L shape 6 × 6 m, T shape 9 × 6 m, four rooms 9 × 6 m.
- Every room edge has **one wall slot per 1 m tile**. Each slot shows exactly one of: a wall, an inner door,
  a front door or nothing. "Nothing" is used where a neighbouring room already draws the shared wall.
- Walls and roof are hidden while the player builds the floor. Each room's walls and **its own roof** appear
  when that room is finished, so every room needs a roof that works on its own and also next to its neighbours.
- No scaling in Unity. Parts must line up on the 1 m grid at scale 1.

Priority: **P1** = blocks the house build, **P2** = causes visible gaps or clipping, **P3** = cleanup.

## 1. Doors and door walls

| # | Prio | Problem | Request |
|---|---|---|---|
| D1 | P1 | `Wall_DoorOpening` is 1.40 m wide. It doesn't fit a 1 m slot and overlaps both neighbouring walls by 0.20 m. | Make one 1 m door slot possible. Preferred: a lintel piece `Wall_DoorLintel` **1.00 × 0.55 × 0.10 m** (fills 1.95 → 2.50 m above the door), bottom-centre pivot, same plaster as `Wall_Plain`. A door slot is then `Door_Frame_A` + `Door_Panel_*` + `Wall_DoorLintel`. (Alternative: a `Wall_DoorOpening` that is exactly 1.00 m wide.) |
| D2 | P1 | `Door_Frame_A/B`, `Door_Panel_A–E` and `Door_Jamb_A/B` have off-centre pivots (spec §6), so they can't be snapped into a slot. | Re-export with **bottom-centre pivots** in the closed position, centred in a 1.00 m opening, with the frame and panel sharing the same pivot. |
| D3 | P2 | `Door_Frame_B` (1.23 m) and `Door_Panel_D` (1.11 m) are wider than a 1 m slot. | Either make them 1.00 m or mark them as "not for wall slots". |
| D4 | P3 | `Door_Panel_E` is `Door_Panel_D` rotated 90°. | Not needed, we rotate in Unity. It can be removed. |

## 2. Roofs

| # | Prio | Problem | Request |
|---|---|---|---|
| R1 | P1 | All roofs span 4 m (`Roof_Gable_4x4`, `Roof_GableEnd_4m`, `Roof_Panel_1m` / `Roof_EdgePanel_1m` cover 2 m per slope). Our rooms are **3 m** wide. | A **3 m-span** set at the same 30° pitch: `Roof_Panel_1m_3m`, `Roof_EdgePanel_1m_3m`, `Roof_GableEnd_3m`. `Roof_Ridge_1m` can stay as it is. Optional ready-made `Roof_Gable_3x3` and `Roof_Gable_3x6`. |
| R2 | P1 | Roof pivots are at the lowest point, and that includes the downpipes (`Roof_Gable_4x4` H = 4.48 m). Placing the roof at y = 2.5 m puts it in the wrong place. | Roof pivot at **wall top / underside of the eave** (y = 0 there) and centred on the footprint the roof covers, not on the overhang. Then a roof always goes at y = 2.50. |
| R3 | P2 | Downpipes and gutters are baked into the full roofs, so they hang into neighbouring rooms when rooms are placed side by side. | Full roofs without downpipes. We add the separate `Downpipe` / `Gutter` only on outside edges. |
| R4 | P2 | `Roof_Panel_1m` is 1.04 m wide. Tiled every 1 m, neighbours overlap by 4 cm on the same plane, which causes z-fighting. | Make it exactly **1.00 m** wide, or explain the overlap and offset it so the faces aren't coplanar. |
| R5 | P2 | When two rooms sit side by side (the shared wall runs along the ridge), both eaves overhang 0.35 m into each other and clip. | An eave-less slope variant `Roof_Panel_1m_NoEave` (3 m span) that stops at the wall centre line, for slopes that meet a neighbouring room. |
| R6 | P2 | Where two rooms meet end to end, `Roof_EdgePanel_1m` adds a barge board overhang that pokes into the next room's roof. | Fine as is if the plain `Roof_Panel_1m` ends exactly at its 1 m bounds (we use the plain panel on shared ends). Please confirm. |

## 3. Walls

| # | Prio | Problem | Request |
|---|---|---|---|
| W1 | P2 | The spec says outside = local +Z but this hasn't been checked. | Confirm, and make sure `Wall_Plain`, `Wall_Window`, frames and panels all use the same convention. |
| W2 | P2 | The game camera looks down into rooms, so the inside face of every wall is visible. | Both faces finished (inside plaster colour), with no open backs or missing faces. |
| W3 | P3 | `Wall_Window` sits 1 m wide in a slot, but its 0.26 m depth includes the sill. | Confirm the wall body is centred on the pivot (sill sticks out on +Z only) so it lines up with `Wall_Plain`. |
| W4 | P3 | `Wall_Panel_Small` has an off-centre pivot. | Bottom-centre pivot. |

## 4. Other pivots (P3, cleanup)

`Floor_Tile_A–D`, `Trim_Post`, `Trim_Strip`, `Water_Layer_A/B` and `Stone_Block` have centre pivots. Please
re-export them with **bottom-centre** pivots so every part in the pack follows the same rule. `Pool_Ladder`
(pivot at rim level) is fine as it is.

## 5. Already fine, no change needed

1 m wall modules, 2.5 m wall height, 0.10 m wall thickness, `Wall_CornerTrim` for outside corners, the
material slots and texture setup, furniture and lighting sizes, the face budget.
