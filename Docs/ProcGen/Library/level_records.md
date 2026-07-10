# Escult Level Records (append-only)

Case log of levels that taught us something — bad, flawed, or exemplary.
Graph conventions: see `README.md`. Each record is a node addressable as
`[[<record-name>]]` and MUST link at least one `[[P*]]` principle or `[[ATOM]]`
so retrieval can reach it.

Entry format (keep each under ~25 lines; canvas optional for big levels):

```
## <name> · <date> · verdict: <taxonomy tag from design_principles.md>
links: [[P*]] / [[ATOM]] / [[V*]] / [[other-record]] · #tags
canvas: (optional, small levels only)
lesson: 1–3 sentences — what to avoid or repeat, phrased as an instruction.
```

---

## esn_worked_example · 2026-07-09 · verdict: flawed_accepted
links: [[P1]] [[THROW_LANE]] [[BRIDGE_GAP]] · #bypassable_mechanic
canvas:
```
###########
#...~~~...#
#@..~A~..X#
#C.1~~~..2#
#...~~~...#
###########
```
lesson: The intended route (open gate A, bridge through it on row 2) is bypassable:
the solver's witness bridges row 1 at the same cost and never touches gate A.
Acceptable in a tutorial-adjacent demo, a defect at medium+ (see [[P1]]). Always
diff the solver's `witness` against your intended solution; if the headline
mechanic is absent, close the bypass (wall off the free row, or price it above slack).

## test_border_gap · 2026-07-09 · verdict: rejected_invalid
links: [[V1]] · #unterminated_ray
lesson: A single missing perimeter wall cell fails [[V1]] (eastward throw never
terminates → engine hard-lock). The solver's lint catches it
("V1: right border not wall at row 1") — never ship without an empty `lint` array.

## level8_bait_bridge · 2026-07-09 · verdict: exemplar
links: [[P3]] [[P4]] [[BAIT_BRIDGE]] [[D3]] · #fair_trap #slack_calibration
lesson: The hand-made final level's signature trap: a bridgeable-looking gap
parallel to the intended throw lane, where bridging burns more souls than the
budget allows. It works because (a) the honest throw's wall backstop is visible
from the trap site, and (b) the player realizes the mistake *before* running out
of souls entirely — fair in hindsight ([[P3]]). This is the template for [[BAIT_BRIDGE]].

## level4_state_explosion · 2026-07-09 · verdict: solver_inconclusive
links: [[P11]] · #solver_intractable
lesson: A structurally-valid, lint-clean ESN reconstruction of the real Level 4
(20x13, with several rows of mostly-open pit as "sea" filler) hit the solver's
1,000,000-state cap without resolving, despite a hand-verifiable 4-soul solution
existing. Cause: wide open pit adjacent to walkable cells gives the Cat dozens of
irrelevant bridge choices at every step, and since bridge-sets are tracked
exactly, this blows up combinatorially long before the search reaches the actual
goal. Fixed by [[level4_reconstruction]] using `#` wall instead of pit for all
non-puzzle filler — see [[P11]]. A clean `lint: []` result does NOT mean the
solver will finish; state-space size is a separate concern from validity.

## level4_reconstruction · 2026-07-09 · verdict: exemplar
links: [[P11]] [[V2]] [[D7]] · #crossed_wiring #resolved_question
lesson: Compact (16x11), wall-filled reconstruction of the real Level 4's
topology (crossed altar/gate wiring, a gate with mixed pit/ground cells modeled
as two co-wired gate ids per [[README]]'s workaround pattern, a required
pre-door bridge) solved in 28k states: certified solvable, minCost 4, slack 5,
zero throws needed. It also resolved a real open question from project memory
(door reachability was never confirmed by hand) — in this reconstruction the
altar1/gate1 side turns out non-load-bearing and was honestly tagged as a decoy
rather than forced into false relevance. Caveat: this is a topology-faithful
reconstruction from documented landmarks, not a re-verified live tile dump —
treat its verdict as "the documented model is solvable," not as gospel about
every exact tile of the shipped scene.
