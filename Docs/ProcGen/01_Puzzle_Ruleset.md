# Escult — Procedural Puzzle Ruleset (v1.0)

Formal, engine-agnostic definition of what an Escult puzzle *is*, what makes it
valid, and how difficulty is measured. Consumed by human designers, solvers,
and AI level-generation agents. Serialization & pipeline: see `02_Generation_Pipeline.md`.

---

## 0. Abstraction contract

- The world is a finite 4-connected grid. Every entity occupies exactly one cell
  (gates occupy a cell *set*). Continuous physics, diagonals, and collider sizes
  are implementation noise and MUST NOT be relied on by any puzzle.
- Focus-swapping (Tab) is free and unlimited → the abstract model treats Girl and
  Cat as independently movable between commitment points; "focus" is not state.
- `R` (level reset) always exists. Reaching an unwinnable state is allowed
  (traps are a design tool); starting in one is not (see V2).

## 1. Building blocks

### 1.1 Terrain (per cell, static)
| Symbol | Name   | Walk | Blocks throw ray | Notes |
|--------|--------|------|------------------|-------|
| `G`    | Ground | yes  | no               | |
| `P`    | Pit    | no   | no (Cat flies over) | Convertible to ground via BRIDGE |
| `W`    | Wall   | no   | **yes** (backstop) | Only throw terminator besides closed gates |

### 1.2 Entities (static placement, dynamic state)
| Entity | Occupies | Dynamic state | Interaction |
|--------|----------|---------------|-------------|
| Gate   | cell set (over `G` or `P`!) | OPEN/CLOSED | CLOSED ⇒ its cells act as `W` (blocks walk, blocks throw, forbids BRIDGE under it). OPEN ⇒ underlying terrain applies. A gate over `P` still needs a bridge after opening. |
| Altar  | 1 cell (blocks walk, **not** throw rays — Cat flies over; illegal landing cell) | none (stateless trigger) | Cat on an adjacent cell may SACRIFICE: **toggles ALL wired targets** (XOR — re-use re-closes; parity puzzles are intentional) |
| Door   | 1 cell (blocks walk **and throw rays** — engine: door is Wall-layer, a legal backstop) | OPEN/CLOSED | Win = Girl adjacent to an OPEN door. May start open or be altar-wired (it is just another toggle target) |
| Girl   | 1 cell | position, `carrying` flag | Mandatory. The only win-relevant character |
| Cat    | 1 cell | position ∨ HELD ∨ DEAD; `souls` | The resource-bearer. Optional per level (`cat_in_level=false` ⇒ girl-only level) |

### 1.3 Resource
- `souls`: integer budget (default **9**), reset every level, **only decreases**.
  Every SACRIFICE, every BRIDGE, and every pit-landing costs exactly 1.
- At `souls == 0` the Cat is DEAD: removed from play, control locks to Girl.
  A dead Cat does not block winning — full sacrifice is a legal strategy.

## 2. Action alphabet (macro-actions)

Free actions cost 0 souls and any number may occur between costed actions.

| Op | Actor | Precondition | Effect | Cost |
|----|-------|--------------|--------|------|
| `MOVE(actor,to)` | either | 4-connected path over walkable cells (walkable = `G` ∪ bridges, minus CLOSED-gate cells, minus `W`; characters never block each other) | position ← `to` | 0 |
| `PICKUP` | Girl | Girl and Cat within adjacency; Cat not DEAD | Cat ← HELD | 0 |
| `DROP` | Girl | carrying | Cat placed at Girl's cell | 0 |
| `THROW(dir)` | Girl | carrying | Ray-cast from Girl's cell along `dir`; flies over `G`/`P`/altars; stops at first `W`, CLOSED-gate cell, or **door** (doors are Wall-layer backstops); lands on cell *before* it. Landing on an altar cell is illegal (skip that throw). Land on walkable ⇒ Cat there. Land on `P` ⇒ `souls−1`, Cat returns to Girl's cell (a paid "recall"). **Ray MUST terminate** (see V1) | 0 (1 if pit-landing) |
| `SACRIFICE(altar)` | Cat | Cat adjacent to altar; `souls ≥ 1`; **Girl not standing on any cell of any gate wired to this altar** (anti-crush interlock — applies even to currently-OPEN gates) | toggle every wired gate/door; `souls−1` | 1 |
| `BRIDGE(cell)` | Cat | Cat adjacent to `cell`; `cell` is `P`; `cell` not covered by a CLOSED gate; `souls ≥ 1` | `cell` becomes permanent walkable bridge; `souls−1`; if souls hit 0 the bridge still completes, then Cat dies | 1 |
| `EXIT` | Girl | Girl adjacent to an OPEN door | **WIN** | 0 |

Derived rule — Cat crossing methods: walk (needs path), be thrown (needs a wall
backstop past the gap, aligned with Girl's cell), or bridge its own way (1 soul
per pit cell). Girl crossing methods: walk or bridges only — she can never be thrown.

## 3. Validity constraints (a level is VALID iff all hold)

