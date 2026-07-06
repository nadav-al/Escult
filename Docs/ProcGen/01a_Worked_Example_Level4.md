# Worked Example — Level 4 in the v1.0 schema

Concrete illustration of how one hand-made level maps onto the ruleset's data
structures (`01_Puzzle_Ruleset.md` §5). Entity coordinates are verified from a
prior live tilemap dump; exact per-cell terrain is landmark-accurate (islands at
the listed coords) but the full pit/ground fill between islands is schematic.
The SolutionPath is the *shape* a solver would emit, not a machine-verified
minimal path (door reachability needs a live solver pass).

## What the level looks like (schematic — not every cell)

Cell coords on the Grid; `.`=ground(-ish island), `#`=pit(hell), `A`=altar,
`C`=Cat spawn, `G`=Girl spawn, `D`=door, `▓`=gate cells.

```
 y                                         Gate2 (4 cells, x=7, y=2..5)
                                             1 over pit, 3 over ground
 5 #########################▓###############
 4 ..A1......................▓...........D..   A1=(-3,4)  D=(13,4)
 3 #.........................▓##############
 2 #########################▓###############
 1 ########..................###############
 0 ...C........G............................   C=(-11,0)  G=(0,0)
-1 #########################################
-3 ....▓####################################
-4 ..A2▓....................................   A2=(-3,-4)
-5 ....▓####################################
       ^Gate1 (3 cells, x=-1, y=-5..-3), all over pit
```

Reading it: the world is islands in a sea of pit. Gate1 (wired to Altar1) sits
in the south, over pit, near Altar2. Gate2 (wired to Altar2) sits in the north,
mostly over ground, right on the corridor to the Door — the wiring is physically
*crossed* (each altar controls the gate near the OTHER altar). The Door is always
open; the whole puzzle is **traversal**: open gates to unlock bridging/walking
lanes, then get the Girl across the pit to the Door.

## 1. LevelTopology (static definition — what a generator emits)

```yaml
LevelTopology:
  version: 1
  id: level_4
  bounds: {min: [-12,-6], max: [14,6]}     # perimeter is wall/pit → satisfies V1
  # terrain: full G|P|W grid omitted here (landmark islands listed below);
  #          a generator would emit the complete row-major strings.
  islands_verified:                         # ground clusters confirmed from dump
    - cat_island:  around [-11,0]
    - girl_island: around [0,0]
    - north_corridor: y=4 strip leading to door at x≈8..13
    - south_island: around [-3,-4] (altar2)
  gates:
    g1: {cells: [[-1,-5],[-1,-4],[-1,-3]], over: [P,P,P],       initial: CLOSED}
    g2: {cells: [[7,2],[7,3],[7,4],[7,5]], over: [P,G,G,G],     initial: CLOSED}
  altars:
    a1: {cell: [-3,4],  targets: [g1]}      # crossed: a1 (north) opens g1 (south)
    a2: {cell: [-3,-4], targets: [g2]}      # crossed: a2 (south) opens g2 (north)
  doors:
    d1: {cell: [13,4], initial: OPEN}       # always open → pure traversal puzzle
  spawns: {girl: [0,0], cat: [-11,0]}
  soul_budget: 9
  decoys: []
```

Key point: a gate is a **cell set**, and `over:` records whether each cell sits
on pit or ground. Gate1's cells are all over pit — opening it only *permits
bridging* through that corridor (still costs souls per cell). Gate2 has 3 cells
over ground — opening it yields an instantly-walkable segment for free.

## 2. PuzzleState (the initial dynamic node the solver starts from)

```yaml
PuzzleState:            # t = 0
  girl: [0,0]
  cat:  {status: ON_GROUND, cell: [-11,0]}
  souls: 9
  gates: {g1: CLOSED, g2: CLOSED}
  doors: {d1: OPEN}
  bridges: []
# walkable(c) = (terrain[c]==G or c in bridges) and not covered by a CLOSED gate
# Girl at t=0 cannot reach the Door: pit + closed Gate2 block every land path.
```

Every action produces a new PuzzleState; the solver explores this graph.

## 3. SolutionPath (illustrative shape of a solver's certificate)

```yaml
SolutionPath:
  min_cost: ~5          # illustrative; exact minimum pending a live solver run
  note: "demonstrates all three costed mechanics + the crossed wiring"
  steps:
    - {op: MOVE,      actor: CAT,  to: [-4,4]}      # cat free-bridges/walks north to a1
    - {op: SACRIFICE, altar: a1}                    # -1 soul → opens g1 (south, over pit)
    - {op: MOVE,      actor: CAT,  to: [-4,-4]}     # cat travels to a2 (south island)
    - {op: SACRIFICE, altar: a2}                    # -1 soul → opens g2 (north corridor now walkable)
    - {op: MOVE,      actor: CAT,  to: [0,1]}       # cat returns near Girl
    - {op: BRIDGE,    cell: [1,0]}                  # -1 soul: begin a walk-lane for the Girl
    - {op: BRIDGE,    cell: [2,0]}                  # -1 soul: extend it (repeat as the gap needs)
    - {op: MOVE,      actor: GIRL, to: [7,4]}       # Girl walks the freed Gate2 ground + bridges
    - {op: MOVE,      actor: GIRL, to: [12,4]}      # along the north corridor to the door
    - {op: EXIT}                                    # Girl touches open door → WIN
  # V3 check: cost (~4-5) <= budget 9 → slack ~4-5 → this level scores 'easy/medium' on D2.
```

## 4. Why this example is instructive for generation

- **Crossed wiring** (D7 misdirection): the solver must realize Altar1 opens the
  *southern* gate — a generator raises difficulty by increasing altar↔target distance.
- **Gate-over-pit vs gate-over-ground**: same entity type, different downstream
  cost (bridge needed vs. free walk). A generator picks `over:` deliberately.
- **Two crossing methods for the Cat** (walk vs. self-bridge) but **only one for
  the Girl** (walk on ground/bridges) — so the Cat is the enabler and its soul
  budget is the true constraint (V3).
- The **door is open**; not every level is a lock-and-key puzzle. `doors.initial`
  and altar→door wiring are how a generator chooses between traversal vs. unlock.
