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
  drop gates/altars/door/spawns with a click, right-drag always erases. Tool hotkeys:
  `Q` wire, `W` wall, `E` ground, `R` pit, `T` gate, `A` altar, `D` door, `F` girl,
  `C` cat, `X` erase. Ctrl+wheel or the toolbar slider zooms.
- **Wire tool** — click an altar, then click gates to connect/disconnect them; wired
  gates highlight. The same wiring is editable as toggle chips in the Legend panel.
- **Art preview** — the canvas renders with the game's actual tiles and sprites
  (BasicTiles + Alter/Door/Girl/Cat prefabs); toggle "Art" for schematic colors.
- **Live validation** — with "Auto" on, every edit is linted, solved, and scored after
  ~0.6 s idle; the status bar shows solvable / minCost / slack / tier at all times, and
  the Results tab holds the full V1–V9 + D1–D10 report.
- **Solution replay** — "Replay ▶" scrubs through the solver's certified witness on the
  canvas: girl/cat move, gates toggle, bridges appear, the soul ledger counts down.
- **Two-way ESN text** — the ESN text tab always mirrors the canvas; edit the text and
  the canvas follows. Undo/redo (Ctrl+Z / Ctrl+Y) covers canvas, legend, and text edits.
- **Load/Save** — Load: `.esn.txt` or **any level prefab** (via the reverse converter).
  Save: ESN file, ESN + all pipeline artifacts, level prefab, or prefab + insert
  into the open scene.
- Fully resizable UIToolkit layout — all three panes are splitter-dragged and scroll;
  nothing clips on small screens (the old fixed-size window problem).

## 2. Two-way ESN ⇄ Prefab converter  (`Escult → Convert`, or from the Studio/Browser)

`EscultPrefabConverter` builds **playable** level prefabs with the exact structure of the
hand-made ones, and reads any level prefab back into ESN.

**ESN → Prefab** (`ESN File → Level Prefab...`):
root (inactive, `LevelManager`) → `Grid - <name>` (cell size 0.64, tag Grid) → nested
instances of `Ground` / `Hell` / `Collision - Walls` tilemap prefabs (cleared of their
baked example tiles, then painted with BasicTiles: floor/pit/walls), one `Gate` prefab
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
cell-for-cell, solvability, and minCost.

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
5. **Art-pass assistant** — the generated prefabs use BasicTiles; a tool that
   re-skins a generated level with the `Tiles Final` rule-tile look (walls with
   proper corners/shadows) would close the gap to shipped-level polish.
6. **Level diff viewer** — two ESN files side by side with cell-level highlighting;
   useful when the agent "repairs" a level and you want to see what changed.
7. **Record-keeper integration** — a "log this level" button in the Studio that
   appends a properly tagged entry to `Docs/ProcGen/Library/level_records.md`.
8. **Soft-lock auditor for hand-made levels** — reverse-convert every prefab in
   `Assets/Prefabs/Levels`, run the solver, and report which shipped levels are
   unsolvable-as-modeled or contain unterminated throw rays.
