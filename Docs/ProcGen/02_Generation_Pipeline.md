# Escult — Procedural Generation Pipeline (v1.0)

How a valid Escult level is produced, stored, and requested from an AI agent.
Depends on `01_Puzzle_Ruleset.md` (the world rules; referenced below as **R§n**).
No engine code here — this is the logical pipeline contract.

Artifacts produced per level:
| File | Content | Consumer |
|---|---|---|
| `<name>.esn.txt` | ESN canvas + legend (R§5.4) — **authoring source of truth** | humans, AI agents, git diffs |
| `<name>.level.json` | compiled `LevelTopology` (§2.2) | Unity importer, solver |
| `<name>.solution.json` | certified `SolutionPath` (R§5.3) | validator, hint system, QA |
| `<name>.report.json` | difficulty vectors D1–D10, tier, V1–V9 results | curation, playlists |

---

## 1. Generation flow

Two entry modes, one shared verification back-end:

```
SEED MODE                        PROMPT MODE
seed + GenerationSpec            natural-language request → AI agent (§3)
      │                                │
      ▼                                ▼
S1–S5 constructive stages        agent authors ESN directly
      └────────────┬───────────────────┘
                   ▼
        S6 SOLVE → S7 VALIDATE → S8 SCORE
                   │ fail: repair/mutate (bounded) or reject
                   ▼
        S9 EMIT (esn + json + solution + report)
```

### 1.1 Stages

