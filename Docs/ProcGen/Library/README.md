# Escult Design Library — linked knowledge graph

An Obsidian-style, grep-retrievable RAG layer for the level generator. Nodes are
stable IDs; edges are `[[wikilinks]]`; retrieval is lexical search + 1-hop link
expansion. No embeddings, no index to rebuild — `grep` over Markdown IS the
retrieval engine, which makes it deterministic, zero-cost, and git-diffable.

## Node namespaces (all IDs already defined in the core docs)

| Link form | Node type | Defined in |
|---|---|---|
| `[[P1]]`…`[[P10]]` | Design principles (fun) | `design_principles.md` |
| `[[V1]]`…`[[V9]]` | Validity constraints | `01_Puzzle_Ruleset.md` section 3 |
| `[[D1]]`…`[[D10]]` | Difficulty vectors | `01_Puzzle_Ruleset.md` section 4 |
| `[[THROW_LANE]]`, `[[BAIT_BRIDGE]]`, … | Puzzle atoms | `02_Generation_Pipeline.md` section 1.2 |
| `[[<record-name>]]` | Level records (case log) | `level_records.md` |
| `#tag` | Fuzzy grouping across nodes | anywhere |

## Retrieval recipes (what the designer agent runs)

1. **Seed query** — for each atom/vector in the current design request:
   `grep -n "ATOM_NAME" Docs/ProcGen/Library/*.md`
2. **1-hop expansion** — collect every `[[X]]` inside the hits, grep each `X`
   once across the library. Stop at one hop unless a hit is directly on-point.
3. **Verdict slice** — before adding traps: `grep -n "verdict: player_feedback_bad" level_records.md`;
   when unsure about tier feel: grep `slack` mentions.
4. Read `design_principles.md` in full regardless (it is capped small on purpose).

## Writing rules (what keeps the graph useful)

- Every new record MUST link at least one `[[P*]]` principle or `[[ATOM]]` — an
  unlinked record is unreachable by retrieval and therefore worthless.
- New principles MUST link the records that motivated them (their evidence).
- Link liberally: a `[[target]]` that does not exist yet is not an error — it
  marks a node worth writing (create it when a second reference appears).
- IDs are permanent. Never renumber principles or rename records; deprecate with
  a note instead (links must never rot).
- Records: append-only. Principles: curated, ~80-line cap; distill ≥3 same-lesson
  records into one principle and link them as evidence.
