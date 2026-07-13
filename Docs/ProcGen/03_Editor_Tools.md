# Escult Editor Tools

v1.0 — 2026-07-12. The in-editor toolchain that sits on top of the ProcGen ruleset
(`01_Puzzle_Ruleset.md`) and pipeline (`02_Generation_Pipeline.md`). Everything lives in
`Assets/Editor/ProcGen/` and shows up under the **Escult** menu in Unity.

## 0. Where levels live

Level artifacts moved from `Docs/ProcGen/Levels/` to **`Assets/ProcGen/Levels/`** so the
Unity editor imports and displays them (ESN files open as TextAssets, reports are
inspectable, the folder is visible in the Project window). Layout per level is unchanged:

```
Assets/ProcGen/Levels/<name>/
  <name>.esn.txt        # authoring source of truth
  <name>.level.json     # Unity-friendly topology
  <name>.solution.json  # solver witness (only when solvable)
  <name>.report.json    # V-checks, D-vectors, tier
  <name>.svg            # rendered sketch
```

Generated level prefabs go to `Assets/Prefabs/Levels/Generated/<name>.prefab`.
The design docs, library, and reference solver stay in `Docs/ProcGen/`.

## 1. Escult Studio  (`Escult → Studio`)

The designer-facing level editor that replaces the old ESN Workbench window.

- **Interactive canvas** — paint terrain by dragging (wall `#`, ground `.`, pit `~`),
  drop gates/altars/door/spawns with a click, right-drag always erases. The default
  **Select** tool is a pure cursor: hovering shows coordinates, clicking never changes
  anything, so you can inspect freely. The **Hand** tool (or a middle-mouse drag with
  any tool) pans the canvas. Tool hotkeys: `V`/`Esc` select, `H` hand, `B` wall,
  `G` ground, `R` pit, `T` gate, `A` altar, `D` door, `F` girl, `C` cat, `Q` wire,
  `X` erase (they fire when the canvas has focus — click it once). Ctrl+wheel or the
  toolbar slider zooms.
- **Coordinate rulers** — column indices run along the top and row indices down the
  left, matching the solver's `(col,row)` output (row 0 = top). The hovered cell's
  row/column labels highlight, so a witness line like `MOVE CAT (6,3)` is trivial
  to locate.
- **Gate tool "Paint gate" picker** — choose which gate the tool paints into: pick an
  existing letter to **add cells to that gate** (so an accidental erase is refilled,
  not turned into a new gate), or `＋ new gate` to start a fresh one. Starting a stroke
  on an existing gate also extends it, and the active gate outlines in blue.
- **Wire tool** — click an altar, then click gates to connect/disconnect them; wired
  gates highlight. The same wiring is editable as toggle chips in the Legend panel.
- **Art preview** — the canvas renders with the game's **shipped tileset**
  (`Tiles Final/tile sheet.psd`: ground `tile sheet_5`, pit `Hell Tile 1`,
  wall `tile sheet_60`, gate `tile sheet_42`) plus the Alter/Door/Girl/Cat prefab
  sprites — the same art the released levels use, not the old BasicTiles placeholders.
  Toggle "Art" for schematic colors.
- **Live validation** — with "Auto" on, every edit is linted, solved, and scored after
  ~0.6 s idle; the status bar shows solvable / minCost / slack / tier at all times, and
  the Results tab holds the full V1–V9 + D1–D10 report.
- **Solution replay** — "Replay ▶" scrubs through the solver's certified witness on the
  canvas: girl/cat move, gates toggle, bridges appear, the soul ledger counts down.
  The replay bar can **auto-play** the solution as an animation (▶/⏸) at 0.5×–4× speed,
  plus step (◀ / ▶▌), jump-to-start (⏮), and a scrubber.
- **Two-way ESN text** — the ESN text tab always mirrors the canvas; edit the text and
  the canvas follows. Undo/redo covers canvas, legend, and text edits, via the toolbar
  **↶ / ↷** buttons or **Ctrl+Z / Ctrl+Shift+Z** (Ctrl+Y also redoes). The keyboard
  shortcuts route to the Studio's own history when the canvas has focus; the toolbar
  buttons always work regardless of focus.
