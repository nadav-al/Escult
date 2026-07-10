# Worked Example — Level 4, solver-certified (v2.0)

Supersedes the pre-ESN draft of this file (dictionary-based schema, world
coordinates, no solver run — see git history). This version is expressed in
current ESN (`01_Puzzle_Ruleset.md` §5.4), compiled to the current JSON schema
(`02_Generation_Pipeline.md` §2.2), and **actually solved** by
`Docs/ProcGen/Tools/solve_esn.ps1` — not illustrative-only.

Full artifacts: `Docs/ProcGen/Levels/level4_reconstruction/`. Case-log entries:
`Docs/ProcGen/Library/level_records.md#level4_reconstruction` (the result) and
`#level4_state_explosion` (a real solver-scaling failure hit along the way,
now [[P11]] in the design principles).

## What this is and isn't

This is a **topology-faithful reconstruction** of the real Level 4 from
documented facts (spawn positions, gate cell counts and over-pit/over-ground
breakdown, crossed altar wiring, door position/open-state — all previously
confirmed via a live tilemap dump, see project memory), compacted to a
solver-tractable scale. It is **not** a re-verified live tile dump: exact
filler geometry (which cells are pit vs. wall away from the landmarks) is an
honest interpretive reconstruction, same epistemic status the original
schematic always had. Where documentation was ambiguous (see "Open question
resolved" below), that ambiguity is disclosed, not papered over.

## The confirmed facts this reconstruction preserves

- Cat and Girl spawn on a shared, connected ground plain (no bridge needed
  between them).
- Two gates, crossed wiring: the north altar opens the *south* gate, the south
  altar opens the *north* gate.
- One gate (real "Gate 2") has **mixed terrain** — 3 cells over ground, 1 over
  pit — which the current schema can't express as a single gate (`overPit` is
  one flag per gate id). Modeled here using the documented workaround: two gate
  ids (`N` for the 3 ground cells, `P` for the 1 pit cell) wired to fire
  together from the same altar. This is now the recommended pattern for any
  future mixed-terrain gate — see `Docs/ProcGen/Library/README.md`.
- The other gate (real "Gate 1") is all-over-pit, 3 cells.
- The door is always open (pure traversal puzzle, no altar wired to it) —
  matches the original's `initial: OPEN`.
- Both altars are reachable via bridging without either gate being open first
  (a confirmed fact from a live BFS at the time).

## ESN source

```
################
#.....NNNP..~O.#
#...1###########
#..#############
#..#############
#..#############
#....C...@.....#
#######~##.#####
#######2##S#####
##########S.####
################

souls: 9
1 -> S
2 -> N, P
P: over=PIT
S: over=PIT
decoys: 1, S
```

Reading it: Cat and Girl start on a shared plain (row 6). A narrow ground shaft
(col 1–2) leads north to a corridor (row 1) where altar `1` sits in a side
alcove (does not block the corridor itself — altars are solid, so they must
never sit *in* a single-width path) and gate `N`+`P` blocks the way further
east to the open door. South of the plain, a single-cell pit strait leads to
altar `2`'s pocket; a separate, isolated 2-cell pit gate `S` (wired from altar
`1`) leads to a dead-end pocket with nothing in it.

## Solver-certified result

```
solvable: true
minCost:  4        (souls)
slack:    5         (of 9 budget)
statesVisited: 28,525
lint: []
altarsUsedInWitness: ["2"]
unusedNonDecoyAltars: []     (altar 1 correctly tagged as decoy — see below)
```

Certified minimal solution (4 souls; cleaned of zero-cost move noise — full
raw witness in `level4_reconstruction.solution.json`):

1. Cat walks to the pocket-crossing cell, **bridges** it (−1 soul), reaches
   altar `2`.
2. Cat **sacrifices** altar `2` (−1 soul) — opens gate `N` (now free ground)
   and gate `P` (now walkable-once-bridged pit).
3. Cat travels north via the shaft, crosses the now-open `N` cells, **bridges**
   gate `P`'s cell (−1 soul).
4. Cat continues to the pre-door pit cell and **bridges** it (−1 soul) — this
   is the "one more bridge right at the doorstep" the original schematic
   flagged as unconfirmed.
5. Girl walks the now-fully-bridged route from the plain to the doorstep and
   **exits**. No throw is used anywhere in the minimal solution.

## Open question resolved (with a caveat)

Project memory flagged Level 4's door reachability as **never confirmed by
hand** — a live BFS at the time found the door's only open neighbor unbridged
and left open whether the Girl needed to be thrown the last stretch. This
reconstruction gives a certified answer for the *documented model*: reachable
in 4 souls, no throw required, via one additional bridge at the doorstep. The
caveat stands: this settles the question for the reconstruction, not
definitively for the exact shipped tile data (a live re-dump would be needed
for that final word).

A second finding, honestly reported rather than engineered around: in this
reconstruction, **altar 1 / gate S (the "south lock") is not load-bearing** —
the minimal-cost witness never touches it. Documentation never actually pinned
down what gate 1 gates (both altars are confirmed reachable independent of
either gate's state), so rather than invent a functional role for it, it's
modeled — and honestly tagged (`decoys: 1, S`) — as a decorative/decoy element.
If a future live re-dump reveals it *was* load-bearing in the shipped level,
that would be a genuine, useful correction to make here.

## Why this exercise was instructive for the pipeline itself

- **The first draft (20×13, original-scale, wide-open pit "sea") hit the
  solver's 1,000,000-state cap without resolving**, despite a hand-verifiable
  solution existing. Cause: every pit cell adjacent to a walkable area is a
  legal (if pointless) bridge target, and the solver's bridge-set state key
  tracks exact cell combinations — wide-open pit multiplies the reachable
  state space combinatorially. This produced [[P11]] in the design library:
  use wall, not pit, for anything that isn't an actual puzzle element.
- **Crossed wiring + a shared/decoy gate is a legitimate, low-cost way to add
  misdirection (D7)** without inflating solution length (D1) — this level
  scores easy tier (D1=4, slack=5) despite two gates and crossed wiring,
  because neither gate individually gates much once you see through it.
- **The mixed-terrain-gate workaround (two co-wired gate ids) is now proven
  in practice**, not just theorized — it solved correctly and the solver
  treated both cells as a single logical lock (fired together, no partial
  states possible) exactly as intended.