| # | Stage | Logic |
|---|-------|-------|
| S1 | **Budgeting** | From `GenerationSpec` (tier, size cap, atom whitelist, trap count, seed) pick target values for D1–D10 inside the tier row of R§4. Seeded RNG ⇒ fully reproducible. |
| S2 | **Arena** | Lay the wall perimeter (satisfies V1 by construction), then partition the interior into 1–D9 ground islands separated by pit moats. |
| S3 | **Solution-first construction** | Place the Door on a goal island. Walk **backwards** from it, inserting puzzle atoms (§1.2) as chained locks until reaching the spawn island. Maintain a running soul ledger; stop when `budget − ledger` hits the tier's target slack. This guarantees an intended solution exists *by construction* — the solver later re-proves it independently (V2). |
| S4 | **Wiring** | Assign altar→target edges. For medium+ tiers deliberately add shared targets (parity, D4/D5) and cross-wire altars far from their gates (D7). Respect V8: every intended SACRIFICE must leave the Girl a legal standing spot. |
| S5 | **Traps & decoys** | Inject dead-end branches (BAIT_BRIDGE, DECOY_ALTAR) per D3/D10 targets. Tag every decoy (`decoys:` legend line). Traps must be *enterable but never mandatory*. |
| S6 | **Solve** | Reference solver (§1.3) exhaustively searches the abstract state space. Outputs: min-cost solution, all minimal solutions, reachable-state graph. Unsolvable ⇒ back to S3 with a repair hint. |
| S7 | **Validate** | Check V1–V9 mechanically (V2/V3/V7/V8 come free from the solver; V1/V5/V6 are static lints; V4 compares the min solution's mechanic usage to the tier floor). Any failure ⇒ targeted repair (e.g., widen a moat, re-wire an altar) with a bounded retry count, else reject the seed. |
| S8 | **Score** | Compute D1–D10 from solver output (trap depth = souls sinkable in dead-end branches of the state graph). If outside the requested tier band ⇒ mutate (add/remove one atom, tighten slack) and go to S6. |
| S9 | **Emit** | Write the four artifacts. The JSON embeds the ESN so any single file is self-describing. |

### 1.2 Puzzle-atom library (composable lock motifs for S3)

| Atom | Recipe | Soul cost | Hooks |
|------|--------|-----------|-------|
| `BRIDGE_GAP(n)` | pit strait of width *n* between islands | n | D1 D2 |
| `THROW_LANE` | pit gap with a Wall backstop past it, aligned with a Girl-reachable cell; Cat-only crossing (Girl can never be thrown) | 0 | D8 |
| `GATE_LOCK` | closed gate across the only corridor; its altar elsewhere | 1 | D4 |
| `OVER_PIT_GATE` | gate cells over `P` — must be opened *and then* bridged | ≥2 | D1 D8 |
| `REMOTE_KEY` | altar on an island the Cat can only reach by throw or paid bridging | ≥1 | D6 D8 |
| `PARITY_TWIST` | two altars sharing a target — firing one un-does part of the other; order-sensitive, may force re-fires | ≥2 | D4 D5 |
| `GIRL_INTERLOCK` | wired gate cells overlap the Girl's natural waiting spot — she must reposition before the sacrifice | 0 | D6 |
| `FINALE_SACRIFICE` | budget engineered so the last bridge kills the Cat; remaining path is Girl-walkable | — | D2 |
| `BAIT_BRIDGE` *(trap)* | a bridgeable-looking gap parallel to the intended THROW_LANE whose full cost exceeds the slack (the final-level pattern) | trap | D3 |
| `DECOY_ALTAR` *(trap)* | altar whose firing only re-locks gates the player needs open | trap | D3 D10 |

Atoms chain: the *key* of one lock is placed behind the *door* of the next.
New mechanics (R§6) extend this table with their own atoms.

### 1.3 Reference solver requirements

- Model: exactly R§2 actions over R§5.2 states; canonical hash for dedup.
- Move abstraction: never enumerate per-cell walks. From any state, each character's
  free-move closure is a flood fill; successor states are generated only at
  **commitment points** — cells adjacent to altars, bridgeable pit cells, pickup
  adjacency, throw origins (cells whose ray terminates usefully), door adjacency.
- Search: BFS (uniform action cost) or Dijkstra weighted by souls when only
  min-cost matters. State space is small (R§5.2) — exhaustive is fine.
- Required outputs: `min_cost`, one witness `SolutionPath`, count of distinct
  minimal solutions, and the reachable-state graph with each state labeled
  solvable/dead-end (feeds D3 and the trap audit).

---

## 2. Serialization

### 2.1 File layout

```
Docs/ProcGen/Levels/            # or Assets/StreamingAssets/Levels/ once the importer exists
  twisted_moat_01/
    twisted_moat_01.esn.txt
    twisted_moat_01.level.json
    twisted_moat_01.solution.json
    twisted_moat_01.report.json
```

### 2.2 `level.json` schema

Unity-friendly: **arrays of objects only, no dictionaries** (JsonUtility cannot
deserialize `Dictionary`), no nulls (use empty arrays / sentinel `[-1,-1]`).

```json
{
  "version": 1,
  "name": "twisted_moat_01",
  "seed": 8842,
  "tier": "easy",
  "bounds": { "w": 11, "h": 6 },
  "terrain": [
    "WWWWWWWWWWW",
    "WGGGPPPGGGW",
    "WGGGPPPGGGW",
    "WGGGPPPGGGW",
    "WGGGPPPGGGW",
    "WWWWWWWWWWW"
  ],
  "gates":  [ { "id": "A", "cells": [[5,2]], "initialClosed": true, "overPit": true } ],
  "altars": [ { "id": "1", "cell": [3,3], "targets": ["A", "d1"] },
              { "id": "2", "cell": [9,3], "targets": ["A"] } ],
  "doors":  [ { "id": "d1", "cell": [9,2], "initialOpen": false } ],
  "spawns": { "girl": [1,2], "cat": [1,3], "catInLevel": true },
  "soulBudget": 9,
  "decoys": ["2"],
  "esn": ["###########","#...~~~...#","#@..~A~..X#","#C.1~~~..2#","#...~~~...#","###########"]
}
```

Rules:
- `terrain` holds **pure terrain** (`G|P|W`); gates/altars/doors/spawns are *not*
  baked into it — they are overlays, exactly as in ESN. `terrain[row][col]`, row 0 = top.
- All coordinates are `[col,row]` in that same frame. ESN ⇄ JSON is a pure
  re-encoding (lossless both ways, R§5.4).
- `esn` is embedded so the JSON is human-inspectable on its own; on conflict the
  compiler errors out (they can never legally disagree).

### 2.3 Unity import mapping (conceptual — for the future importer)

| JSON field | Existing engine target |
|---|---|
| `terrain` `G` | Ground tilemap tile |
| `terrain` `P` | Hell tilemap tile (solid collider, `Hell` tag) |
| `terrain` `W` | Walls tilemap tile (**Wall layer** — throw backstop) |
| `gates[i]` | gate GameObject: Tilemap with `cells`, Wall layer, `GateContoller.INITIAL_GATE_STATUS = initialClosed` (engine convention: active GameObject = closed/locked) |
| `altars[i]` | `Alter.prefab` at `cell`; `AlterController.connectedObjects` = resolved `targets` |
| `doors[i]` | `Door.prefab` at `cell`; `LevelManager.isDoorOpen = initialOpen` |
| `spawns` | `LevelManager.girlPos` / `catPos`; `catInLevel` → `isCatInLevel` |
| `soulBudget` | `SoulsController.CAT_INIT_SOULS` |
| grid mapping | Unity cell = `(col + originX, originY − row)`; the importer picks the origin (ESN rows grow downward, Unity Y grows upward) |

### 2.4 `solution.json` / `report.json`

```json
{ "minCost": 4,
  "steps": [
    { "op": "MOVE",      "actor": "CAT",  "to": [2,3] },
    { "op": "SACRIFICE", "altar": "1",    "soulsAfter": 8 },
    { "op": "MOVE",      "actor": "CAT",  "to": [3,2] },
    { "op": "BRIDGE",    "cell": [4,2],   "soulsAfter": 7 },
    { "op": "MOVE",      "actor": "CAT",  "to": [4,2] },
    { "op": "BRIDGE",    "cell": [5,2],   "soulsAfter": 6 },
    { "op": "MOVE",      "actor": "CAT",  "to": [5,2] },
    { "op": "BRIDGE",    "cell": [6,2],   "soulsAfter": 5 },
    { "op": "MOVE",      "actor": "GIRL", "to": [8,2] },
    { "op": "EXIT" } ] }
```
`report.json`: `{ "tier": "...", "vectors": {"D1": n, … , "D10": n}, "checks": {"V1": "PASS", …}, "solutionCount": n }`.

---

## 3. AI-agent design kit

### 3.1 Agent operating instructions (paste verbatim as the agent's brief)

```
You are an Escult level designer.
Authoritative rules: Docs/ProcGen/01_Puzzle_Ruleset.md. Author levels in ESN (R§5.4).

Mandatory workflow, in this order:
1. Parse the DESIGN REQUEST (tier, size cap, required atoms, traps).
2. Pick puzzle atoms from 02_Generation_Pipeline.md §1.2 and chain them into a
   solution skeleton (key of each lock behind the door of the next).
3. Write the intended SOLUTION FIRST: ordered op list with a running soul
   ledger (9 → …). If the ledger goes negative, redesign before drawing anything.
4. Draw the ESN canvas around that solution. Perimeter must be '#'.
5. Only after the main path works, add the requested traps/decoys; tag them
   with a 'decoys:' legend line.
6. Run the self-check (§3.4). Fix and re-run until every item passes.
7. Emit exactly the OUTPUT CONTRACT (§3.3). No other prose.

Invariants you must never violate:
- Altars TOGGLE all wired targets (re-firing re-locks; costs a soul every time).
- A throw is a straight ray that stops only at a Wall, a CLOSED gate, or a door
  (doors are backstops; altars are NOT — the Cat flies over them); it lands
  one cell before the blocker. Every ray must terminate ('#' perimeter).
- The Girl is never thrown and cannot cross pit except on bridges.
- A gate over pit ('over=PIT') needs opening AND bridging.
- Souls only decrease; sacrifice, bridge, and pit-landing cost 1 each.
- SACRIFICE is illegal while the Girl stands on any cell of a wired gate.
- Win = Girl adjacent to an OPEN door; the Cat never needs to reach it and may die.
- Only the initial state must be solvable; reachable traps are allowed and welcome.
```

### 3.2 Request template (what a human sends the agent)

```
DESIGN REQUEST
tier: hard                     # tutorial | easy | medium | hard | extreme
size_max: [20, 12]             # cols, rows incl. perimeter
must_use: [THROW_LANE, PARITY_TWIST]
forbid: []                     # atoms to exclude
traps: 1                       # BAIT_BRIDGE / DECOY_ALTAR count
souls: 9
name: <slug>
notes: <free text — theme, mood, specific ideas>
```
Every field except `tier` may be omitted (defaults: size 16×10, no forced atoms,
traps per tier table, souls 9).

### 3.3 Output contract (the agent's entire reply)

```
== LEVEL <slug> · tier <t> ==
<ESN canvas>

<ESN legend>

== SOLUTION (souls 9 -> n) ==
1. MOVE CAT (2,3)
2. SACRIFICE 1        # souls 8 — opens A, opens d1
...
N. EXIT

== DIFFICULTY ==
D1..D10: <estimates>  ⇒ tier claim: <t>

== CHECKS ==
V1..V9: PASS — <one-line justification each>
```

### 3.4 Self-verification checklist (agent must answer every item)

1. **V1** Is every border cell `#`?
2. **V2** Replay your SOLUTION step by step against R§2 preconditions — does every
   step's precondition hold in the state produced by the previous steps?
3. **V3** Ledger: does the soul count stay ≥ 0, and does the final spend match `min_cost ≤ souls`?
4. **V4** Does the solution use at least the tier's required mechanics (R§4 table)?
5. **V5** Are `@` and `C` on ground, off gate cells, each with ≥1 legal move?
6. **V6** Does every altar/gate/door appear in the solution or in `decoys:`?
7. **V7** If the Cat dies in your solution, are ALL later steps Girl-only?
8. **V8** For each SACRIFICE, where exactly is the Girl standing? Confirm it is not a wired-gate cell.
9. **V9** Does anything rely on timing/physics? (Must be no.)
10. **ESN lint**: gate letters unique and ∉ {C,O,X}; every canvas symbol defined in
    the legend and vice versa; every wiring target exists on the canvas; every
    throw in the solution has its backstop wall actually present on the ray.

### 3.5 Design-knowledge library (the fun feedback loop)

Solvability is certified by the solver; **fun is steered by the library** in
`Docs/ProcGen/Library/`:

| File | Role | Mutation rule |
|---|---|---|
| `README.md` | Graph conventions + retrieval recipes | Stable |
| `design_principles.md` | Curated taste — ~10 tagged principles every design must respect (or explicitly argue against) | Edited/distilled; capped ~80 lines |
| `level_records.md` | Append-only case log: rejected, flawed, and exemplary levels with a 1–3 sentence `lesson` each | Append-only |

The library is an Obsidian-style **wikilink knowledge graph** used as grep-RAG:
nodes are stable ids (`[[P1]]` principles, `[[V1]]`/`[[D1]]` constraints/vectors,
`[[THROW_LANE]]` atoms, `[[record-name]]` cases); edges are `[[wikilinks]]`;
retrieval = lexical grep for the request's atoms + 1-hop link expansion. No
embeddings by design — the corpus is small, ids are unambiguous, and grep is
deterministic, free, and git-native.

Loop: the agent reads principles + greps records by atom-relevant tags **before**
designing (S1), and appends a record **after** any design-level solver rejection,
any human play feedback, or any reusable discovery (S9+). When ≥3 records teach
the same lesson, it is distilled into one principle. Human feedback
(`player_feedback_good/bad`) is the only channel through which real play
experience enters the system — it must always be recorded.

### 3.6 Known agent failure modes (check these twice)

- Throw ray with no wall behind the gap (V1 hard-lock) — pit is NOT a backstop.
- Forgetting toggle semantics: using an altar twice and counting only one soul,
  or leaving a shared gate re-locked by a later sacrifice (parity bug).
- Bridging under a CLOSED gate (illegal) or walking the Girl through a throw-only crossing.
- Soul ledger counting sacrifices but not pit-landings of a deliberate recall-throw.
- Gate id collision with reserved letters C/O/X.
- Declaring a gate `over=PIT` in the legend but drawing walkable ground around it
  that lets the Girl bypass the whole lock (accidental trivial path — V4).
```