- **Load/Save** — Load (`Load ▾`): `.esn.txt`, **any level prefab** (via the reverse
  converter), or a **Recent** submenu of the last levels you opened/saved. The primary
  **Save** button writes the ESN, emits all pipeline artifacts, *and* (re)builds the
  level prefab in one click; the `▾` next to it holds the granular options (ESN only,
  ESN + artifacts, build prefab, build + insert into scene).
- **Collapsible / dockable panel** — the "Panel" toolbar toggle (or the ✕ on the panel's
  tab bar) hides the Results / ESN-text panel so the canvas gets the full width on small
  screens. "Dock: Side / Bottom" moves that panel between the right side and along the
  bottom of the canvas.
- Fully resizable UIToolkit layout — all three panes are splitter-dragged and scroll;
  nothing clips on small screens (the old fixed-size window problem).

> Note: changing the converter's tileset only affects **newly built** prefabs. Rebuild
> any level generated before this update (Studio **Save**, or the Browser's
> **Rebuild prefab**) to swap its placeholder tiles for the shipped ones.

## 2. Two-way ESN ⇄ Prefab converter  (`Escult → Convert`, or from the Studio/Browser)

`EscultPrefabConverter` builds **playable** level prefabs with the exact structure of the
hand-made ones, and reads any level prefab back into ESN.

**ESN → Prefab** (`ESN File → Level Prefab...`):
root (inactive, `LevelManager`) → `Grid - <name>` (cell size 0.64, tag Grid) → nested
instances of `Ground` / `Hell` / `Collision - Walls` tilemap prefabs (cleared of their
baked example tiles, then painted with the shipped `Tiles Final` tileset — ground
`tile sheet_5`, pit `Hell Tile 1`, wall `tile sheet_60`; falls back to BasicTiles if
those assets are missing), the decorative **`BestSoulsRemaining`** panel every shipped
level carries (a `SpriteRenderer` child of the Grid on sorting layer "Props", using a
`Sprites/Best/` sprite at the standard top-left placement), one `Gate` prefab
instance per gate id (gate tile cells, `INITIAL_GATE_STATUS` = ESN initial=CLOSED),
`Alter` instances with `connectedObjects` wired per the legend, and a `Door` (always
open, per design law). `LevelManager` gets girlPos/catPos (cell centers), isCatInLevel,
hellTile, groundMap/hellMap, gates and altars lists; `doors` stays empty **by design**
(populating it re-arms dead engine code that can soft-lock levels — see the
project ruleset notes). ESN row 0 maps to the top; the grid is centered on the origin.

**Prefab → ESN** (`Level Prefab → ESN Artifacts...`): reads tilemaps back to terrain
(precedence wall > pit > ground; void inside bounds becomes ground, with a warning —
that is what the engine actually does), gates via `GateContoller` (a gate object whose
cells sit on both pit and ground is split into co-wired ESN gates — the documented
one-gate-one-terrain workaround), wiring via `AlterController.connectedObjects`,
spawns via LevelManager. Emits the ESN plus all artifacts through the standard
lint/solve pipeline. Round-trip is verified: ESN → prefab → ESN preserves terrain
cell-for-cell, solvability, and minCost. Reverse conversion reads the *pre-auto-tile*
terrain (wall/pit/ground only) — the decoration below is cosmetic and never round-trips.

**Auto-tiling polish** (`EscultAutoTiler`, runs automatically inside `BuildPrefab`):
there are no RuleTiles in this project, so the shipped levels' wall faces/corners,
`WallTops` overlay, `Props` wall-shadow decoration, and floor-tile variety are all
hand-painted per level. `EscultAutoTiler` closes that gap by **learning** the pattern
from the 9 polished reference prefabs (`Level 3/4 New…`, `…easy 1…`, `ThrowCatOverAltar`,
`Tutorial 1–4 - Or level Variant`) instead of hand-authoring rules:
- For each wall/pit cell it records the artist's tile choice against the 8-neighbour
  wall/pit occupancy pattern (majority vote per pattern, falling back to a coarser
  4-neighbour vote, then to the flat placeholder tile if a pattern was never seen).
- It builds a `Grid/JuiceLayers/WallTops` overlay (same child path shipped levels use)
  the same way, keyed on the wall neighbourhood.
