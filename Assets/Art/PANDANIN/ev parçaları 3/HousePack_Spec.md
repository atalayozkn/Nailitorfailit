# House Pack – Modular Low-Poly Kit (spec)

Folder: `Assets/Art/PANDANIN/ev parçaları 3/`
83 FBX files, one prefab-ready mesh per file, plus 3 textures. Built in Blender 5.2, exported for Unity.

This file is written for people **and for AI agents**. Every part has a stable name, a category, a
purpose and its exact size, so a level can be assembled by snapping parts on a grid without opening
each mesh.

---

## 1. Global conventions

| Item | Value |
|---|---|
| Units | 1 Unity unit = 1 m. Import scale 1, no extra scaling needed. |
| Axes (Unity) | Y up. Sizes below are **W = X, H = Y, D = Z**. |
| Transform | Every FBX root has position 0, rotation 0, scale 1 (all applied before export). |
| Pivot | **Bottom-centre** (X and Z centred, Y = 0 at the lowest point) for all parts, unless listed in §6. Drop on the floor at y = 0 and it sits correctly. |
| Front / outside face | Points to local **+Z** for walls, windows, doors, cabinets, appliances (expected from the Blender −Y → Unity +Z export; confirm once on first placement and rotate 180° on Y if a set comes in backwards). |
| Grid | Base module **1.0 m**. Large pieces are multiples of 1 m. Use 0.5 m snapping for props. |
| Storey height | **2.5 m** (walls). Wall thickness **0.10 m**. |
| Door opening | **1.0 m wide × 1.95 m high** (matches the original door panels and frames). |
| Roof | 30° pitch for gable roofs, 4 m span = 4 wall modules. Eaves overhang ~0.35–0.40 m. |
| Style | Stylised low-poly, warm palette: cream plaster, orange wood, terracotta/orange accents, shingle roofs. |

## 2. Materials and textures

All parts share these 4 materials (names as they appear in the FBX material slots):

| Slot name | Texture | Use | Unity setup |
|---|---|---|---|
| `Ev_Atlas` | `Ev_Texture_Atlas.png` (2048²) | Plaster, panels, star tiles, flat colours (UV picks single-colour spots of the atlas) | Opaque, URP/Lit. Wrap = Clamp is fine. |
| `Ev_Ahsap` | `Ev_Ahsap_Tekrar.png` (tileable planks) | All wood: frames, furniture, roof trims | **Wrap Mode = Repeat** (UVs are in world metres, ~1.0 × 0.9 m per tile) |
| `Ev_Sindil` | `Ev_Sindil_Tekrar.png` (tileable shingles) | Roof surfaces only | **Wrap Mode = Repeat** (~1.4 m per tile) |
| `Glass` | none | Window panes, shower, oven door, peephole | Surface Type = Transparent, light blue, alpha ~0.35 |

If a model imports grey: Model Import Settings → **Materials** → remap each slot name above to a
material that uses the matching texture.

Lights: `Lamp_*`, `Chandelier`, `Light_CeilingPanel` contain a child light. Turn on **Import Lights**
in the model import settings; intensity will need tuning in Unity.

---

## 3. Parts by category

Size column = **W × H × D in metres** (Unity X × Y × Z). Tris are approximate (faces × 2).

### 3.1 Walls (build the shell) – snap on 1 m grid, height 2.5 m

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Wall_Plain` | Solid 1 m wall segment | 1.00 × 2.50 × 0.10 | Repeat along a line for long walls |
| `Wall_Window` | 1 m wall with built-in window (glass + sill) | 1.00 × 2.50 × 0.26 | Depth includes the outside sill |
| `Wall_DoorOpening` | Wall with 1.0 × 1.95 m door hole | 1.40 × 2.50 × 0.10 | Put a `Door_Frame_*` + `Door_Panel_*` in the hole |
| `Wall_CornerTrim` | Corner post to hide wall joints | 0.15 × 2.50 × 0.15 | Place on every outside corner |
| `Baseboard` | Skirting board, 1 m | 1.00 × 0.10 × 0.02 | Inside walls, along the floor |
| `Wall_Panel_Small` | Small decorative star-tile panel | 0.56 × 0.78 × 0.02 | Pivot off-centre, see §6 |

### 3.2 Windows

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Window` | Stand-alone window (frame, glass, sill) | 0.90 × 1.04 × 0.22 | For custom wall holes |
| `Window_Shutters` | Window with two open wooden shutters | 1.64 × 1.04 × 0.22 | Same window + shutters each side |