| ID | Constraint |
|----|-----------|
| V1 | **Closed arena**: every possible throw ray terminates. Sufficient condition: the playfield's bounding perimeter is `W`. (An unterminated ray = Cat flies forever = hard-lock.) |
| V2 | **Initial solvability**: a solver (BFS/A* over §5 state space) proves ≥1 win path from the initial state. Traps — reachable states with no win path — are permitted and measured (D3), never required to be entered. |
| V3 | **Soul feasibility**: `min_cost(solution) ≤ soul_budget`. Record `slack = budget − min_cost` (difficulty input, D2). |
| V4 | **Non-triviality** (skip for tutorial tier): the minimal solution uses ≥ the tier's required mechanic count (§4 table). Girl must not be able to walk straight to an open door unless intended. |
| V5 | **Spawn sanity**: both spawns on `G`, not under any gate cell, not adjacent-trapped (each character can make ≥1 move). |
| V6 | **Element relevance**: every altar/gate/door either appears in some minimal solution or is explicitly tagged `decoy` (decoys are a difficulty tool, not an accident). |
| V7 | **Cat-death ordering**: if the minimal solution spends the last soul, all subsequent steps must be Girl-only (solver enforces automatically; listed for designer awareness). |
| V8 | **Anti-crush compatibility**: every SACRIFICE in the intended solution is executable with the Girl positioned off the wired gates' cells (solver enforces; wiring a gate across the *only* Girl-holding cell of an island makes its altar unusable). |
| V9 | **Determinism**: no element may depend on timing, physics, or animation. State + action ⇒ unique next state. |

## 4. Difficulty scaling vectors

| ID | Vector | Measure | Easy → Hard direction |
|----|--------|---------|----------------------|
| D1 | Solution length | # costed actions + # commitment moves in minimal solution | longer |
| D2 | Soul slack | `budget − min_cost` | 9 → 0 |
| D3 | Trap depth | # reachable dead-end branches × how many souls a player can sink before the trap is detectable (Level-8 pattern: bridging where a throw was intended) | more/deeper |
| D4 | Wiring degree | edges in the altar→target bipartite graph; # targets shared by ≥2 altars (parity conflicts) | denser |
| D5 | Toggle parity | max # times one altar must fire in the minimal solution | 1 → n |
| D6 | Interleaving | # forced Girl↔Cat alternations (order-sensitive segments) | more |
| D7 | Misdirection | avg grid distance between an altar and its wired targets (cross-wiring) | farther |
| D8 | Throw complexity | # throws in minimal solution + # setup actions required to *create* a throw lane (open gate / build backstop-adjacent path first) | more |
| D9 | Topology | # disconnected ground islands | more |
| D10| Decoys | # tagged decoy elements | more |

### Tier calibration (anchored: current final level ≙ `extreme`)
| Tier | D1 | D2 slack | D3 traps | D4–D5 wiring/parity | D6 swaps | D8 throws |
|------|----|----------|----------|---------------------|----------|-----------|
| tutorial | 1–3 | ≥6 | 0 | ≤1 edge, parity 1 | ≤1 | ≤1 |
| easy     | 3–6 | 4–6 | 0 | ≤2 edges, parity 1 | ≤2 | ≤1 |
| medium   | 5–9 | 2–4 | ≤1 shallow | ≤4 edges, parity ≤2 | 2–4 | 1–2 |
| hard     | 8–14| 1–2 | 1–2 | shared targets, parity ≤3 | 4–6 | 2–3 |
| extreme  | 12+ | 0–1 | ≥2, deep (≥2 souls sunk) | shared + cross-wired | 6+ | 3+ |

## 5. Data structures (theoretical; serialization in doc 02)

### 5.1 LevelTopology (static)
```yaml
LevelTopology:
  version: 1
  bounds: {w: int, h: int}          # perimeter must satisfy V1
  terrain: ["WWWWW", "WGPGW", ...]  # row-major strings of G|P|W
  gates:   {g1: {cells: [[x,y],...], initial: CLOSED}}   # cells may be over G or P
  altars:  {a1: {cell: [x,y], targets: [g1, g2, d1]}}    # toggles ALL targets
  doors:   {d1: {cell: [x,y], initial: CLOSED}}
  spawns:  {girl: [x,y], cat: [x,y] | null}              # null ⇒ girl-only level
  soul_budget: 9
  decoys: [g3]                       # V6 exemptions
```

### 5.2 PuzzleState (dynamic; the solver's node)
```yaml
PuzzleState:
  girl: [x,y]
  cat:  {status: ON_GROUND|HELD|DEAD, cell: [x,y]|null}  # HELD ⇒ cell = girl
  souls: int
  gates: {g1: OPEN|CLOSED, ...}
  doors: {d1: OPEN|CLOSED, ...}
  bridges: [[x,y], ...]              # monotonically growing set
# canonical hash: (girl, cat.status, cat.cell, souls, gate bits, door bits, bridge set)
# walkable(c) = (terrain[c]==G or c in bridges) and no CLOSED gate covers c
```
State space is finite and small (positions × 10 souls × 2^gates × 2^doors ×
bridge subsets bounded by budget) — exhaustive solving is tractable.

