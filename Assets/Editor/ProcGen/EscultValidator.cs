using System;
using System.Collections.Generic;
using System.Text;

namespace Escult.ProcGen
{
    public class CheckResult
    {
        public string Id;       // V1..V9, LINT
        public string Status;   // PASS | FAIL | WARN | INFO
        public string Note;
    }

    /// <summary>Full pipeline report: V1–V9 checks + D1–D10 vectors + tier estimate.</summary>
    public class EscultReport
    {
        public string Name;
        public string RequestedTier;                     // may be null
        public string TierEstimate;
        public List<CheckResult> Checks = new List<CheckResult>();
        public Dictionary<string, int> Vectors = new Dictionary<string, int>();
        public EscultSolver.Result Solve;
        public List<LintMessage> Lints = new List<LintMessage>();
        public bool Valid
        {
            get
            {
                foreach (var c in Checks) if (c.Status == "FAIL") return false;
                return Solve != null && Solve.Solvable;
            }
        }
    }

    public static class EscultValidator
    {
        static readonly string[] Tiers = { "tutorial", "easy", "medium", "hard", "extreme" };

        public static EscultReport Run(Topology t, List<LintMessage> lints, string requestedTier = null)
        {
            var rep = new EscultReport { Name = t.Name, RequestedTier = requestedTier, Lints = lints ?? new List<LintMessage>() };

            var solver = new EscultSolver(t);
            var s = solver.Solve();
            rep.Solve = s;

            if (s.Error != null)
            {
                rep.Checks.Add(new CheckResult { Id = "V2", Status = "FAIL", Note = "solver error: " + s.Error });
                return rep;
            }

            // ---- V1: closed arena ----
            bool perimeterWalled = true;
            for (int c = 0; c < t.W && perimeterWalled; c++)
                perimeterWalled = t.Terrain[t.Idx(c, 0)] == 'W' && t.Terrain[t.Idx(c, t.H - 1)] == 'W';
            for (int r = 0; r < t.H && perimeterWalled; r++)
                perimeterWalled = t.Terrain[t.Idx(0, r)] == 'W' && t.Terrain[t.Idx(t.W - 1, r)] == 'W';
            if (perimeterWalled)
                rep.Checks.Add(new CheckResult { Id = "V1", Status = "PASS", Note = "bounding perimeter is all wall" });
            else if (s.RayEscape)
                rep.Checks.Add(new CheckResult { Id = "V1", Status = "FAIL", Note = "a reachable throw ray exits the canvas (hard-lock)" });
            else
                rep.Checks.Add(new CheckResult { Id = "V1", Status = "WARN", Note = "perimeter is not fully walled; no escaping ray was reachable, but the sufficient condition of V1 is unmet" });

            // ---- V2: initial solvability ----
            rep.Checks.Add(new CheckResult
            {
                Id = "V2",
                Status = s.Solvable ? "PASS" : "FAIL",
                Note = s.Solvable
                    ? $"solver proved a win path; min_cost={s.MinCost}, {s.SolutionCount}{(s.SolutionCountCapped ? "+" : "")} minimal solution(s)"
                    : $"no win path exists from the initial state ({s.ReachableStates} states explored)"
            });

            // ---- V3: soul feasibility ----
            if (s.Solvable)
                rep.Checks.Add(new CheckResult { Id = "V3", Status = "PASS", Note = $"min_cost {s.MinCost} ≤ budget {t.SoulBudget}; slack={t.SoulBudget - s.MinCost}" });
            else
                rep.Checks.Add(new CheckResult { Id = "V3", Status = "FAIL", Note = "no solution within the soul budget" });

            // ---- vectors (need solver output; computed before V4 which uses D1) ----
            ComputeVectors(t, s, rep);

            // ---- V4: non-triviality ----
            {
                bool trivial = s.Solvable && s.WitnessCostedActions == 0;
                if (requestedTier == null)
                    rep.Checks.Add(new CheckResult { Id = "V4", Status = trivial ? "WARN" : "INFO", Note = trivial ? "girl can reach the door with zero costed actions — tutorial-only pattern" : "no tier requested; V4 not enforced (estimate: " + rep.TierEstimate + ")" });
                else if (requestedTier == "tutorial")
                    rep.Checks.Add(new CheckResult { Id = "V4", Status = "PASS", Note = "tutorial tier: V4 skipped by rule" });
                else
                {
                    int floor = TierD1Floor(requestedTier);
                    bool ok = !trivial && rep.Vectors["D1"] >= floor;
                    rep.Checks.Add(new CheckResult { Id = "V4", Status = ok ? "PASS" : "FAIL", Note = $"minimal solution length {rep.Vectors["D1"]} vs tier '{requestedTier}' floor {floor}" + (trivial ? " (trivially walkable!)" : "") });
                }
            }

            // ---- V5: spawn sanity ----
            {
                var notes = new List<string>();
                foreach (var pair in new[] { new { name = "girl", cell = t.GirlSpawn }, new { name = "cat", cell = t.CatSpawn } })
                {
                    if (pair.cell < 0) continue;
                    if (t.Terrain[pair.cell] != 'G') notes.Add($"{pair.name} spawn not on ground");
                    if (t.GateAt[pair.cell] >= 0) notes.Add($"{pair.name} spawn under gate {t.Gates[t.GateAt[pair.cell]].Id}");
                }
                if (s.Error != null && s.Error.Contains("spawn")) notes.Add(s.Error);
                rep.Checks.Add(new CheckResult { Id = "V5", Status = notes.Count == 0 ? "PASS" : "FAIL", Note = notes.Count == 0 ? "spawns on ground, off gates, mobile" : string.Join("; ", notes) });
            }

            // ---- V6: element relevance (approximate: usage across all soul-minimal solutions) ----
            {
                var irrelevant = new List<string>();
                var notes = new List<string>();
                foreach (var a in t.Altars)
                    if (!s.UsedAltars.Contains(a.Id) && !t.Decoys.Contains(a.Id)) irrelevant.Add("altar " + a.Id);
                foreach (var g in t.Gates)
                {
                    if (s.UsedGates.Contains(g.Id) || t.Decoys.Contains(g.Id)) continue;
                    if (!g.InitialClosed) { notes.Add($"gate {g.Id} starts OPEN and is never toggled (decorative)"); continue; }
                    irrelevant.Add("gate " + g.Id);
                }
                foreach (var d in t.Doors)
                    if (!s.UsedDoors.Contains(d.Id) && !t.Decoys.Contains(d.Id)) irrelevant.Add("door " + d.Id);
                string status = !s.Solvable ? "INFO" : irrelevant.Count == 0 ? "PASS" : "FAIL";
                string note = irrelevant.Count == 0
                    ? "every element is used in some minimal solution or tagged decoy"
                    : "not in any minimal solution and not tagged decoy: " + string.Join(", ", irrelevant);
                if (notes.Count > 0) note += " | " + string.Join("; ", notes);
                rep.Checks.Add(new CheckResult { Id = "V6", Status = status, Note = note });
            }

            // V7/V8/V9: enforced by the solver's action model itself
            rep.Checks.Add(new CheckResult { Id = "V7", Status = "PASS", Note = "solver locks all cat actions at souls=0 (by construction)" });
            rep.Checks.Add(new CheckResult { Id = "V8", Status = "PASS", Note = "anti-crush interlock enforced inside successor generation (by construction)" });
            rep.Checks.Add(new CheckResult { Id = "V9", Status = "PASS", Note = "abstract model is deterministic by definition" });

            // lint roll-up
            foreach (var l in rep.Lints)
                rep.Checks.Add(new CheckResult { Id = "LINT", Status = l.IsError ? "FAIL" : "WARN", Note = l.Text });

            // requested-tier band check
            if (requestedTier != null && s.Solvable)
            {
                bool inBand = rep.TierEstimate == requestedTier;
                rep.Checks.Add(new CheckResult { Id = "TIER", Status = inBand ? "PASS" : "WARN", Note = $"requested '{requestedTier}', vectors estimate '{rep.TierEstimate}'" });
            }

            return rep;
        }

