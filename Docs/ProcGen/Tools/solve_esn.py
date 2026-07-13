#!/usr/bin/env python3
"""
Reference solver for Escult Sketch Notation (ESN) levels.

Implements Docs/ProcGen/01_Puzzle_Ruleset.md section 2 (actions) over
section 5.2 (state), and Docs/ProcGen/02_Generation_Pipeline.md section 1.3
(reference solver requirements). Behavior-compatible port of the original
PowerShell implementation; solve_esn.ps1 is now a thin wrapper around this file.

Ruleset v1.1: doors are always open — 'X' (closed door) is a retired glyph
(parsed as a door, but linted) and altar->door wiring is rejected with a lint.

Input:  an .esn.txt file - canvas block, ONE blank line, then legend block.
Output: a single JSON object on stdout (schema identical to the old solver).

Usage:
  python solve_esn.py --esn-path <file> [--max-cost 30] [--max-states 300000]
"""
import argparse
import json
import os
import re
import sys
from collections import deque

RESERVED = ('C', 'O', 'X')
DIRVECS = {'N': (0, -1), 'S': (0, 1), 'E': (1, 0), 'W': (-1, 0)}


def fail(msg):
    print(json.dumps({'ok': False, 'error': msg}, indent=2))
    sys.exit(1)


def main():
    ap = argparse.ArgumentParser(description='Escult ESN reference solver')
    ap.add_argument('--esn-path', required=True)
    ap.add_argument('--max-cost', type=int, default=30)
    ap.add_argument('--max-states', type=int, default=300000)
    args = ap.parse_args()

    if not os.path.isfile(args.esn_path):
        fail(f'file not found: {args.esn_path}')
    with open(args.esn_path, encoding='utf-8-sig') as f:
        raw = f.read().splitlines()

    blank = next((i for i, l in enumerate(raw) if l.strip() == ''), None)
    if blank is None:
        fail('esn file must have a blank line separating canvas from legend')
    canvas = [l for l in raw[:blank] if l.strip()]
    legend = raw[blank + 1:]

    if not canvas:
        fail('empty canvas')
    W = len(canvas[0])
    H = len(canvas)
    for l in canvas:
        if len(l) != W:
            fail(f"canvas rows must be equal width (expected {W}, got {len(l)} on '{l}')")

    lint = []

    # ---- parse canvas ----
    terrain = {}
    gates = {}    # id -> [cells]
    altars = {}   # id -> cell
    doors = []    # {id, cell, openInitial}
    girl0 = cat0 = None
    for y, row in enumerate(canvas):
        for x, ch in enumerate(row):
            c = f'{x},{y}'
            if ch == '#':
                terrain[c] = 'W'
            elif ch == '~':
                terrain[c] = 'P'
            elif ch == '.':
                terrain[c] = 'G'
            elif ch == '@':
                terrain[c] = 'G'
                if girl0:
                    lint.append("multiple '@' girl spawns")
                girl0 = c
            elif ch == 'C':
                terrain[c] = 'G'
                if cat0:
                    lint.append("multiple 'C' cat spawns")
                cat0 = c
            elif ch == 'X':
                terrain[c] = 'G'
                doors.append({'id': f'd{len(doors)+1}', 'cell': c, 'openInitial': True})
                lint.append("'X' (closed door) is retired in ruleset v1.1 — doors are always open; draw the door as 'O'")
            elif ch == 'O':
                terrain[c] = 'G'
                doors.append({'id': f'd{len(doors)+1}', 'cell': c, 'openInitial': True})
            elif ch.isdigit():
                terrain[c] = 'G'
                if ch in altars:
                    lint.append(f"duplicate altar id '{ch}'")
                altars[ch] = c
            elif ch.isupper() and ch.isalpha() and ch not in RESERVED:
                terrain[c] = 'G'  # default; over=PIT legend line can flip this below
                gates.setdefault(ch, []).append(c)
            else:
                lint.append(f"unrecognized glyph '{ch}' at ({x},{y})")

    if not girl0:
        fail("no '@' girl spawn found")
    GATE_IDS = sorted(gates.keys())
    DOOR_IDS = [d['id'] for d in doors]
    door_cell_of = {d['id']: d['cell'] for d in doors}
    door_open0 = {d['id']: d['openInitial'] for d in doors}

    # V1 lint: perimeter must be all '#'
    for x in range(W):
        if terrain.get(f'{x},0') != 'W':
            lint.append(f'V1: top border not wall at col {x}')
        if terrain.get(f'{x},{H-1}') != 'W':
            lint.append(f'V1: bottom border not wall at col {x}')
    for y in range(H):
        if terrain.get(f'0,{y}') != 'W':
            lint.append(f'V1: left border not wall at row {y}')
        if terrain.get(f'{W-1},{y}') != 'W':
            lint.append(f'V1: right border not wall at row {y}')

    # ---- parse legend ----
    # Ruleset v1.1: wiring targets are gates only; doors are always open.
    def resolve_target(t):
        if t in gates:
            return t
        return None

    def is_door_ref(t):
        return t in DOOR_IDS or t in ('X', 'O')

    souls0 = 9
    wiring = {}
    gate_initial = {g: True for g in GATE_IDS}   # default CLOSED
    gate_over_pit = {g: False for g in GATE_IDS}  # default over GROUND
    decoy_ids = []
    for line in legend:
        l = line.split('#', 1)[0].strip()
        if not l:
            continue
        m = re.match(r'^souls\s*:\s*(\d+)$', l)
        if m:
            souls0 = int(m.group(1))
            continue
        m = re.match(r'^decoys\s*:\s*(.+)$', l)
        if m:
            decoy_ids = [t.strip() for t in m.group(1).split(',')]
            continue
        m = re.match(r'^(\d)\s*->\s*(.+)$', l)
        if m:
            aid = m.group(1)
            raw_targets = [t.strip() for t in m.group(2).split(',')]
            if aid not in altars:
                lint.append(f"wiring references undefined altar '{aid}'")
            resolved = []
            for t in raw_targets:
                r = resolve_target(t)
                if r is not None:
                    resolved.append(r)
                elif is_door_ref(t):
                    lint.append(f"altar {aid} wired to door '{t}' — doors are always open and cannot be wired (ruleset v1.1)")
                else:
                    lint.append(f"altar {aid} targets undefined element '{t}'")
            wiring.setdefault(aid, []).extend(resolved)
            continue
        m = re.match(r'^([A-Za-z0-9]+)\s*:\s*(.+)$', l)
        if m:
            gid, rest = m.group(1), m.group(2)
            if gid in ('X', 'O') or gid in DOOR_IDS:
                lint.append(f"door config line '{gid}: ...' is retired — doors are always open (ruleset v1.1)")
                continue
            if gid not in gates:
                lint.append(f"legend configures undefined gate '{gid}'")
                continue
            if re.search(r'initial\s*=\s*OPEN', rest):
                gate_initial[gid] = False
            elif re.search(r'initial\s*=\s*CLOSED', rest):
                gate_initial[gid] = True
            if re.search(r'over\s*=\s*PIT', rest):
                gate_over_pit[gid] = True
                for cell in gates[gid]:
                    terrain[cell] = 'P'
            continue
        lint.append(f"unparsed legend line: '{l}'")

    for a in altars:
        if a not in wiring:
            lint.append(f"altar '{a}' has no wiring (fires with no effect)")
    for i in decoy_ids:
        if i not in altars and i not in gates and i not in DOOR_IDS:
            lint.append(f"decoy id '{i}' does not match any altar/gate/door")

    altar_cells = set(altars.values())
    door_cells = set(door_cell_of.values())
    has_cat = cat0 is not None
    gate_index = {g: i for i, g in enumerate(GATE_IDS)}
    door_index = {d: i for i, d in enumerate(DOOR_IDS)}
    altar_ids_sorted = sorted(altars.keys())

    def adj(c):
        x, y = map(int, c.split(','))
        return (f'{x+1},{y}', f'{x-1},{y}', f'{x},{y+1}', f'{x},{y-1}')

    def closed_gate_cells(gb):
        s = set()
        for i, g in enumerate(GATE_IDS):
            if not (gb >> i) & 1:
                s.update(gates[g])
        return s

    def walkable(c, cgc, br):
        t = terrain.get(c)
        if t is None or t == 'W':
            return False
        if c in altar_cells or c in door_cells:
            return False
        if c in cgc:
            return False
        return t == 'G' or c in br

    def throw_land(src, d, cgc):
        vx, vy = DIRVECS[d]
        x, y = map(int, src.split(','))
        prev = src
        while True:
            x += vx
            y += vy
            c = f'{x},{y}'
            if c not in terrain:
                return None  # off-grid: never terminates (V1 violation)
            if terrain[c] == 'W' or c in cgc or c in door_cells:
                if prev in altar_cells:
                    return None  # illegal landing on an altar
                return prev
            prev = c

    def won(girl, db):
        an = adj(girl)
        for i, d in enumerate(DOOR_IDS):
            if (db >> i) & 1 and door_cell_of[d] in an:
                return True
        return False

    # state = (girl, cat, souls, gb, db, br) ; br = sorted tuple of bridge cells
    def successors(st):
        girl, cat, souls, gb, db, br = st
        cgc = closed_gate_cells(gb)
        brs = set(br)
        out = []
        for n in adj(girl):
            if walkable(n, cgc, brs):
                out.append((0, f'MOVE_GIRL:{n}', (n, cat, souls, gb, db, br)))
        if has_cat and cat != 'HELD' and cat != 'DEAD':
            for n in adj(cat):
                if walkable(n, cgc, brs):
                    out.append((0, f'MOVE_CAT:{n}', (girl, n, souls, gb, db, br)))
            if cat == girl or cat in adj(girl):
                out.append((0, 'PICKUP', (girl, 'HELD', souls, gb, db, br)))
            if souls >= 1:
                for a in altar_ids_sorted:
                    if cat in adj(altars[a]):
                        targets = wiring.get(a, [])
                        crush = any(t in gates and girl in gates[t] for t in targets)
                        if not crush:
                            ngb, ndb = gb, db
                            for t in targets:
                                if t in gates:
                                    ngb ^= 1 << gate_index[t]
                                else:
                                    ndb ^= 1 << door_index[t]
                            nc = 'DEAD' if souls == 1 else cat
                            out.append((1, f'SACRIFICE_{a}', (girl, nc, souls - 1, ngb, ndb, br)))
                for p in adj(cat):
                    if terrain.get(p) == 'P' and p not in brs and p not in cgc:
                        nb = tuple(sorted(br + (p,)))
                        nc = 'DEAD' if souls == 1 else cat
                        out.append((1, f'BRIDGE_{p}', (girl, nc, souls - 1, gb, db, nb)))
        if has_cat and cat == 'HELD':
            out.append((0, 'DROP', (girl, girl, souls, gb, db, br)))
            for d in DIRVECS:
                land = throw_land(girl, d, cgc)
                if land is None:
                    continue
                if walkable(land, cgc, brs):
                    out.append((0, f'THROW_{d}', (girl, land, souls, gb, db, br)))
                elif terrain[land] == 'P' and souls >= 1:
                    nc = 'DEAD' if souls == 1 else girl
                    out.append((1, f'THROW_{d}(pit)', (girl, nc, souls - 1, gb, db, br)))
        return out

    def solve(start, max_cost, max_states):
        buckets = [deque() for _ in range(max_cost + 1)]
        paths = {start: []}
        best = {}
        visited = 0
        buckets[0].append(start)
        for cost in range(max_cost + 1):
            q = buckets[cost]
            while q:
                st = q.popleft()
                visited += 1
                if visited > max_states:
                    return {'ok': False,
                            'error': f'state space exceeded {max_states} - level likely malformed '
                                     '(e.g. unterminated throw) or too large for exhaustive search'}
                girl, cat, souls, gb, db, br = st
                if won(girl, db):
                    return {'ok': True, 'cost': cost, 'path': paths[st],
                            'finalState': st, 'statesVisited': visited}
                key = (girl, cat, gb, db, br)
                if key in best and best[key] <= cost:
                    continue
                best[key] = cost
                for c, name, ns in successors(st):
                    ngirl, ncat, _, ngb, ndb, nbr = ns
                    nkey = (ngirl, ncat, ngb, ndb, nbr)
                    if nkey in best and best[nkey] <= cost + c:
                        continue
                    if c > 0 or ns not in paths:
                        paths[ns] = paths[st] + [name]
                    buckets[cost + c].append(ns)
        return {'ok': False, 'error': f'no solution found within soul/cost budget {max_cost}'}

    db0 = 0
    for i, d in enumerate(DOOR_IDS):
        if door_open0[d]:
            db0 |= 1 << i
    cat_start = cat0 if has_cat else 'NONE'
    start = (girl0, cat_start, souls0, 0, db0, ())

    # spawn sanity (V5): at least one legal move each from the initial state
    init_cgc = closed_gate_cells(0)
    if not any(walkable(n, init_cgc, set()) for n in adj(girl0)):
        lint.append('V5: girl spawn has no legal initial move')
    if has_cat and not any(walkable(n, init_cgc, set()) for n in adj(cat0)):
        lint.append('V5: cat spawn has no legal initial move')

    result = solve(start, args.max_cost, args.max_states)

    out = {
        'ok': True,
        'file': os.path.abspath(args.esn_path),
        'bounds': {'w': W, 'h': H},
        'soulBudget': souls0,
        'gates': GATE_IDS,
        'altars': altar_ids_sorted,
        'doors': DOOR_IDS,
        'decoys': decoy_ids,
        'lint': lint,
        'solvable': result['ok'],
    }
    if result['ok']:
        out['minCost'] = result['cost']
        out['slack'] = souls0 - result['cost']
        out['witness'] = result['path']
        out['statesVisited'] = result['statesVisited']
        used = []
        for step in result['path']:
            m = re.match(r'^SACRIFICE_(.+)$', step)
            if m and m.group(1) not in used:
                used.append(m.group(1))
        out['altarsUsedInWitness'] = used
        out['unusedNonDecoyAltars'] = [a for a in altars
                                       if a not in decoy_ids and a not in used]
    else:
        out['error'] = result['error']
    print(json.dumps(out, indent=2))


if __name__ == '__main__':
    main()
