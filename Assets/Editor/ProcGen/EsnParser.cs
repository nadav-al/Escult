using System;
using System.Collections.Generic;
using System.Text;

namespace Escult.ProcGen
{
    /// <summary>
    /// ESN (Escult Sketch Notation) parser + linter + serializer.
    /// Spec: Docs/ProcGen/01_Puzzle_Ruleset.md section 5.4. Lossless ESN ⇄ Topology.
    /// </summary>
    public static class EsnParser
    {
        const string CanvasGlyphs = "#.~@C=123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        static bool IsCanvasLine(string line)
        {
            if (line.Length < 2) return false;
            foreach (char ch in line)
                if (CanvasGlyphs.IndexOf(ch) < 0) return false;
            return true;
        }

        public static ParseResult Parse(string text, string name = "level")
        {
            var res = new ParseResult();
            var canvas = new List<string>();
            var legend = new List<string>();

            // Split into canvas block (leading run of glyph-only lines) and legend (the rest).
            bool inCanvas = true, canvasSeen = false;
            foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = raw.TrimEnd();
                if (inCanvas)
                {
                    if (IsCanvasLine(line)) { canvas.Add(line); canvasSeen = true; continue; }
                    if (line.Length == 0 && !canvasSeen) continue;   // leading blanks
                    inCanvas = false;
                }
                legend.Add(line);
            }

            if (canvas.Count == 0) { res.Error("no canvas found (expected leading block of #.~ glyph rows)"); return res; }

            int h = canvas.Count, w = canvas[0].Length;
            for (int r = 0; r < h; r++)
                if (canvas[r].Length != w)
                    res.Error($"ragged canvas: row {r} has width {canvas[r].Length}, expected {w}");
            if (res.HasErrors) return res;

            var t = new Topology { Name = name, W = w, H = h, CanvasRows = canvas.ToArray() };
            t.Terrain = new char[w * h];