        static int TierD1Floor(string tier)
        {
            switch (tier)
            {
                case "easy": return 3;
                case "medium": return 5;
                case "hard": return 8;
                case "extreme": return 12;
                default: return 1;
            }
        }

        static void ComputeVectors(Topology t, EscultSolver.Result s, EscultReport rep)
        {
            var v = rep.Vectors;
            v["D1"] = s.Solvable ? s.WitnessLength : 0;
            v["D2"] = s.Solvable ? t.SoulBudget - s.MinCost : 0;
            v["D3"] = s.StructuralTrapEntries;
            v["D3_depth"] = s.MaxSoulsSunkInTrap;

            int edges = 0;
            var targetCount = new Dictionary<string, int>();
            foreach (var a in t.Altars)
                foreach (var tg in a.Targets)
                {
                    edges++;
                    int n; targetCount.TryGetValue(tg, out n); targetCount[tg] = n + 1;
                }
            int shared = 0;
            foreach (var kv in targetCount) if (kv.Value >= 2) shared++;
            v["D4"] = edges;
            v["D4_shared"] = shared;
            v["D5"] = s.Solvable ? s.WitnessMaxAltarFires : 0;
            v["D6"] = s.Solvable ? s.WitnessAlternations : 0;

            // D7: avg manhattan distance altar -> wired target (gates: nearest cell)
            {
                int sum = 0, cnt = 0;
                foreach (var a in t.Altars)
                {
                    foreach (var tg in a.Targets)
                    {
                        int best = int.MaxValue;
                        if (tg.StartsWith("d"))
                        {
                            int dc = t.Doors[t.DoorIndex(tg)].Cell;
                            best = Math.Abs(t.Col(dc) - t.Col(a.Cell)) + Math.Abs(t.Row(dc) - t.Row(a.Cell));
                        }
                        else
                        {
                            foreach (var cell in t.Gates[t.GateIndex(tg)].Cells)
                            {
                                int d = Math.Abs(t.Col(cell) - t.Col(a.Cell)) + Math.Abs(t.Row(cell) - t.Row(a.Cell));
                                if (d < best) best = d;
                            }
                        }
                        if (best < int.MaxValue) { sum += best; cnt++; }
                    }
                }
                v["D7"] = cnt == 0 ? 0 : (int)Math.Round((double)sum / cnt);
            }

            v["D8"] = s.Solvable ? s.WitnessThrows : 0;

            // D9: ground islands over pure terrain
            {
                var seen = new bool[t.N];
                int islands = 0;
                var stack = new Stack<int>();
                for (int i = 0; i < t.N; i++)
                {
                    if (t.Terrain[i] != 'G' || seen[i]) continue;
                    islands++;
                    seen[i] = true; stack.Push(i);
                    while (stack.Count > 0)
                        foreach (var n in t.Neighbors(stack.Pop()))
                            if (t.Terrain[n] == 'G' && !seen[n]) { seen[n] = true; stack.Push(n); }
                }
                v["D9"] = islands;
            }

            v["D10"] = t.Decoys.Count;

            rep.TierEstimate = EstimateTier(v);
        }

