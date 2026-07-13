using System.Collections.Generic;

namespace Escult.ProcGen
{
    /// <summary>
    /// In-memory LevelTopology (Docs/ProcGen/01_Puzzle_Ruleset.md section 5.1).
    /// Cells are flat indexes: idx = row * W + col, row 0 = top (ESN frame).
    /// </summary>
    public class Gate
    {
        public string Id;                       // single letter, not C/O/X
        public List<int> Cells = new List<int>();
        public bool InitialClosed = true;
        public bool OverPit = false;            // terrain beneath every cell (v1: uniform per gate)
    }

    public class Altar
    {
        public string Id;                       // "1".."9"
        public int Cell;
        public List<string> Targets = new List<string>();  // gate letters and/or door ids ("d1")
    }

    public class Door
    {
        public string Id;                       // "d1".."dn" in canvas reading order
        public int Cell;
        public bool InitialOpen = false;
    }

    public class Topology
    {
        public string Name = "level";
        public int W, H;
        public char[] Terrain;                  // 'G' | 'P' | 'W' per cell (pure terrain, overlays resolved out)
        public List<Gate> Gates = new List<Gate>();
        public List<Altar> Altars = new List<Altar>();
        public List<Door> Doors = new List<Door>();
        public int GirlSpawn = -1;
        public int CatSpawn = -1;               // -1 => girl-only level
        public bool CatInLevel { get { return CatSpawn >= 0; } }
        public int SoulBudget = 9;
        public List<string> Decoys = new List<string>();
        public string[] CanvasRows;             // original ESN canvas (authoring source of truth)

        // Derived lookups (BuildLookups)
        public int[] GateAt;                    // cell -> gate index | -1
        public int[] AltarAt;                   // cell -> altar index | -1
        public int[] DoorAt;                    // cell -> door index | -1

        public int N { get { return W * H; } }
        public int Idx(int col, int row) { return row * W + col; }
        public int Col(int i) { return i % W; }
        public int Row(int i) { return i / W; }
        public bool InBounds(int col, int row) { return col >= 0 && col < W && row >= 0 && row < H; }

        public void BuildLookups()
        {
            GateAt = new int[N]; AltarAt = new int[N]; DoorAt = new int[N];
            for (int i = 0; i < N; i++) { GateAt[i] = -1; AltarAt[i] = -1; DoorAt[i] = -1; }
            for (int g = 0; g < Gates.Count; g++)
                foreach (var c in Gates[g].Cells) GateAt[c] = g;
            for (int a = 0; a < Altars.Count; a++) AltarAt[Altars[a].Cell] = a;
            for (int d = 0; d < Doors.Count; d++) DoorAt[Doors[d].Cell] = d;
        }

        public int GateIndex(string id)
        {
            for (int i = 0; i < Gates.Count; i++) if (Gates[i].Id == id) return i;
            return -1;
        }

        public int DoorIndex(string id)
        {
            for (int i = 0; i < Doors.Count; i++) if (Doors[i].Id == id) return i;
            return -1;
        }

        /// <summary>4-neighbor cell indexes (N, S, W, E order).</summary>
        public IEnumerable<int> Neighbors(int cell)
        {
            int c = Col(cell), r = Row(cell);
            if (r > 0) yield return cell - W;
            if (r < H - 1) yield return cell + W;
            if (c > 0) yield return cell - 1;
            if (c < W - 1) yield return cell + 1;
        }
    }

    public class LintMessage
    {
        public bool IsError;
        public string Text;
        public LintMessage(bool isError, string text) { IsError = isError; Text = text; }
        public override string ToString() { return (IsError ? "ERROR: " : "warn: ") + Text; }
    }

    public class ParseResult
    {
        public Topology Topology;               // null when errors prevent building one
        public List<LintMessage> Lints = new List<LintMessage>();
        public bool HasErrors
        {
            get { foreach (var l in Lints) if (l.IsError) return true; return false; }
        }
        public void Error(string msg) { Lints.Add(new LintMessage(true, msg)); }
        public void Warn(string msg) { Lints.Add(new LintMessage(false, msg)); }
    }
}