            var gateCells = new Dictionary<char, List<int>>();
            var doorGlyphs = new List<KeyValuePair<int, bool>>();  // cell, initialOpen — canvas reading order
            var altarCells = new Dictionary<char, int>();

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    char ch = canvas[r][c];
                    int i = t.Idx(c, r);
                    switch (ch)
                    {
                        case '#': t.Terrain[i] = 'W'; break;
                        case '.': t.Terrain[i] = 'G'; break;
                        case '~': t.Terrain[i] = 'P'; break;
                        case '=':
                            t.Terrain[i] = 'G';
                            res.Warn($"bridge glyph '=' at ({c},{r}) treated as ground (state-sketch glyph in a topology)");
                            break;
                        case '@':
                            t.Terrain[i] = 'G';
                            if (t.GirlSpawn >= 0) res.Error("multiple '@' girl spawns");
                            t.GirlSpawn = i;
                            break;
                        case 'C':
                            t.Terrain[i] = 'G';
                            if (t.CatSpawn >= 0) res.Error("multiple 'C' cat spawns");
                            t.CatSpawn = i;
                            break;
                        case 'X':
                            t.Terrain[i] = 'G';
                            doorGlyphs.Add(new KeyValuePair<int, bool>(i, false));
                            res.Warn($"closed-door glyph 'X' at ({c},{r}) is retired (ruleset v1.1: doors are always open) — use 'O' and lock the exit with a gate in front");
                            break;
                        case 'O':
                            t.Terrain[i] = 'G';
                            doorGlyphs.Add(new KeyValuePair<int, bool>(i, true));
                            break;
                        default:
                            if (ch >= '1' && ch <= '9')
                            {
                                t.Terrain[i] = 'G';
                                if (altarCells.ContainsKey(ch)) res.Error($"altar '{ch}' appears on more than one cell");
                                else altarCells[ch] = i;
                            }
                            else // A–Z gate letter (C/O/X already consumed above)
                            {
                                t.Terrain[i] = 'G'; // corrected later by over=PIT
                                if (!gateCells.TryGetValue(ch, out var list)) { list = new List<int>(); gateCells[ch] = list; }
                                list.Add(i);
                            }
                            break;
                    }
                }
            }

            if (t.GirlSpawn < 0) res.Error("no '@' girl spawn on canvas");
            if (doorGlyphs.Count == 0) res.Error("no door ('X'/'O') on canvas — level cannot be won");

            foreach (var kv in gateCells)
                t.Gates.Add(new Gate { Id = kv.Key.ToString(), Cells = kv.Value });
            t.Gates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            foreach (var kv in altarCells)
                t.Altars.Add(new Altar { Id = kv.Key.ToString(), Cell = kv.Value });
            t.Altars.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int d = 0; d < doorGlyphs.Count; d++)
                t.Doors.Add(new Door { Id = "d" + (d + 1), Cell = doorGlyphs[d].Key, InitialOpen = doorGlyphs[d].Value });

            ParseLegend(legend, t, res);

            // over=PIT gates: fix terrain beneath their cells
            foreach (var g in t.Gates)
                if (g.OverPit)
                    foreach (var c in g.Cells) t.Terrain[c] = 'P';

            StaticLint(t, res);
            if (res.HasErrors) return res;

            t.BuildLookups();
            res.Topology = t;
            return res;
        }

        /// <summary>Resolve a legend target/decoy token to a canonical element id, or null.</summary>
        static string ResolveRef(string token, Topology t, ParseResult res, string context)
        {
            token = token.Trim();
            if (token.Length == 0) return null;
            if (token.Length == 1 && token[0] >= '1' && token[0] <= '9')
            {
                foreach (var a in t.Altars) if (a.Id == token) return token;
                res.Error($"{context}: altar '{token}' not on canvas");
                return null;
            }
            if (token == "X" || token == "O")
            {
                if (t.Doors.Count == 1) return "d1";
                res.Error($"{context}: '{token}' door shorthand is only legal with exactly one door (found {t.Doors.Count}); use d1..d{t.Doors.Count}");
                return null;
            }
            if (token.Length >= 2 && (token[0] == 'd' || token[0] == 'D') && char.IsDigit(token[1]))
            {
                string id = "d" + token.Substring(1);
                if (t.DoorIndex(id) >= 0) return id;
                res.Error($"{context}: door '{token}' does not exist");
                return null;
            }
            if (token.Length == 1 && token[0] >= 'A' && token[0] <= 'Z')
            {
                if (t.GateIndex(token) >= 0) return token;
                res.Error($"{context}: gate '{token}' not on canvas");
                return null;
            }
            res.Error($"{context}: unrecognized reference '{token}'");
            return null;
        }

        static void ParseLegend(List<string> lines, Topology t, ParseResult res)
        {
            foreach (var raw in lines)
            {
                string line = raw;
                int hash = line.IndexOf('#');
                if (hash >= 0) line = line.Substring(0, hash);
                line = line.Trim();
                if (line.Length == 0) continue;

                int arrow = line.IndexOf("->", StringComparison.Ordinal);
                if (arrow > 0)
                {
                    string left = line.Substring(0, arrow).Trim();
                    string right = line.Substring(arrow + 2).Trim();
                    Altar altar = null;
                    foreach (var a in t.Altars) if (a.Id == left) altar = a;
                    if (altar == null) { res.Error($"wiring '{line}': altar '{left}' not on canvas"); continue; }
                    foreach (var tok in right.Split(','))
                    {
                        string id = ResolveRef(tok, t, res, $"wiring '{line}'");
                        if (id != null)
                        {
                            if (altar.Targets.Contains(id)) res.Warn($"wiring '{line}': duplicate target '{id}'");
                            else altar.Targets.Add(id);
                        }
                    }
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0) { res.Warn($"unrecognized legend line: '{raw.Trim()}'"); continue; }
                string key = line.Substring(0, colon).Trim();
                string val = line.Substring(colon + 1).Trim();

                if (key == "souls")
                {
                    int budget;
                    if (int.TryParse(val, out budget) && budget >= 0) t.SoulBudget = budget;
                    else res.Error($"souls: expected a non-negative integer, got '{val}'");
                }
                else if (key == "decoys")
                {
                    foreach (var tok in val.Split(','))
                    {
                        string id = ResolveRef(tok, t, res, "decoys");
                        if (id != null) t.Decoys.Add(id);
                    }
                }
                else if (key == "name")
                {
                    t.Name = val;
                }
                else if (key.Length == 1 && key[0] >= 'A' && key[0] <= 'Z' && key != "C" && key != "O" && key != "X")
                {
                    int gi = t.GateIndex(key);
                    if (gi < 0) { res.Error($"legend '{key}:': gate '{key}' not on canvas"); continue; }
                    ParseAttributes(val, res, key, (k, v) =>
                    {
                        if (k == "initial")
                        {
                            if (v == "CLOSED") t.Gates[gi].InitialClosed = true;
                            else if (v == "OPEN") t.Gates[gi].InitialClosed = false;
                            else res.Error($"gate {key}: initial must be OPEN or CLOSED, got '{v}'");
                        }
                        else if (k == "over")
                        {
                            if (v == "PIT") t.Gates[gi].OverPit = true;
                            else if (v == "GROUND") t.Gates[gi].OverPit = false;
                            else res.Error($"gate {key}: over must be PIT or GROUND, got '{v}'");
                        }
                        else res.Warn($"gate {key}: unknown attribute '{k}'");
                    });
                }
                else if (key == "X" || key == "O" || (key.Length >= 2 && key[0] == 'd' && char.IsDigit(key[1])))
                {
                    string id = ResolveRef(key, t, res, $"legend '{key}:'");
                    if (id == null) continue;
                    int di = t.DoorIndex(id);
                    ParseAttributes(val, res, key, (k, v) =>
                    {
                        if (k == "initial")
                        {
                            bool open;
                            if (v == "OPEN") open = true;
                            else if (v == "CLOSED") open = false;
                            else { res.Error($"door {id}: initial must be OPEN or CLOSED, got '{v}'"); return; }
                            if (open != t.Doors[di].InitialOpen)
                                res.Error($"door {id}: legend says initial={v} but canvas glyph says {(t.Doors[di].InitialOpen ? "OPEN ('O')" : "CLOSED ('X')")} — they must agree");
                        }
                        else res.Warn($"door {id}: unknown attribute '{k}'");
                    });
                }
                else
                {
                    res.Warn($"unrecognized legend line: '{raw.Trim()}'");
                }
            }
        }

        static void ParseAttributes(string val, ParseResult res, string owner, Action<string, string> onAttr)
        {
            foreach (var part in val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) { res.Warn($"'{owner}:': expected key=value, got '{part}'"); continue; }
                onAttr(part.Substring(0, eq).Trim(), part.Substring(eq + 1).Trim());
            }
        }

        /// <summary>Static (non-solver) lints, incl. the ESN lint of doc 02 section 3.4 item 10.</summary>
        static void StaticLint(Topology t, ParseResult res)
        {
            foreach (var a in t.Altars)
                if (a.Targets.Count == 0 && !t.Decoys.Contains(a.Id))
                    res.Warn($"altar {a.Id} has no wiring ('{a.Id} -> ...') and is not a decoy");

            foreach (var g in t.Gates)
            {
                // gate cell contiguity is not required by the ruleset, but split gates are usually typos
                if (g.Cells.Count > 1)
                {
                    var seen = new HashSet<int> { g.Cells[0] };
                    var stack = new Stack<int>(); stack.Push(g.Cells[0]);
                    var setAll = new HashSet<int>(g.Cells);
                    while (stack.Count > 0)
                        foreach (var n in t.Neighbors(stack.Pop()))
                            if (setAll.Contains(n) && seen.Add(n)) stack.Push(n);
                    if (seen.Count != g.Cells.Count)
                        res.Warn($"gate {g.Id}: its {g.Cells.Count} cells are not contiguous (same letter = same gate — intended?)");
                }
            }

            if (t.CatSpawn < 0 && (t.Altars.Count > 0 || t.Gates.Count > 0))
                res.Warn("girl-only level (no 'C') with altars/gates — nothing can trigger them");
        }

        /// <summary>Serialize a Topology back to canonical ESN text (canvas + legend).</summary>
        public static string ToEsn(Topology t)
        {
            var sb = new StringBuilder();
            foreach (var row in BuildCanvas(t)) sb.AppendLine(row);
            sb.AppendLine();
            sb.AppendLine("souls: " + t.SoulBudget);
            foreach (var a in t.Altars)
                if (a.Targets.Count > 0)
                    sb.AppendLine(a.Id + " -> " + string.Join(", ", a.Targets));
            foreach (var g in t.Gates)
                sb.AppendLine($"{g.Id}: initial={(g.InitialClosed ? "CLOSED" : "OPEN")}" + (g.OverPit ? " over=PIT" : ""));
            foreach (var d in t.Doors)
                sb.AppendLine($"{d.Id}: initial={(d.InitialOpen ? "OPEN" : "CLOSED")}");
            if (t.Decoys.Count > 0) sb.AppendLine("decoys: " + string.Join(", ", t.Decoys));
            return sb.ToString();
        }

        /// <summary>Rebuild the glyph canvas from topology data (used when CanvasRows is absent).</summary>
        public static string[] BuildCanvas(Topology t)
        {
            if (t.CanvasRows != null && t.CanvasRows.Length == t.H) return t.CanvasRows;
            var rows = new char[t.H][];
            for (int r = 0; r < t.H; r++)
            {
                rows[r] = new char[t.W];
                for (int c = 0; c < t.W; c++)
                {
                    char ch = t.Terrain[t.Idx(c, r)];
                    rows[r][c] = ch == 'W' ? '#' : ch == 'P' ? '~' : '.';
                }
            }
            foreach (var g in t.Gates) foreach (var c in g.Cells) rows[t.Row(c)][t.Col(c)] = g.Id[0];
            foreach (var a in t.Altars) rows[t.Row(a.Cell)][t.Col(a.Cell)] = a.Id[0];
            foreach (var d in t.Doors) rows[t.Row(d.Cell)][t.Col(d.Cell)] = d.InitialOpen ? 'O' : 'X';
            if (t.GirlSpawn >= 0) rows[t.Row(t.GirlSpawn)][t.Col(t.GirlSpawn)] = '@';
            if (t.CatSpawn >= 0) rows[t.Row(t.CatSpawn)][t.Col(t.CatSpawn)] = 'C';
            var result = new string[t.H];
            for (int r = 0; r < t.H; r++) result[r] = new string(rows[r]);
            return result;
        }
    }
}