        /// <summary>Score each tier row of the Ruleset section 4 calibration table; most matching vector bands wins (ties go harder).</summary>
        static string EstimateTier(Dictionary<string, int> v)
        {
            int d1 = v["D1"], slack = v["D2"], traps = v["D3"], edges = v["D4"], shared = v["D4_shared"],
                parity = v["D5"], swaps = v["D6"], throws = v["D8"];

            var scores = new int[Tiers.Length];
            Action<int, bool> add = (tier, cond) => { if (cond) scores[tier]++; };

            // tutorial
            add(0, d1 >= 1 && d1 <= 3); add(0, slack >= 6); add(0, traps == 0);
            add(0, edges <= 1 && parity <= 1); add(0, swaps <= 1); add(0, throws <= 1);
            // easy
            add(1, d1 >= 3 && d1 <= 6); add(1, slack >= 4 && slack <= 6); add(1, traps == 0);
            add(1, edges <= 2 && parity <= 1); add(1, swaps <= 2); add(1, throws <= 1);
            // medium
            add(2, d1 >= 5 && d1 <= 9); add(2, slack >= 2 && slack <= 4); add(2, traps <= 1);
            add(2, edges <= 4 && parity <= 2); add(2, swaps >= 2 && swaps <= 4); add(2, throws >= 1 && throws <= 2);
            // hard
            add(3, d1 >= 8 && d1 <= 14); add(3, slack >= 1 && slack <= 2); add(3, traps >= 1 && traps <= 2);
            add(3, shared >= 1 && parity <= 3); add(3, swaps >= 4 && swaps <= 6); add(3, throws >= 2 && throws <= 3);
            // extreme
            add(4, d1 >= 12); add(4, slack <= 1); add(4, traps >= 2);
            add(4, shared >= 1 && parity >= 2); add(4, swaps >= 6); add(4, throws >= 3);

            int best = 0;
            for (int i = 0; i < scores.Length; i++)
                if (scores[i] >= scores[best]) best = i;    // >= : ties go to the harder tier
            return Tiers[best];
        }