### 3.3 Doors and door details

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Door_Panel_A` | Door leaf (cream) | 1.01 × 1.94 × 0.10 | Original model, pivot see §6 |
| `Door_Panel_B` | Door leaf variant | 1.00 × 1.95 × 0.10 | Pivot see §6 |
| `Door_Panel_C` | Door leaf, wood-framed cream panel | 1.01 × 1.95 × 0.07 | Pivot see §6 |
| `Door_Panel_D` | Door leaf, wider | 1.11 × 1.94 × 0.10 | Pivot see §6 |
| `Door_Panel_E` | Door leaf, same as D but **rotated 90°** (thin along X) | 0.10 × 1.94 × 1.11 | Use for doors in walls running along Z |
| `Door_Frame_A` | Wooden door frame (casing) | 1.00 × 1.95 × 0.14 | Pivot see §6 |
| `Door_Frame_B` | Wider wooden door frame | 1.23 × 1.95 × 0.12 | Pivot see §6 |
| `Door_Jamb_A` | Open door / side jamb, faces along X | 0.20 × 1.86 × 0.98 | Pivot see §6 |
| `Door_Jamb_B` | Open door / side jamb variant | 0.20 × 1.79 × 0.89 | Pivot see §6 |
| `Door_SlidingGlass` | Two-leaf sliding glass door | 2.00 × 2.10 × 0.13 | Needs a 2 m opening |
| `Door_Garage` | Sectional garage door with frame | 2.84 × 2.38 × 0.16 | Cream panels, wood frame |
| `Door_Threshold` | Door sill / threshold | 1.00 × 0.03 × 0.17 | Under door frames |
| `Door_Handle` | Lever handle + key hole | 0.15 × 0.16 × 0.06 | Attach to door leaf ~1.0 m high |
| `Door_Hinge` | Hinge | 0.08 × 0.12 × 0.02 | 2–3 per door |
| `Door_Lock` | Lock cylinder plate | 0.05 × 0.09 × 0.02 | |
| `Door_Peephole` | Peephole | 0.03 × 0.03 × 0.12 | Goes through the door |
| `Doormat` | Doormat | 0.80 × 0.02 × 0.50 | In front of entrances |

### 3.4 Roofs (all 30° pitch except shed/porch)

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Roof_Gable_4x4` | Full gable roof for a 4 × 4 m room, with gutters and downpipes | 4.70 × 4.48 × 5.06 | H includes downpipes; roof starts at wall top (2.5 m) |
| `Roof_Gable_8x8` | Full gable roof for an 8 × 8 m house | 8.82 × 2.95 × 9.26 | Ridge runs along X |
| `Roof_Hip_8x6` | Hip roof (4 slopes) for 8 × 6 m | 9.32 × 4.41 × 7.26 | H includes downpipes |
| `Roof_Shed_4x3` | Single-slope (15°) roof for annex 4 × 3 m | 4.72 × 3.60 × 4.03 | Low side has gutter + downpipe |
| `Roof_Porch_6x3` | Porch / veranda: posts + 10° roof, leans on wall | 6.40 × 3.41 × 3.75 | Self-standing with 4 posts, back edge goes against wall |
| `Roof_Dormer` | Dormer window to sit on a roof slope | 1.94 × 1.73 × 2.10 | Has glass window |
| `Roof_Panel_1m` | 1 m slope module (shingles + gutter) | 1.04 × 1.65 × 2.56 | Line up along X to build any roof length |
| `Roof_EdgePanel_1m` | 1 m slope module with barge board (gable end) | 1.10 × 1.68 × 2.58 | Use at both ends of a run of `Roof_Panel_1m` |
| `Roof_Ridge_1m` | Ridge cap, 1 m | 1.00 × 0.17 × 0.43 | On top where two slopes meet |
| `Roof_GableEnd_4m` | Triangle gable wall for a 4 m span | 0.10 × 1.16 × 4.00 | Closes the triangle under a gable roof, thin along X |
| `Chimney` | Chimney with cap | 0.70 × 1.43 × 0.70 | Place on a roof slope |
| `Gutter` | Rain gutter, 1 m | 1.00 × 0.13 × 0.15 | Under eaves |
| `Downpipe` | Downpipe, full storey | 0.12 × 2.51 × 0.47 | From eave to ground |
| `Awning` | Striped fabric awning over a door/window | 2.10 × 0.63 × 1.07 | Mount on wall above openings |

