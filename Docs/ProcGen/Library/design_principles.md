# Escult Design Principles (curated, living)

The distilled taste of the project. The level-designer agent MUST read this file
before designing and treat every principle as a soft constraint (violating one
requires stating why). Solvability is certified by the solver; **this file is
about fun** — the part no script can check.

Graph conventions: see `README.md`. Principles are nodes `[[P1]]`…`[[P10]]`; each
links its evidence (records) and the atoms/constraints it governs.

Curation rule: keep this file under ~80 lines. When `level_records.md` accumulates
≥3 records teaching the same lesson, distill them into ONE principle here and link
the records as evidence. Principles may be edited; records are append-only.

| # | Principle | Why | Links |
|---|-----------|-----|-------|
| P1 | The headline mechanic must be load-bearing: the solver's witness must actually use it. If the witness bypasses your [[THROW_LANE]] by bridging elsewhere, the level you built is not the level you designed. | A bypassed mechanic = the player never meets the idea; the level plays as a corridor. | evidence: [[esn_worked_example]] · governs [[THROW_LANE]] [[GATE_LOCK]] [[OVER_PIT_GATE]] · #bypassable_mechanic |
| P2 | Escult's fun is resource planning, not navigation. Prefer open, readable islands over maze corridors; the player should spend souls thinking, not steps walking. | Long walks between decisions dilute tension without adding difficulty. | relates [[D9]] · #maze_corridor |
| P3 | Traps must be fair-in-hindsight: after falling in, the player should be able to say "I *should* have seen the throw." Never hide the honest route off-screen or behind notation-only knowledge. | [[level8_bait_bridge]] works because the wall backstop was visible all along. | evidence: [[level8_bait_bridge]] · governs [[BAIT_BRIDGE]] [[DECOY_ALTAR]] [[D3]] · #unfair_trap |
| P4 | Calibrate slack to tension: slack ≥ 6 plays as a sandbox (fine for tutorials only); slack 0 is brutal and should be reserved for extreme tier. | Souls are the tension meter; too many and nothing matters, too few and it reads as trial-and-error. | governs [[D2]] · evidence: [[level8_bait_bridge]] · #slack_miscalibrated |
| P5 | At most ONE unfamiliar atom-combination per level; stack *familiar* atoms for difficulty instead. | Players parse one new idea per level; two compound into confusion, not challenge. | #idea_overload |
| P6 | Parity re-fires of the same altar: ≤2 below extreme tier. Three+ toggles of one altar reads as busywork, not insight. | The "aha" is realizing the order, not grinding the switch. | governs [[PARITY_TWIST]] [[D5]] · #parity_grind |
| P7 | Telegraph interlocks: if the Girl must step off a gate cell before a sacrifice, the gate's cells should visibly overlap where she'd naturally wait. | An interlock the player never notices is a rule violation dressed as a puzzle. | governs [[GIRL_INTERLOCK]] · #hidden_interlock |
| P8 | Cat-death finales are memorable — use at most once per level *set*, never in consecutive levels. | It's an emotional beat; repetition makes it a mechanic chore. | governs [[FINALE_SACRIFICE]] · #finale_overuse |
| P9 | Keep island count ≤4 on canvases under 20×12; more reads as visual noise and makes wiring illegible. | The map must be sketchable-at-a-glance — that's the whole point of ESN. | governs [[D9]] · #island_noise |
| P10 | Decoys must be *plausible*: a decoy altar in an unreachable corner fools nobody and fails [[V6]] in spirit. Wire decoys to real gates near the honest path. | A decoy is a bet the player will consider it; place it where they're already looking. | governs [[DECOY_ALTAR]] [[D10]] · #implausible_decoy |
| P11 | Keep pit straits narrow (1–2 cells) and use `#` wall for all non-puzzle-relevant filler space, not decorative open pit. Only cells that are an actual bridge candidate should be `~`. | The solver tracks bridge state as an exact cell-set; every incidental pit cell adjacent to a walkable area multiplies the reachable state space combinatorially. Wide-open "pit seas" that look atmospheric can make an otherwise-solvable level computationally intractable to certify. | evidence: [[level4_state_explosion]] [[level4_reconstruction]] · governs solver tractability · #solver_intractable |

## Verdict taxonomy for `level_records.md`

`rejected_unsolvable` · `rejected_invalid` (lint/V-failure) · `rejected_too_easy` ·
`rejected_too_hard` · `flawed_accepted` (shipped with a known wart) ·
`solver_inconclusive` (hit the state/cost cap — not proven either way) ·
`player_feedback_bad` (human played it, didn't enjoy) · `player_feedback_good` ·
`exemplar` (study this one)