- It builds a `Grid/JuiceLayers/Props` layer for the wall-contact shadow decoration,
  including learning *not* to place a shadow on most open cells adjacent to a wall
  (so it doesn't oversaturate the level with props).
- Ground tiles get the reference levels' floor-variant mix, picked deterministically
  per cell (stable across rebuilds, not random noise every time).
- The learned tables are cached in-memory per editor session; **Escult → Convert →
  Rebuild Auto-Tiler Cache** forces a relearn (e.g. after editing a reference prefab).
- Fully generalizes: it is keyed on local neighbourhood shape, not on level identity,
  so it applies correctly to any generated topology, not just ones matching a
  reference layout. Verified visually against `Level 3 New - put as lvl 7 Variant`
  (the source of the polished scene "Level 4") and by re-running the round-trip/solver
  check on every tracked level after rebuilding — terrain, solvability and minCost are
  all unchanged, only decoration is added.
- If a reference prefab goes missing, decoration silently falls back to the flat
  placeholder tiles (a warning is added to `ConvertResult.Warnings`) rather than failing
  the build.

**Insert into scene** (`Insert Level Prefab Into Open Scene...`): instantiates the
prefab under the scene's `GameManager`, injects the scene Girl/Cat into its
`LevelManager`, appends it to `GameManager.levels`, and marks the scene dirty
(you decide when to save). Works from the menu, the Studio, and the Browser.

## 3. Escult Level Browser  (`Escult → Level Browser`)

Dashboard over both worlds:

- **ProcGen levels** — every `*.esn.txt` under `Assets/ProcGen/Levels` with its
  validity ✓/✗, tier, minCost/slack, and prefab status. Per row: open in **Studio**,
  **Build/Rebuild prefab**, **Insert into scene**, reveal **Files**.
- **Scene roster** — `GameManager.levels` of the open scene: reorder with ↑/↓
  (undo-aware, marks the scene dirty), select the level object, and **▶ Play from
  here** — enters play mode starting at that level (one-shot `PlayerPrefs` override,
  `#if UNITY_EDITOR` only, zero effect on builds).
- **Validate all** — runs the whole-folder validation and writes
  `Assets/ProcGen/Levels/_validation_report.md`.

## 4. Headless entry points (unchanged, for agents)

`EscultCli.Check / CheckText / CheckAll` remain the bridge-facing API
(`Escult → Validate All ESN Levels` for humans). They now refresh the AssetDatabase
when artifacts land inside `Assets/`. The escult-level-designer agent and
`Docs/ProcGen/Tools/solve_esn.py` are path-updated to `Assets/ProcGen/Levels`.

## 5. Future tool ideas (backlog)

Ranked by expected value for this team:

1. **Reachability / throw-lane overlay in the Studio** — shade cells reachable by
   girl vs cat in the *current* gate state, and draw the four throw rays from the
   hovered cell (the solver already computes all of this; it is pure UI).
2. **Batch generation dialog** — "give me N candidates: tier, size, must-use atoms" →
   runs the escult-level-designer agent loop or a future in-editor generator, drops
   results straight into the Browser for triage.
3. **Difficulty heatmap across the roster** — plot minCost/slack/solutionCount per
   scene-roster level to spot difficulty-curve dips (data is already in the reports).
4. **Playtest telemetry hook** — GameManager already logs per-level completion times;
   write them to a JSON the Browser can display next to the solver's tier estimate
   (predicted vs actual difficulty).
5. **Auto-tiler coverage for gates** — `EscultAutoTiler` currently polishes walls,
   pit rims, floor variety, wall-tops and wall-shadow props; gate cells still use the
   single flat `tile sheet_42`. The reference levels don't vary gate tiles by neighbor
   much (gates are usually 1–4 cells), so this is low priority, but worth a look if a
   generated level has an unusually large gate.
6. **Level diff viewer** — two ESN files side by side with cell-level highlighting;
   useful when the agent "repairs" a level and you want to see what changed.
7. **Record-keeper integration** — a "log this level" button in the Studio that
   appends a properly tagged entry to `Docs/ProcGen/Library/level_records.md`.
8. **Soft-lock auditor for hand-made levels** — reverse-convert every prefab in
   `Assets/Prefabs/Levels`, run the solver, and report which shipped levels are
   unsolvable-as-modeled or contain unterminated throw rays.