### 3.5 Stairs, balcony, exterior

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Stairs_Interior` | 12-step straight stair, one storey (2.5 m) with handrail | 1.00 × 3.52 × 3.03 | H includes the rail; floor-to-floor 2.5 m |
| `Steps_Exterior` | 3-step entrance stairs (0.45 m) | 1.60 × 0.45 × 0.90 | Up to `Entrance_Platform` |
| `Entrance_Platform` | Front porch platform | 2.00 × 0.45 × 1.50 | Top at 0.45 m |
| `Ramp` | Accessibility ramp to the platform | 1.20 × 0.45 × 3.00 | Rises to 0.45 m |
| `Railing` | 1 m railing segment | 1.01 × 1.00 × 0.06 | Balconies, stairs, terraces |
| `Balcony` | 2 × 1 m balcony slab with railing | 2.00 × 1.08 × 1.00 | Attach to wall at floor level of upper storey |
| `Sidewalk_Tile` | 1 × 1 m pavement tile with kerb | 1.00 × 0.20 × 1.08 | Tile along streets |
| `Stone_Block` | Small decorative stone block | 0.34 × 0.11 × 0.35 | Pivot centre, see §6 |

### 3.6 Floors and trims (original models)

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Floor_Tile_A` | 1 × 1 m floor tile, star pattern | 1.00 × 0.07 × 1.00 | Pivot centre, see §6 |
| `Floor_Tile_B` | 1 × 1 m floor tile, cream plaster (648 faces) | 1.00 × 0.07 × 1.00 | Highest poly floor, use sparingly |
| `Floor_Tile_C` | 1 × 1 m floor tile, cream plaster | 1.00 × 0.07 × 1.00 | |
| `Floor_Tile_D` | 1 × 1 m floor tile, star pattern | 1.00 × 0.07 × 1.00 | |
| `Trim_Post` | Orange trim post | 0.10 × 0.07 × 1.00 | Lies flat; edge between floor tiles |
| `Trim_Strip` | Orange trim strip | 1.00 × 0.07 × 0.20 | Lies flat |
| `Water_Layer_A` / `_B` | 1 × 1 m flat cream slab (formerly "water layer") | 1.04 × 0.11 × 1.04 | Plain cream now, not water |

### 3.7 Pool and bathroom

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Pool_EdgeStone` | Pool coping stone, 1 m (2 star tiles) | 1.00 × 0.06 × 0.42 | Around pool rim |
| `Pool_EdgeCorner` | Pool coping corner | 0.40 × 0.06 × 0.40 | Corners of the pool rim |
| `Pool_Ladder` | Pool ladder | 0.55 × 2.02 × 0.63 | **Pivot at pool-rim level** (1 m goes below), see §6 |
| `Shower` | Glass shower cabin with tray and head | 0.91 × 2.06 × 0.91 | |
| `Bathtub` | Bathtub, wood panelled, with water and faucet | 1.74 × 0.75 × 0.76 | |
| `Bathroom_Sink` | Vanity: wood cabinet, basin with water, faucet | 0.84 × 0.98 × 0.49 | Back against wall |
| `Toilet` | Toilet with wooden seat and tank lid | 0.45 × 0.85 × 0.75 | Back against wall |
| `Faucet` | Separate faucet | 0.07 × 0.14 × 0.19 | Kitchen counters etc. |

### 3.8 Kitchen

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Kitchen_Cabinet_Base` | Base cabinet, 0.6 m module | 0.60 × 0.84 × 0.61 | Line up along walls |
| `Kitchen_Cabinet_Wall` | Wall cabinet, 0.6 m module | 0.60 × 0.70 × 0.38 | Mount ~1.45 m above floor |
| `Kitchen_Countertop` | Worktop with backsplash, 1.2 m (= 2 base cabinets) | 1.20 × 0.10 × 0.62 | Put on cabinets at y = 0.84 |
| `Stove` | Oven with 4-burner hob | 0.60 × 0.93 × 0.65 | Same width as a base cabinet |
| `Fridge` | Two-door fridge | 0.70 × 1.80 × 0.73 | |

### 3.9 Living and bedroom

