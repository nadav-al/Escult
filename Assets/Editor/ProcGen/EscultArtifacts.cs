using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Escult.ProcGen
{
    /// <summary>
    /// Emits the four per-level artifacts of Docs/ProcGen/02_Generation_Pipeline.md
    /// (esn.txt, level.json, solution.json, report.json) plus a rendered .svg.
    /// JSON is hand-built (no library dependency); schema matches doc 02 section 2.2/section 2.4.
    /// </summary>
    public static class EscultArtifacts
    {
        // ---------------- JSON helpers ----------------
        static string J(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
        static string Cell(Topology t, int i) { return $"[{t.Col(i)},{t.Row(i)}]"; }
        static string Cells(Topology t, IEnumerable<int> cells)
        {
            var parts = new List<string>();
            foreach (var c in cells) parts.Add(Cell(t, c));
            return "[" + string.Join(",", parts) + "]";
        }
        static string Strings(IEnumerable<string> items)
        {
            var parts = new List<string>();
            foreach (var s in items) parts.Add(J(s));
            return "[" + string.Join(",", parts) + "]";
        }

        // ---------------- level.json ----------------
        public static string LevelJson(Topology t, string tier)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"version\": 1,");
            sb.AppendLine($"  \"name\": {J(t.Name)},");
            sb.AppendLine($"  \"seed\": 0,");
            sb.AppendLine($"  \"tier\": {J(tier ?? "unrated")},");
            sb.AppendLine($"  \"bounds\": {{ \"w\": {t.W}, \"h\": {t.H} }},");

            var terrainRows = new List<string>();
            for (int r = 0; r < t.H; r++)
            {
                var row = new char[t.W];
                for (int c = 0; c < t.W; c++) row[c] = t.Terrain[t.Idx(c, r)];
                terrainRows.Add(J(new string(row)));
            }
            sb.AppendLine("  \"terrain\": [");
            sb.AppendLine("    " + string.Join(",\n    ", terrainRows));
            sb.AppendLine("  ],");

            var gates = new List<string>();
            foreach (var g in t.Gates)
                gates.Add($"{{ \"id\": {J(g.Id)}, \"cells\": {Cells(t, g.Cells)}, \"initialClosed\": {(g.InitialClosed ? "true" : "false")}, \"overPit\": {(g.OverPit ? "true" : "false")} }}");
            sb.AppendLine("  \"gates\":  [ " + string.Join(",\n              ", gates) + " ],");

            var altars = new List<string>();
            foreach (var a in t.Altars)
                altars.Add($"{{ \"id\": {J(a.Id)}, \"cell\": {Cell(t, a.Cell)}, \"targets\": {Strings(a.Targets)} }}");
            sb.AppendLine("  \"altars\": [ " + string.Join(",\n              ", altars) + " ],");

            var doors = new List<string>();
            foreach (var d in t.Doors)
                doors.Add($"{{ \"id\": {J(d.Id)}, \"cell\": {Cell(t, d.Cell)}, \"initialOpen\": {(d.InitialOpen ? "true" : "false")} }}");
            sb.AppendLine("  \"doors\":  [ " + string.Join(",\n              ", doors) + " ],");

            string cat = t.CatSpawn >= 0 ? Cell(t, t.CatSpawn) : "[-1,-1]";
            sb.AppendLine($"  \"spawns\": {{ \"girl\": {Cell(t, t.GirlSpawn)}, \"cat\": {cat}, \"catInLevel\": {(t.CatInLevel ? "true" : "false")} }},");
            sb.AppendLine($"  \"soulBudget\": {t.SoulBudget},");
            sb.AppendLine($"  \"decoys\": {Strings(t.Decoys)},");
            sb.AppendLine($"  \"esn\": {Strings(EsnParser.BuildCanvas(t))}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ---------------- solution.json ----------------
        public static string SolutionJson(Topology t, EscultSolver.Result s)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{ \"minCost\": " + (s.Solvable ? s.MinCost : -1) + ",");
            sb.AppendLine($"  \"solutionCount\": {s.SolutionCount}{(s.SolutionCountCapped ? " " : "")},");
            var steps = new List<string>();
            foreach (var st in s.Witness)
            {
                var f = new List<string> { $"\"op\": {J(st.Op)}" };
                if (st.Actor != null && st.Op != "PICKUP" && st.Op != "DROP" && st.Op != "EXIT") f.Add($"\"actor\": {J(st.Actor)}");
                if (st.To >= 0) f.Add($"\"to\": {Cell(t, st.To)}");
                if (st.AltarId != null) f.Add($"\"altar\": {J(st.AltarId)}");
                if (st.Op == "BRIDGE" && st.Cell >= 0) f.Add($"\"cell\": {Cell(t, st.Cell)}");
                if (st.Op == "THROW")
                {
                    if (st.Cell >= 0) f.Add($"\"from\": {Cell(t, st.Cell)}");
                    f.Add($"\"dir\": {J(st.Dir)}");
                    if (st.Lands >= 0) f.Add($"\"lands\": {Cell(t, st.Lands)}");
                }
                if (st.SoulsAfter >= 0) f.Add($"\"soulsAfter\": {st.SoulsAfter}");
                steps.Add("    { " + string.Join(", ", f) + " }");
            }
            sb.AppendLine("  \"steps\": [");
            sb.AppendLine(string.Join(",\n", steps));
            sb.AppendLine("  ] }");
            return sb.ToString();
        }

        // ---------------- report.json ----------------
        public static string ReportJson(EscultReport rep)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": {J(rep.Name)},");
            sb.AppendLine($"  \"valid\": {(rep.Valid ? "true" : "false")},");
            sb.AppendLine($"  \"tier\": {J(rep.TierEstimate ?? "unrated")},");
            if (rep.RequestedTier != null) sb.AppendLine($"  \"requestedTier\": {J(rep.RequestedTier)},");
            var vec = new List<string>();
            foreach (var kv in rep.Vectors) vec.Add($"\"{kv.Key}\": {kv.Value}");
            sb.AppendLine("  \"vectors\": { " + string.Join(", ", vec) + " },");
            var checks = new List<string>();
            foreach (var c in rep.Checks) checks.Add($"\"{c.Id}\": {J(c.Status + " — " + c.Note)}");
            sb.AppendLine("  \"checks\": { " + string.Join(",\n              ", checks) + " },");
            long solCount = rep.Solve != null ? rep.Solve.SolutionCount : 0;
            sb.AppendLine($"  \"solutionCount\": {solCount},");
            sb.AppendLine($"  \"reachableStates\": {(rep.Solve != null ? rep.Solve.ReachableStates : 0)},");
            sb.AppendLine($"  \"deadEndStates\": {(rep.Solve != null ? rep.Solve.DeadStates : 0)}");
            sb.AppendLine("}");
            return sb.ToString();
        }

        // ---------------- SVG renderer (deterministic glyph -> colored view) ----------------
        public static string Svg(Topology t)
        {
            const int CS = 32, PAD = 8;
            int legendLines = 1 + t.Altars.Count + t.Gates.Count + (t.Decoys.Count > 0 ? 1 : 0);
            int wpx = t.W * CS + PAD * 2;
            int hpx = t.H * CS + PAD * 3 + legendLines * 18;
            var ci = CultureInfo.InvariantCulture;

            var sb = new StringBuilder();
            sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{wpx}\" height=\"{hpx}\" viewBox=\"0 0 {wpx} {hpx}\" font-family=\"monospace\">");
            sb.AppendLine($"<rect width=\"{wpx}\" height=\"{hpx}\" fill=\"#1b1b22\"/>");

            string RectAt(int cell, string fill, string extra = "")
            {
                int x = PAD + t.Col(cell) * CS, y = PAD + t.Row(cell) * CS;
                return $"<rect x=\"{x}\" y=\"{y}\" width=\"{CS}\" height=\"{CS}\" fill=\"{fill}\" {extra}/>";
            }
            string TextAt(int cell, string txt, string fill)
            {
                int x = PAD + t.Col(cell) * CS + CS / 2, y = PAD + t.Row(cell) * CS + CS / 2 + 5;
                return $"<text x=\"{x}\" y=\"{y}\" fill=\"{fill}\" font-size=\"16\" text-anchor=\"middle\" font-weight=\"bold\">{txt}</text>";
            }

            for (int i = 0; i < t.N; i++)
            {
                char ter = t.Terrain[i];
                string fill = ter == 'W' ? "#3d3d49" : ter == 'P' ? "#5c1420" : "#c9bfae";
                sb.AppendLine(RectAt(i, fill, "stroke=\"#1b1b22\" stroke-width=\"1\""));
            }
            foreach (var g in t.Gates)
                foreach (var c in g.Cells)
                {
                    sb.AppendLine(RectAt(c, g.InitialClosed ? "#6b4fa0" : "#a98fd6", "stroke=\"#2c2140\" stroke-width=\"2\" fill-opacity=\"0.85\""));
                    sb.AppendLine(TextAt(c, g.Id, "#f2ecff"));
                }
            foreach (var a in t.Altars)
            {
                sb.AppendLine(RectAt(a.Cell, "#d9a441", "stroke=\"#6e5012\" stroke-width=\"2\""));
                sb.AppendLine(TextAt(a.Cell, a.Id, "#40300a"));
            }
            foreach (var d in t.Doors)
            {
                sb.AppendLine(RectAt(d.Cell, d.InitialOpen ? "#7fa66b" : "#8a5a3c", "stroke=\"#3d2a1c\" stroke-width=\"2\""));
                sb.AppendLine(TextAt(d.Cell, d.InitialOpen ? "O" : "X", "#fff6e8"));
            }
            if (t.GirlSpawn >= 0)
            {
                int x = PAD + t.Col(t.GirlSpawn) * CS + CS / 2, y = PAD + t.Row(t.GirlSpawn) * CS + CS / 2;
                sb.AppendLine($"<circle cx=\"{x}\" cy=\"{y}\" r=\"11\" fill=\"#e07a9a\" stroke=\"#5c2237\" stroke-width=\"2\"/>");
                sb.AppendLine(TextAt(t.GirlSpawn, "@", "#ffffff"));
            }
            if (t.CatSpawn >= 0)
            {
                int x = PAD + t.Col(t.CatSpawn) * CS + CS / 2, y = PAD + t.Row(t.CatSpawn) * CS + CS / 2;
                sb.AppendLine($"<circle cx=\"{x}\" cy=\"{y}\" r=\"11\" fill=\"#8d99ae\" stroke=\"#2b3040\" stroke-width=\"2\"/>");
                sb.AppendLine(TextAt(t.CatSpawn, "C", "#ffffff"));
            }

            int ty = PAD * 2 + t.H * CS + 12;
            void Line(string s2) { sb.AppendLine($"<text x=\"{PAD}\" y=\"{ty}\" fill=\"#cfc6b8\" font-size=\"13\">{s2}</text>"); ty += 18; }
            Line($"{t.Name} · souls: {t.SoulBudget}" + (t.CatInLevel ? "" : " · girl-only"));
            foreach (var a in t.Altars) Line($"{a.Id} -&gt; {string.Join(", ", a.Targets)}");
            foreach (var g in t.Gates) Line($"{g.Id}: initial={(g.InitialClosed ? "CLOSED" : "OPEN")}{(g.OverPit ? " over=PIT" : "")}");
            if (t.Decoys.Count > 0) Line("decoys: " + string.Join(", ", t.Decoys));

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        // ---------------- emission ----------------
        /// <summary>Write all artifacts next to the level's esn file. Returns the folder.</summary>
        public static string Emit(string folder, string esnText, Topology t, EscultReport rep)
        {
            Directory.CreateDirectory(folder);
            string baseName = Path.Combine(folder, t.Name);
            File.WriteAllText(baseName + ".esn.txt", esnText, new UTF8Encoding(false));
            File.WriteAllText(baseName + ".level.json", LevelJson(t, rep.TierEstimate), new UTF8Encoding(false));
            if (rep.Solve != null && rep.Solve.Solvable)
                File.WriteAllText(baseName + ".solution.json", SolutionJson(t, rep.Solve), new UTF8Encoding(false));
            File.WriteAllText(baseName + ".report.json", ReportJson(rep), new UTF8Encoding(false));
            File.WriteAllText(baseName + ".svg", Svg(t), new UTF8Encoding(false));
            return folder;
        }
    }
}
