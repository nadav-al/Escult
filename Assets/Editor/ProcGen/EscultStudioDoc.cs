using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Escult.ProcGen
{
    /// <summary>
    /// The Studio's editable level document: a glyph canvas plus structured legend data.
    /// Regenerates canonical ESN text after every edit; parsing that text (EsnParser) is
    /// the single source of truth for lint/solve/preview, so canvas and text can never drift.
    /// Legend data for ids not currently on the canvas is kept in memory, so temporarily
    /// erasing a gate does not forget its wiring.
    /// </summary>
    public class EscultStudioDoc
    {
        public const string GateLetters = "ABDEFGHIJKLMNPQRSTUVWYZ";   // C/O/X are reserved glyphs

        public string Name = "new_level";
        public int W { get; private set; }
        public int H { get; private set; }
        public int Souls = 9;
        char[][] cells;                                                 // [row][col]

        // legend memory, keyed by glyph id — survives glyph erase/redraw
        public Dictionary<char, List<string>> Wiring = new Dictionary<char, List<string>>();   // altar -> target ids
        public Dictionary<char, bool> GateInitialOpen = new Dictionary<char, bool>();
        public Dictionary<char, bool> GateOverPit = new Dictionary<char, bool>();
        public HashSet<string> Decoys = new HashSet<string>();

        readonly List<string> undo = new List<string>();
        readonly List<string> redo = new List<string>();
        const int UndoCap = 200;

        public event Action Changed;                                    // fired after every committed mutation

        // ------------------------------------------------------------------
        //  construction
        // ------------------------------------------------------------------

        public static EscultStudioDoc NewDefault(string name = "new_level")
        {
            var d = new EscultStudioDoc { Name = name };
            d.Resize(16, 10);
            for (int r = 0; r < d.H; r++)
                for (int c = 0; c < d.W; c++)
                    d.cells[r][c] = (r == 0 || c == 0 || r == d.H - 1 || c == d.W - 1) ? '#' : '.';
            d.cells[d.H / 2][1] = '@';
            d.cells[d.H / 2][2] = 'C';
            d.cells[d.H / 2][d.W - 2] = 'O';
            return d;
        }

        public static EscultStudioDoc FromTopology(Topology t)
        {
            var d = new EscultStudioDoc { Name = t.Name, Souls = t.SoulBudget };
            var canvas = EsnParser.BuildCanvas(t);
            d.Resize(t.W, t.H);
            for (int r = 0; r < t.H; r++) d.cells[r] = canvas[r].ToCharArray();
            foreach (var a in t.Altars) d.Wiring[a.Id[0]] = new List<string>(a.Targets);
            foreach (var g in t.Gates) { d.GateInitialOpen[g.Id[0]] = !g.InitialClosed; d.GateOverPit[g.Id[0]] = g.OverPit; }
            foreach (var id in t.Decoys) d.Decoys.Add(id);
            return d;
        }

        /// <summary>Parse ESN text into a fresh doc. Returns null when the canvas cannot be read at all.</summary>
        public static EscultStudioDoc FromEsnText(string text, string name)
        {
            var parse = EsnParser.Parse(text, name);
            if (parse.Topology != null) return FromTopology(parse.Topology);

            // Canvas may still be salvageable even when the legend/spawns fail hard lints.
            var rows = new List<string>();
            foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                bool canvasLine = line.Length >= 2 && line.All(ch => "#.~@C=123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".IndexOf(ch) >= 0);
                if (canvasLine) rows.Add(line);
                else if (rows.Count > 0) break;
            }
            if (rows.Count == 0) return null;
            int w = rows.Max(r => r.Length);
            var d = new EscultStudioDoc { Name = name };
            d.Resize(w, rows.Count);
            for (int r = 0; r < d.H; r++)
                for (int c = 0; c < d.W; c++)
                    d.cells[r][c] = c < rows[r].Length ? rows[r][c] : '#';
            return d;
        }

        void Resize(int w, int h)
        {
            W = w; H = h;
            cells = new char[h][];
            for (int r = 0; r < h; r++) { cells[r] = new char[w]; for (int c = 0; c < w; c++) cells[r][c] = '#'; }
        }

        // ------------------------------------------------------------------
        //  queries
        // ------------------------------------------------------------------

        public char Get(int col, int row) { return cells[row][col]; }
        public bool InBounds(int col, int row) { return col >= 0 && col < W && row >= 0 && row < H; }

        public IEnumerable<char> PresentAltars()
        {
            var seen = new SortedSet<char>();
            for (int r = 0; r < H; r++) foreach (char ch in cells[r]) if (ch >= '1' && ch <= '9') seen.Add(ch);
            return seen;
        }

        public IEnumerable<char> PresentGates()
        {
            var seen = new SortedSet<char>();
            for (int r = 0; r < H; r++) foreach (char ch in cells[r]) if (GateLetters.IndexOf(ch) >= 0) seen.Add(ch);
            return seen;
        }

        public char NextFreeAltar()
        {
            var used = new HashSet<char>(PresentAltars());
            for (char c = '1'; c <= '9'; c++) if (!used.Contains(c)) return c;
            return '\0';
        }

        public char NextFreeGate()
        {
            var used = new HashSet<char>(PresentGates());
            foreach (char c in GateLetters) if (!used.Contains(c)) return c;
            return '\0';
        }

        /// <summary>A short signature of the structural id set — used to know when legend UI must rebuild.</summary>
        public string StructureSignature()
        {
            return new string(PresentAltars().ToArray()) + "|" + new string(PresentGates().ToArray()) + "|" + W + "x" + H;
        }

        // ------------------------------------------------------------------
        //  ESN serialization
        // ------------------------------------------------------------------

        public string ToEsnText()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < H; r++) sb.Append(cells[r]).Append('\n');
            sb.Append('\n');
            sb.Append("souls: ").Append(Souls).Append('\n');
            var gates = new HashSet<char>(PresentGates());
            foreach (char a in PresentAltars())
            {
                if (!Wiring.TryGetValue(a, out var targets)) continue;
                var live = targets.Where(t => t.Length == 1 ? gates.Contains(t[0]) : t.StartsWith("d")).ToList();
                if (live.Count > 0) sb.Append(a).Append(" -> ").Append(string.Join(", ", live)).Append('\n');
            }
            foreach (char g in gates)
            {
                bool open = GateInitialOpen.TryGetValue(g, out var o) && o;
                bool pit = GateOverPit.TryGetValue(g, out var p) && p;
                sb.Append(g).Append(": initial=").Append(open ? "OPEN" : "CLOSED").Append(pit ? " over=PIT" : "").Append('\n');
            }
            var ids = new HashSet<string>(gates.Select(g => g.ToString()));
            foreach (char a in PresentAltars()) ids.Add(a.ToString());
            var decoys = Decoys.Where(d => ids.Contains(d)).ToList();
            if (decoys.Count > 0) sb.Append("decoys: ").Append(string.Join(", ", decoys)).Append('\n');
            return sb.ToString();
        }

        public ParseResult Parse() { return EsnParser.Parse(ToEsnText(), Name); }

        // ------------------------------------------------------------------
        //  editing (all mutations go through Commit for undo)
        // ------------------------------------------------------------------

        string PendingSnapshot;

        /// <summary>Call once before a stroke/legend change; pair with Commit().</summary>
        public void BeginEdit() { if (PendingSnapshot == null) PendingSnapshot = Snapshot(); }

        public void Commit()
        {
            if (PendingSnapshot == null) return;
            if (PendingSnapshot != Snapshot())
            {
                undo.Add(PendingSnapshot);
                if (undo.Count > UndoCap) undo.RemoveAt(0);
                redo.Clear();
                Changed?.Invoke();
            }
            PendingSnapshot = null;
        }

        string Snapshot()
        {
            // canvas + legend + name + souls; wiring memory of absent ids intentionally excluded
            return Name + "\u0001" + ToEsnText();
        }

        void Restore(string snap)
        {
            int cut = snap.IndexOf('\u0001');
            string name = snap.Substring(0, cut), esn = snap.Substring(cut + 1);
            var d = FromEsnText(esn, name);
            if (d == null) return;
            Name = d.Name; W = d.W; H = d.H; cells = d.cells; Souls = d.Souls;
            Wiring = d.Wiring; GateInitialOpen = d.GateInitialOpen; GateOverPit = d.GateOverPit; Decoys = d.Decoys;
        }

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public void Undo()
        {
            if (undo.Count == 0) return;
            redo.Add(Snapshot());
            Restore(undo[undo.Count - 1]);
            undo.RemoveAt(undo.Count - 1);
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            undo.Add(Snapshot());
            Restore(redo[redo.Count - 1]);
            redo.RemoveAt(redo.Count - 1);
            Changed?.Invoke();
        }

        /// <summary>Paint a glyph. Girl/Cat glyphs are unique — placing them removes the previous one.</summary>
        public void SetCell(int col, int row, char glyph)
        {
            if (!InBounds(col, row)) return;
            if (glyph == '@' || glyph == 'C') RemoveAll(glyph);
            cells[row][col] = glyph;
        }

        void RemoveAll(char glyph)
        {
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    if (cells[r][c] == glyph) cells[r][c] = '.';
        }

        /// <summary>Resize keeping the top-left anchored; new cells become wall.</summary>
        public void ResizeCanvas(int newW, int newH)
        {
            newW = Math.Max(3, Math.Min(60, newW));
            newH = Math.Max(3, Math.Min(40, newH));
            if (newW == W && newH == H) return;
            var old = cells; int oldW = W, oldH = H;
            Resize(newW, newH);
            for (int r = 0; r < Math.Min(oldH, newH); r++)
                for (int c = 0; c < Math.Min(oldW, newW); c++)
                    cells[r][c] = old[r][c];
        }

        /// <summary>Replace the whole document from ESN text (undoable). False when the text has no readable canvas.</summary>
        public bool ApplyEsnText(string text)
        {
            var d = FromEsnText(text, Name);
            if (d == null) return false;
            BeginEdit();
            W = d.W; H = d.H; cells = d.cells; Souls = d.Souls;
            Wiring = d.Wiring; GateInitialOpen = d.GateInitialOpen; GateOverPit = d.GateOverPit; Decoys = d.Decoys;
            Commit();
            return true;
        }

        public void ToggleWiring(char altar, string targetId)
        {
            if (!Wiring.TryGetValue(altar, out var list)) Wiring[altar] = list = new List<string>();
            if (!list.Remove(targetId)) list.Add(targetId);
        }

        public bool IsWired(char altar, string targetId)
        {
            return Wiring.TryGetValue(altar, out var list) && list.Contains(targetId);
        }
    }

    // ======================================================================
    //  Solution replay: reconstructs board state at every witness step so the
    //  canvas can scrub through the solver's certified solution.
    // ======================================================================

    public class EscultReplay
    {
        public class Frame
        {
            public int Girl, Cat;               // cell index, -1 = n/a
            public bool CatHeld, CatDead;
            public int Souls;
            public bool[] GateOpen;             // by topology gate index
            public HashSet<int> Bridges = new HashSet<int>();
            public string Caption = "";
        }

        public readonly List<Frame> Frames = new List<Frame>();
        public readonly Topology T;

        public EscultReplay(Topology t, List<EscultSolver.SolveStep> witness)
        {
            T = t;
            var f = new Frame
            {
                Girl = t.GirlSpawn,
                Cat = t.CatSpawn,
                Souls = t.SoulBudget,
                GateOpen = t.Gates.Select(g => !g.InitialClosed).ToArray(),
                Caption = "initial state",
            };
            Frames.Add(f);
            foreach (var s in witness)
            {
                f = Clone(f);
                Apply(f, s);
                Frames.Add(f);
            }
        }

        Frame Clone(Frame f)
        {
            return new Frame
            {
                Girl = f.Girl, Cat = f.Cat, CatHeld = f.CatHeld, CatDead = f.CatDead, Souls = f.Souls,
                GateOpen = (bool[])f.GateOpen.Clone(),
                Bridges = new HashSet<int>(f.Bridges),
            };
        }

        void Apply(Frame f, EscultSolver.SolveStep s)
        {
            switch (s.Op)
            {
                case "MOVE":
                    if (s.Actor == "GIRL") { f.Girl = s.To; if (f.CatHeld) f.Cat = s.To; }
                    else f.Cat = s.To;
                    f.Caption = $"{s.Actor} moves";
                    break;
                case "PICKUP": f.CatHeld = true; f.Cat = f.Girl; f.Caption = "Girl picks up Cat"; break;
                case "DROP": f.CatHeld = false; f.Cat = f.Girl; f.Caption = "Girl drops Cat"; break;
                case "THROW":
                    f.CatHeld = false;
                    f.Cat = s.Lands >= 0 ? s.Lands : f.Girl;
                    f.Caption = $"throw {s.Dir}";
                    if (s.Lands >= 0 && T.Terrain[s.Lands] == 'P' && !f.Bridges.Contains(s.Lands))
                    { f.Cat = f.Girl; f.Caption += " — lands in pit, recalled (-1 soul)"; }
                    break;
                case "SACRIFICE":
                    f.Caption = $"sacrifice at altar {s.AltarId}";
                    var altar = T.Altars.FirstOrDefault(a => a.Id == s.AltarId);
                    if (altar != null)
                        foreach (var target in altar.Targets)
                        {
                            int gi = T.GateIndex(target);
                            if (gi >= 0) f.GateOpen[gi] = !f.GateOpen[gi];
                        }
                    break;
                case "BRIDGE": f.Bridges.Add(s.Cell); f.Caption = "Cat builds bridge (-1 soul)"; break;
                case "EXIT": f.Caption = "Girl exits — solved!"; break;
                default: f.Caption = s.Op; break;
            }
            if (s.SoulsAfter >= 0) f.Souls = s.SoulsAfter;
            if (f.Souls <= 0 && T.CatInLevel) f.CatDead = f.Souls == 0 && f.CatDead;
        }
    }
}