| Name | Purpose | W × H × D | Notes |
|---|---|---|---|
| `Table` | Dining table | 1.40 × 0.76 × 0.80 | |
| `Chair` | Wooden chair | 0.44 × 0.96 × 0.44 | Back toward local −Z |
| `Sofa` | 3-seat sofa, wood body, cream cushions | 2.02 × 0.93 × 0.86 | |
| `Bed` | Double bed, wooden frame and headboard | 1.76 × 1.10 × 2.16 | Headboard at one end of Z |
| `Wardrobe` | Two-door wardrobe | 1.24 × 2.06 × 0.65 | |
| `Bookshelf` | Bookshelf with 18 coloured books | 0.90 × 1.80 × 0.30 | |
| `Shelf_Wall` | Wall shelf with brackets | 0.90 × 0.21 × 0.22 | |
| `Mirror` | Wall mirror | 0.60 × 0.90 × 0.03 | |
| `Rug` | Rug with tassels | 2.12 × 0.01 × 1.40 | |
| `Curtain` | Curtain pair with rod | 1.85 × 2.25 × 0.08 | Above a window; rod at top |

### 3.10 Lighting (contain Light components)

| Name | Purpose | W × H × D | Light |
|---|---|---|---|
| `Lamp_Floor` | Floor lamp | 0.44 × 1.75 × 0.44 | Point, 1.55 m |
| `Lamp_Table` | Table lamp | 0.24 × 0.41 × 0.24 | Point |
| `Lamp_Pendant` | Ceiling pendant (1 m cord) | 0.50 × 1.00 × 0.50 | Point. Pivot bottom: place top at ceiling (y = 2.5) |
| `Chandelier` | 6-candle chandelier | 0.77 × 0.75 × 0.68 | Point. Pivot bottom: place top at ceiling |
| `Light_CeilingPanel` | Flat ceiling light panel | 0.60 × 0.04 × 0.60 | Area. Place at ceiling, light faces down |

---

## 4. Assembly recipes

- **4 × 4 m room:** 16 × `Wall_Plain` (swap some for `Wall_Window` / `Wall_DoorOpening`), 4 × `Wall_CornerTrim`, 16 floor tiles, `Roof_Gable_4x4` at y = 2.5.
- **8 × 8 m house:** 32 wall modules around the edge, `Roof_Gable_8x8` at y = 2.5; add `Chimney`, `Roof_Dormer` on the slopes.
- **Custom-length gable roof:** `Roof_EdgePanel_1m` + N × `Roof_Panel_1m` + `Roof_EdgePanel_1m` per side (mirror the second side 180° on Y), `Roof_Ridge_1m` × (N+2) on top, `Roof_GableEnd_4m` at each end.
- **Entrance:** `Entrance_Platform` against the door wall, `Steps_Exterior` or `Ramp` in front, `Doormat`, `Awning` above the door.
- **Kitchen run:** `Kitchen_Cabinet_Base` ×2 + `Kitchen_Countertop`, `Stove` next to it, `Fridge` at the end, `Kitchen_Cabinet_Wall` above.
- **Two storeys:** second floor at y = 2.5; `Stairs_Interior` connects them; `Balcony` hangs on the outside at y = 2.5.

## 5. Budget

All parts are low poly: most are 6–150 faces. Heaviest: `Floor_Tile_B` (648), `Roof_Gable_4x4` (302), `Roof_Gable_8x8` (294), `Chandelier` (280), `Roof_Hip_8x6` (247), `Toilet` (246).

## 6. Pivot exceptions (original models)

These came from the older pack and keep their original pivot. Pivot position is given as a
fraction of the bounding box along width / height / depth (0 = min side, 0.5 = centre, 1 = max side),
measured in Blender. Unity's axis conversion can mirror the width and depth sides, so treat these
as "how far off-centre" rather than which side:

| Name | Pivot X / Y / Z |
|---|---|
| `Door_Frame_A` | 0.93 / 0.45 / 0.07 |
| `Door_Frame_B` | 0.13 / 0.46 / 0.20 |
| `Door_Jamb_A` | 0.50 / 0.50 / 1.00 |
| `Door_Jamb_B` | 0.53 / 0.45 / 1.04 |
| `Door_Panel_A` | 0.76 / 0.28 / 0.63 |
| `Door_Panel_B` | 0.85 / 0.25 / 0.00 |
| `Door_Panel_C` | 0.20 / 0.14 / 0.00 |
| `Door_Panel_D` | 0.89 / 0.18 / 0.00 |
| `Door_Panel_E` | 0.00 / 0.18 / 0.88 |
| `Floor_Tile_A–D`, `Trim_Post`, `Trim_Strip`, `Water_Layer_A/B`, `Stone_Block` | centre (0.5 / 0.5 / 0.5) – raise by half the height to sit on the floor |
| `Wall_Panel_Small` | 0.46 / 0.22 / 0.50 |
| `Pool_Ladder` | centre / 0.49 (rim level) / centre |

When placing these, snap by their renderer bounds (`Renderer.bounds`) instead of the transform position.