### 5.3 SolutionPath (certificate of V2)
```yaml
SolutionPath:
  min_cost: 7                        # souls spent
  steps:
    - {op: MOVE,      actor: CAT,  to: [4,2]}
    - {op: SACRIFICE, altar: a1}                 # -1 soul
    - {op: MOVE,      actor: GIRL, to: [3,2]}
    - {op: PICKUP}
    - {op: THROW,     dir: E,      lands: [9,2]} # deterministic, recorded
    - {op: BRIDGE,    cell: [10,3]}              # -1 soul
    - {op: MOVE,      actor: GIRL, to: [10,4]}
    - {op: EXIT}
  # each step must satisfy §2 preconditions given the state after prior steps
```

### 5.4 Escult Sketch Notation (ESN) — the canonical readable form

One format that is simultaneously **sketchable on paper, readable by a human at a
glance, parseable by a program, and emittable by an AI**. It is a fixed-width
**glyph canvas** + a **legend**, and it round-trips 1:1 with `LevelTopology`
(§5.1) and the JSON (doc 02). It is the primary artifact designers and agents
author; JSON is the compiled output.

**Glyph canvas** — a rectangle of characters, one per cell, row-major (row 0 = top).

Terrain (exactly one per cell):
| `#` Wall | `.` Ground | `~` Pit |

Overlays (a cell's glyph; ground is assumed beneath unless the legend says `over=PIT`):
| `@` Girl spawn | `C` Cat spawn | `1`–`9` Altar (its id) | `A`–`Z` Gate cell (same letter = same gate) | `X` Door closed | `O` Door open | `=` Bridge (state/solution sketches only) |

Reserved letters `C`, `O`, `X` are never used as gate ids (they are Cat / open-door /
closed-door). >9 altars: keep numbering in the legend only.
Multiple doors: door ids are assigned in canvas reading order (`d1`, `d2`, …) and
referenced by id in the legend; with a single door, `X`/`O` may be referenced directly.

**Legend** — plain `key: value` / arrow lines; everything the canvas can't show:
```
souls: 9
1 -> A, X            # altar 1 toggles gate A and door X
2 -> A               # altar 2 also toggles A (re-firing re-locks it)
A: initial=CLOSED over=PIT
X: initial=CLOSED
decoys: 2            # machine-readable decoy tag (V6); comments are for humans only
```

**Worked example** (easy tier; min-cost 4, slack 5):
```
###########
#...~~~...#
#@..~A~..X#
#C.1~~~..2#
#...~~~...#
###########
```
```
souls: 9
1 -> A, X
2 -> A
A: initial=CLOSED over=PIT
X: initial=CLOSED
decoys: 2
```
Intended solution: Cat sacrifices altar `1` (opens gate `A` + door `X`, −1) →
Cat bridges the three pit cells of row 2 incl. the now-open gate cell (−3) →
Girl walks across and exits. Altar `2` is bait: firing it re-locks gate `A` and
burns a soul (the Level-8 trap pattern in miniature).

**Why this satisfies all four readability requirements**
| Requirement | How ESN meets it |
|---|---|
| Captures all mechanics | terrain, gates (+cell sets, initial state, over-pit), altars, wiring, doors, spawns, souls, bridges, decoys all expressible; new mechanics add a glyph + legend key (§6) |
| Human **and** AI/program readable | canvas = 2-D char array (trivial parse); legend = key/arrow lines; no nesting, no escaping |
| Easy to describe / represent | it *is* the level as text — dictate it over chat, paste in a commit, diff it in git |
| Easy to visualize / view | fixed-width already looks like the map; deterministic glyph→SVG renderer (doc 02) gives a colored view with zero layout logic |

**Parse/serialize rules (for the pipeline):** canvas width/height = char dims;
`bounds` from them; each `A`–`Z` run collects into that gate's `cells`; digits →
altar cells; `@`/`C` → spawns; `X`/`O` → door + initial; legend supplies wiring,
soul budget, gate `initial`/`over`, decoy tags. The mapping is total and
reversible, so JSON ⇄ ESN is lossless and either can be the stored source.

## 6. Extensibility protocol

New mechanics plug in without changing the solver core. A mechanic is registered as:
```yaml
MechanicDef:
  id: soul_pickup                    # example: engine hook already exists (IncreaseSoul)
  state_vars: {collected: bool}      # added to PuzzleState
  terrain_or_entity: entity          # placement rule
  actions:                           # new rows for the §2 table
    - {op: COLLECT, actor: CAT, pre: "cat adjacent & !collected",
       effect: "souls+1; collected=true", cost: 0}
  difficulty_hooks: [D2]             # which vectors it can modulate
```
Rules for any future mechanic: deterministic (V9), expressible as
precondition/effect on grid state, cost stated in souls (or a new declared
resource). Reserved candidates seen in the engine: soul pickups (`IncreaseSoul`),
platforms (`Platform` layer), steppables (`Steppable` tag).