        // ------------------------------------------------------------------
        public static string ToMarkdown(EscultReport rep, Topology t)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## {rep.Name} — {(rep.Valid ? "VALID" : "INVALID")}" +
                          (rep.Solve != null && rep.Solve.Solvable ? $" · min_cost {rep.Solve.MinCost}/{t.SoulBudget} · tier estimate **{rep.TierEstimate}**" : ""));
            sb.AppendLine();
            sb.AppendLine("| Check | Status | Note |");
            sb.AppendLine("|---|---|---|");
            foreach (var c in rep.Checks)
                sb.AppendLine($"| {c.Id} | {c.Status} | {c.Note} |");
            sb.AppendLine();
            if (rep.Vectors.Count > 0)
            {
                sb.AppendLine("| D1 len | D2 slack | D3 traps (depth) | D4 edges (shared) | D5 parity | D6 swaps | D7 wire-dist | D8 throws | D9 islands | D10 decoys |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
                var v = rep.Vectors;
                sb.AppendLine($"| {v["D1"]} | {v["D2"]} | {v["D3"]} ({v["D3_depth"]}) | {v["D4"]} ({v["D4_shared"]}) | {v["D5"]} | {v["D6"]} | {v["D7"]} | {v["D8"]} | {v["D9"]} | {v["D10"]} |");
                sb.AppendLine();
            }
            if (rep.Solve != null && rep.Solve.Solvable)
            {
                sb.AppendLine($"Witness solution ({rep.Solve.MinCost} souls, {rep.Solve.Witness.Count} steps; dead-end states reachable: {rep.Solve.DeadStates}):");
                int n = 1, souls = t.SoulBudget;
                foreach (var st in rep.Solve.Witness)
                {
                    sb.Append(n++).Append(". ").Append(FormatStep(st, t));
                    if (st.SoulsAfter >= 0) { souls = st.SoulsAfter; sb.Append($"   # souls {souls}"); }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        public static string FormatStep(EscultSolver.SolveStep st, Topology t)
        {
            string XY(int cell) { return cell < 0 ? "?" : $"({t.Col(cell)},{t.Row(cell)})"; }
            switch (st.Op)
            {
                case "MOVE": return $"MOVE {st.Actor} {XY(st.To)}";
                case "PICKUP": return "PICKUP";
                case "DROP": return "DROP";
                case "THROW": return $"THROW {st.Dir} from {XY(st.Cell)} lands {XY(st.Lands)}" + (st.SoulsAfter >= 0 ? " (pit! recall)" : "");
                case "SACRIFICE": return $"SACRIFICE {st.AltarId}";
                case "BRIDGE": return $"BRIDGE {XY(st.Cell)}";
                case "EXIT": return "EXIT";
                default: return st.Op;
            }
        }
    }
}
