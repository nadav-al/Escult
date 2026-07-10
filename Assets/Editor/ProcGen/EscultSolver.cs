using System;
using System.Collections.Generic;
using System.Text;

namespace Escult.ProcGen
{
    /// <summary>
    /// Exhaustive reference solver (Docs/ProcGen/02_Generation_Pipeline.md §1.3).
    /// Models exactly the R§2 action alphabet over R§5.2 states. Free moves are
    /// abstracted to connected-component closures; successor states are generated
    /// only at commitment points. Character positions are canonicalized to the
    /// minimum cell index of their component, which keeps the state space small
    /// without losing any distinctions that matter (all commitment cells are
    /// re-enumerated from the closure at expansion time).
    /// </summary>
    public class EscultSolver
    {
        public const byte CAT_GROUND = 0, CAT_HELD = 1, CAT_DEAD = 2, CAT_ABSENT = 3;
        const int INF = int.MaxValue / 4;

        public class SolveStep
        {
            public string Op;          // MOVE PICKUP DROP THROW SACRIFICE BRIDGE EXIT
            public string Actor;       // GIRL | CAT
            public int To = -1;        // MOVE target
            public string AltarId;     // SACRIFICE
            public int Cell = -1;      // BRIDGE cell / THROW origin
            public string Dir;         // THROW: N S E W
            public int Lands = -1;     // THROW landing
            public int SoulsAfter = -1;
        }

        class Edge
        {
            public int From, To;
            public int Cost;                       // souls
            public List<SolveStep> Steps;          // micro-steps incl. commitment MOVEs
            public string AltarUsed, DoorUsed;     // element usage for V6
        }

        class Node
        {
            public int Girl; public byte Cat; public int CatCell; public int Souls;
            public int GateMask, DoorMask;         // bit set = OPEN
            public ulong[] Bridges;
        }

        public class Result
        {
            public bool Solvable;
            public int MinCost;
            public List<SolveStep> Witness = new List<SolveStep>();
            public long SolutionCount; public bool SolutionCountCapped;
            public int ReachableStates, DeadStates, StructuralTrapEntries, MaxSoulsSunkInTrap;
            public HashSet<string> UsedAltars = new HashSet<string>();
            public HashSet<string> UsedGates = new HashSet<string>();
            public HashSet<string> UsedDoors = new HashSet<string>();
            public int WitnessThrows, WitnessMaxAltarFires, WitnessAlternations, WitnessLength, WitnessCostedActions;
            public bool RayEscape;                 // a reachable throw ray left the canvas (V1 violation witnessed)
            public string Error;                   // state-space explosion etc.
        }

        readonly Topology T;
        readonly int MaxNodes;
        readonly int BridgeWords;

        List<Node> nodes = new List<Node>();
        Dictionary<string, int> keyToId = new Dictionary<string, int>();
        List<List<Edge>> outEdges = new List<List<Edge>>();
        Result result = new Result();
        const int WIN = 0;                          // reserved node id

        public EscultSolver(Topology t, int maxNodes = 400000) { T = t; MaxNodes = maxNodes; BridgeWords = (t.N + 63) / 64; }

        static bool Bit(ulong[] set, int i) { return set != null && (set[i >> 6] & (1UL << (i & 63))) != 0; }
        static ulong[] With(ulong[] set, int i, int words)
        {
            var n = new ulong[words];
            if (set != null) Array.Copy(set, n, words);
            n[i >> 6] |= 1UL << (i & 63);
            return n;
        }

        string Key(Node s)
        {
            var sb = new StringBuilder(48);
            sb.Append(s.Girl).Append('|').Append(s.Cat).Append('|').Append(s.CatCell).Append('|')
              .Append(s.Souls).Append('|').Append(s.GateMask).Append('|').Append(s.DoorMask);
            for (int i = 0; i < BridgeWords; i++) { sb.Append('|'); sb.Append(s.Bridges[i].ToString("x")); }
            return sb.ToString();
        }

        bool[] Walkable(Node s)
        {
            var w = new bool[T.N];
            for (int i = 0; i < T.N; i++)
            {
                char ter = T.Terrain[i];
                bool ground = ter == 'G' || (ter == 'P' && Bit(s.Bridges, i));
                if (!ground) continue;
                int g = T.GateAt[i];
                if (g >= 0 && ((s.GateMask >> g) & 1) == 0) continue;   // closed gate acts as wall
                if (T.AltarAt[i] >= 0) continue;                        // altar blocks walk
                if (T.DoorAt[i] >= 0) continue;                         // door blocks walk
                w[i] = true;
            }
            return w;
        }

        /// <summary>cell blocks a throw ray: wall, CLOSED gate cell, or door (any state).</summary>
        bool BlocksRay(Node s, int i)
        {
            if (T.Terrain[i] == 'W') return true;
            if (T.DoorAt[i] >= 0) return true;
            int g = T.GateAt[i];
            return g >= 0 && ((s.GateMask >> g) & 1) == 0;
        }

        /// <summary>Label components of a walkable grid. comp[i] = component id or -1; compCells[k] ascending; rep = compCells[k][0].</summary>
        void Components(bool[] walk, out int[] comp, out List<List<int>> compCells)
        {
            comp = new int[T.N];
            for (int i = 0; i < T.N; i++) comp[i] = -1;
            compCells = new List<List<int>>();
            var queue = new Queue<int>();
            for (int i = 0; i < T.N; i++)
            {
                if (!walk[i] || comp[i] >= 0) continue;
                int id = compCells.Count;
                var cells = new List<int>();
                compCells.Add(cells);
                comp[i] = id; queue.Enqueue(i);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    cells.Add(cur);
                    foreach (var n in T.Neighbors(cur))
                        if (walk[n] && comp[n] < 0) { comp[n] = id; queue.Enqueue(n); }
                }
                cells.Sort();
            }
        }

        int NodeId(Node s)
        {
            string k = Key(s);
            int id;
            if (keyToId.TryGetValue(k, out id)) return id;
            id = nodes.Count;
            keyToId[k] = id;
            nodes.Add(s);
            outEdges.Add(new List<Edge>());
            return id;
        }

        public Result Solve()
        {
            // node 0 is the virtual WIN node
            nodes.Add(new Node()); outEdges.Add(new List<Edge>()); keyToId["WIN"] = WIN;

            var init = new Node
            {
                Girl = T.GirlSpawn,
                Cat = T.CatInLevel ? CAT_GROUND : CAT_ABSENT,
                CatCell = T.CatInLevel ? T.CatSpawn : -1,
                Souls = T.SoulBudget,
                Bridges = new ulong[BridgeWords]
            };
            for (int g = 0; g < T.Gates.Count; g++) if (!T.Gates[g].InitialClosed) init.GateMask |= 1 << g;
            for (int d = 0; d < T.Doors.Count; d++) if (T.Doors[d].InitialOpen) init.DoorMask |= 1 << d;

            // canonicalize initial positions
            {
                var walk = Walkable(init);
                if (!walk[init.Girl]) { result.Error = "girl spawn is not on a walkable cell"; return result; }
                if (init.Cat == CAT_GROUND && !walk[init.CatCell]) { result.Error = "cat spawn is not on a walkable cell"; return result; }
                int[] comp; List<List<int>> cells;
                Components(walk, out comp, out cells);
                init.Girl = cells[comp[init.Girl]][0];
                if (init.Cat == CAT_GROUND) init.CatCell = cells[comp[init.CatCell]][0];
            }
            int start = NodeId(init);

            // ---- forward Dijkstra, lexicographic (souls spent, micro-steps) ----
            var distS = new List<int> { INF, INF };   // grows with nodes
            var distT = new List<int> { INF, INF };
            var predEdge = new List<Edge> { null, null };
            while (distS.Count < nodes.Count) { distS.Add(INF); distT.Add(INF); predEdge.Add(null); }
            distS[start] = 0; distT[start] = 0;

            var pq = new MinHeap();
            pq.Push(0, 0, start);
            var settled = new List<bool> { false, false };

            while (pq.Count > 0)
            {
                int ds, dt, u;
                pq.Pop(out ds, out dt, out u);
                while (settled.Count < nodes.Count) settled.Add(false);
                if (settled[u]) continue;
                if (ds != distS[u] || dt != distT[u]) continue;
                settled[u] = true;
                if (u == WIN) continue;

                if (nodes.Count > MaxNodes) { result.Error = $"state space exceeded {MaxNodes} nodes — level too large for exhaustive solve"; return result; }

                foreach (var e in Expand(u))
                {
                    outEdges[u].Add(e);
                    while (distS.Count < nodes.Count) { distS.Add(INF); distT.Add(INF); predEdge.Add(null); }
                    int ns = ds + e.Cost, nt = dt + e.Steps.Count;
                    if (ns < distS[e.To] || (ns == distS[e.To] && nt < distT[e.To]))
                    {
                        distS[e.To] = ns; distT[e.To] = nt; predEdge[e.To] = e;
                        pq.Push(ns, nt, e.To);
                    }
                }
            }

            result.ReachableStates = nodes.Count - 1;
            result.Solvable = distS[WIN] < INF;
            result.MinCost = result.Solvable ? distS[WIN] : -1;

            // ---- backward Dijkstra (souls only): min remaining cost to WIN ----
            var rem = new int[nodes.Count];
            for (int i = 0; i < rem.Length; i++) rem[i] = INF;
            var revEdges = new List<Edge>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) revEdges[i] = new List<Edge>();
            for (int u = 0; u < nodes.Count; u++)
                foreach (var e in outEdges[u]) revEdges[e.To].Add(e);
            {
                var bpq = new MinHeap();
                rem[WIN] = 0; bpq.Push(0, 0, WIN);
                while (bpq.Count > 0)
                {
                    int ds, dt, v; bpq.Pop(out ds, out dt, out v);
                    if (ds > rem[v]) continue;
                    foreach (var e in revEdges[v])
                        if (rem[v] + e.Cost < rem[e.From]) { rem[e.From] = rem[v] + e.Cost; bpq.Push(rem[e.From], 0, e.From); }
                }
            }

            // ---- dead-end / trap audit (D3) ----
            int deadStates = 0, structEntries = 0, maxSunk = 0;
            var structDead = new HashSet<int>();
            for (int u = 1; u < nodes.Count; u++)
            {
                if (distS[u] >= INF) continue;              // unreached (shouldn't happen)
                if (rem[u] < INF) continue;
                deadStates++;
                if (nodes[u].Souls > 0)                     // dead with souls left = structural trap, not mere exhaustion
                {
                    structDead.Add(u);
                    int sunk = T.SoulBudget - nodes[u].Souls;
                    if (sunk > maxSunk) maxSunk = sunk;
                }
            }
            var countedEntries = new HashSet<int>();
            for (int u = 1; u < nodes.Count; u++)
            {
                if (distS[u] >= INF || rem[u] >= INF) continue;   // only alive states
                foreach (var e in outEdges[u])
                    if (structDead.Contains(e.To) && countedEntries.Add(e.To)) structEntries++;
            }
            result.DeadStates = deadStates;
            result.StructuralTrapEntries = structEntries;
            result.MaxSoulsSunkInTrap = maxSunk;

            if (!result.Solvable) return result;

            // ---- element usage over ALL soul-minimal solutions (V6) ----
            for (int u = 1; u < nodes.Count; u++)
            {
                if (distS[u] >= INF || rem[u] >= INF) continue;
                foreach (var e in outEdges[u])
                {
                    if (rem[e.To] >= INF) continue;
                    if (distS[u] + e.Cost + rem[e.To] != result.MinCost) continue;
                    if (e.AltarUsed != null)
                    {
                        result.UsedAltars.Add(e.AltarUsed);
                        var altar = T.Altars.Find(a => a.Id == e.AltarUsed);
                        foreach (var tg in altar.Targets)
                            if (tg.StartsWith("d")) result.UsedDoors.Add(tg); else result.UsedGates.Add(tg);
                    }
                    if (e.DoorUsed != null) result.UsedDoors.Add(e.DoorUsed);
                }
            }

            // ---- count (souls, steps)-minimal solutions on the shortest-path DAG ----
            {
                var order = new List<int>();
                for (int u = 0; u < nodes.Count; u++) if (distS[u] < INF) order.Add(u);
                order.Sort((a, b) => distS[a] != distS[b] ? distS[a] - distS[b] : distT[a] - distT[b]);
                var count = new long[nodes.Count];
                count[start] = 1;
                bool capped = false;
                const long CAP = 1000000;
                foreach (var u in order)
                {
                    if (count[u] == 0) continue;
                    foreach (var e in outEdges[u])
                    {
                        if (distS[u] + e.Cost == distS[e.To] && distT[u] + e.Steps.Count == distT[e.To])
                        {
                            count[e.To] += count[u];
                            if (count[e.To] > CAP) { count[e.To] = CAP; capped = true; }
                        }
                    }
                }
                result.SolutionCount = count[WIN];
                result.SolutionCountCapped = capped;
            }

            // ---- witness reconstruction ----
            {
                var chain = new List<Edge>();
                int cur = WIN;
                while (cur != start)
                {
                    var e = predEdge[cur];
                    if (e == null) break;
                    chain.Add(e);
                    cur = e.From;
                }
                chain.Reverse();
                int souls = T.SoulBudget;
                foreach (var e in chain)
                {
                    foreach (var st in e.Steps)
                    {
                        if (st.Op == "SACRIFICE" || st.Op == "BRIDGE" || (st.Op == "THROW" && st.SoulsAfter == -2))
                        {
                            souls -= 1;
                            st.SoulsAfter = souls;
                        }
                        result.Witness.Add(st);
                    }
                }

                // witness-derived difficulty inputs
                var fires = new Dictionary<string, int>();
                string lastActor = null;
                foreach (var st in result.Witness)
                {
                    if (st.Op == "THROW") result.WitnessThrows++;
                    if (st.Op == "SACRIFICE")
                    {
                        int f; fires.TryGetValue(st.AltarId, out f); fires[st.AltarId] = f + 1;
                    }
                    if (st.Op == "SACRIFICE" || st.Op == "BRIDGE" || (st.Op == "THROW" && st.SoulsAfter >= 0)) result.WitnessCostedActions++;
                    if (st.Actor != null && st.Op != "EXIT")
                    {
                        if (lastActor != null && st.Actor != lastActor) result.WitnessAlternations++;
                        lastActor = st.Actor;
                    }
                }
                foreach (var kv in fires) if (kv.Value > result.WitnessMaxAltarFires) result.WitnessMaxAltarFires = kv.Value;
                result.WitnessLength = result.Witness.Count > 0 ? result.Witness.Count - 1 : 0;   // exclude EXIT
            }

            return result;
        }

        // ------------------------------------------------------------------
        // successor generation at commitment points
        // ------------------------------------------------------------------
        List<Edge> Expand(int id)
        {
            var s = nodes[id];
            var edges = new List<Edge>();
            var local = new HashSet<string>();      // dedup successors per source node

            var walk = Walkable(s);
            int[] comp; List<List<int>> compCells;
            Components(walk, out comp, out compCells);
            int gComp = comp[s.Girl];
            var girlCells = compCells[gComp];

            void AddEdge(Node ns, int cost, List<SolveStep> steps, string altarUsed = null, string doorUsed = null)
            {
                int to = NodeId(ns);
                string dk = to + "/" + cost;
                if (!local.Add(dk)) return;
                edges.Add(new Edge { From = id, To = to, Cost = cost, Steps = steps, AltarUsed = altarUsed, DoorUsed = doorUsed });
            }

            List<SolveStep> Steps(params SolveStep[] arr) { return new List<SolveStep>(arr); }
            SolveStep MoveG(int to) { return new SolveStep { Op = "MOVE", Actor = "GIRL", To = to }; }
            SolveStep MoveC(int to) { return new SolveStep { Op = "MOVE", Actor = "CAT", To = to }; }

            // EXIT — girl adjacent to an open door
            for (int d = 0; d < T.Doors.Count; d++)
            {
                if (((s.DoorMask >> d) & 1) == 0) continue;
                foreach (var n in T.Neighbors(T.Doors[d].Cell))
                {
                    if (!walk[n] || comp[n] != gComp) continue;
                    var steps = Steps(MoveG(n), new SolveStep { Op = "EXIT", Actor = "GIRL" });
                    var winEdge = new Edge { From = id, To = WIN, Cost = 0, Steps = steps, DoorUsed = T.Doors[d].Id };
                    string dk = "WIN/" + T.Doors[d].Id;
                    if (local.Add(dk)) edges.Add(winEdge);
                    break;
                }
            }

            // PICKUP — same component (canonical positions equal component reps)
            if (s.Cat == CAT_GROUND && comp[s.CatCell] == gComp)
            {
                var ns = Clone(s); ns.Cat = CAT_HELD; ns.CatCell = -1;
                AddEdge(ns, 0, Steps(MoveG(s.CatCell), new SolveStep { Op = "PICKUP", Actor = "GIRL" }));
            }

            if (s.Cat == CAT_HELD)
            {
                // DROP
                {
                    var ns = Clone(s); ns.Cat = CAT_GROUND; ns.CatCell = s.Girl;
                    AddEdge(ns, 0, Steps(new SolveStep { Op = "DROP", Actor = "GIRL" }));
                }
                // THROW — from every girl-closure cell in 4 directions
                int[] dc = { 0, 0, -1, 1 };
                int[] dr = { -1, 1, 0, 0 };
                string[] dirName = { "N", "S", "W", "E" };
                foreach (var g in girlCells)
                {
                    for (int di = 0; di < 4; di++)
                    {
                        int c = T.Col(g), r = T.Row(g);
                        int landC = c, landR = r;
                        bool escaped = false;
                        while (true)
                        {
                            int nc = landC + dc[di], nr = landR + dr[di];
                            if (!T.InBounds(nc, nr)) { escaped = true; break; }
                            if (BlocksRay(s, T.Idx(nc, nr))) break;
                            landC = nc; landR = nr;
                        }
                        if (escaped) { result.RayEscape = true; continue; }
                        int land = T.Idx(landC, landR);
                        if (land == g) continue;                       // no-op throw
                        if (T.AltarAt[land] >= 0) continue;            // illegal landing
                        if (walk[land])
                        {
                            var ns = Clone(s); ns.Cat = CAT_GROUND; ns.CatCell = compCells[comp[land]][0];
                            var mv = new List<SolveStep>();
                            if (g != s.Girl) mv.Add(MoveG(g));
                            mv.Add(new SolveStep { Op = "THROW", Actor = "GIRL", Cell = g, Dir = dirName[di], Lands = land });
                            AddEdge(ns, 0, mv);
                        }
                        else if (T.Terrain[land] == 'P' && !Bit(s.Bridges, land) && s.Souls >= 1)
                        {
                            // pit landing: -1 soul, cat recalled to girl (or dies)
                            var ns = Clone(s); ns.Souls = s.Souls - 1;
                            if (ns.Souls == 0) { ns.Cat = CAT_DEAD; ns.CatCell = -1; }
                            else { ns.Cat = CAT_GROUND; ns.CatCell = s.Girl; }
                            var mv = new List<SolveStep>();
                            if (g != s.Girl) mv.Add(MoveG(g));
                            mv.Add(new SolveStep { Op = "THROW", Actor = "GIRL", Cell = g, Dir = dirName[di], Lands = land, SoulsAfter = -2 });
                            AddEdge(ns, 1, mv);
                        }
                    }
                }
            }

            if (s.Cat == CAT_GROUND && s.Souls >= 1)
            {
                int cComp = comp[s.CatCell];
                var catCells = compCells[cComp];
                var catSet = new HashSet<int>(catCells);

                // SACRIFICE
                for (int a = 0; a < T.Altars.Count; a++)
                {
                    var altar = T.Altars[a];
                    if (altar.Targets.Count == 0) continue;

                    var wiredCells = new HashSet<int>();
                    int toggleGates = 0, toggleDoors = 0;
                    foreach (var tg in altar.Targets)
                    {
                        if (tg.StartsWith("d")) { toggleDoors |= 1 << T.DoorIndex(tg); }
                        else
                        {
                            int gi = T.GateIndex(tg);
                            toggleGates |= 1 << gi;
                            foreach (var cell in T.Gates[gi].Cells) wiredCells.Add(cell);
                        }
                    }

                    // cat standing spots: adjacent to altar, in cat's closure, off wired gate cells
                    var catStand = new List<int>();
                    foreach (var n in T.Neighbors(altar.Cell))
                        if (catSet.Contains(n) && !wiredCells.Contains(n)) catStand.Add(n);
                    if (catStand.Count == 0) continue;

                    // girl standing spots: her closure minus wired gate cells (anti-crush interlock V8)
                    var girlStand = new List<int>();
                    foreach (var c in girlCells) if (!wiredCells.Contains(c)) girlStand.Add(c);
                    if (girlStand.Count == 0) continue;

                    var after = Clone(s);
                    after.GateMask ^= toggleGates;
                    after.DoorMask ^= toggleDoors;
                    after.Souls = s.Souls - 1;
                    bool catDies = after.Souls == 0;

                    var nwalk = Walkable(after);
                    int[] ncomp; List<List<int>> ncells;
                    Components(nwalk, out ncomp, out ncells);

                    // branch over the distinct components each character can end up in
                    var girlReps = new Dictionary<int, int>();       // new rep -> chosen standing cell
                    foreach (var gs in girlStand)
                    {
                        int rep = ncells[ncomp[gs]][0];
                        if (!girlReps.ContainsKey(rep)) girlReps[rep] = gs;
                    }
                    var catReps = new Dictionary<int, int>();
                    if (catDies) catReps[-1] = catStand[0];
                    else foreach (var x in catStand)
                    {
                        int rep = ncells[ncomp[x]][0];
                        if (!catReps.ContainsKey(rep)) catReps[rep] = x;
                    }

                    foreach (var gkv in girlReps)
                    {
                        foreach (var ckv in catReps)
                        {
                            var ns = Clone(after);
                            ns.Girl = gkv.Key;
                            if (catDies) { ns.Cat = CAT_DEAD; ns.CatCell = -1; }
                            else ns.CatCell = ckv.Key;
                            var mv = new List<SolveStep>();
                            if (ckv.Value != s.CatCell) mv.Add(MoveC(ckv.Value));
                            if (gkv.Value != s.Girl) mv.Add(MoveG(gkv.Value));
                            mv.Add(new SolveStep { Op = "SACRIFICE", Actor = "CAT", AltarId = altar.Id });
                            AddEdge(ns, 1, mv, altar.Id);
                        }
                    }
                }

                // BRIDGE — any pit cell (not under a CLOSED gate, not already bridged) adjacent to cat's closure
                for (int p = 0; p < T.N; p++)
                {
                    if (T.Terrain[p] != 'P' || Bit(s.Bridges, p)) continue;
                    int pg = T.GateAt[p];
                    if (pg >= 0 && ((s.GateMask >> pg) & 1) == 0) continue;   // closed gate forbids bridging under it
                    int stand = -1;
                    foreach (var n in T.Neighbors(p))
                        if (walk[n] && comp[n] == cComp) { stand = n; break; }
                    if (stand < 0) continue;

                    var ns = Clone(s);
                    ns.Bridges = With(s.Bridges, p, BridgeWords);
                    ns.Souls = s.Souls - 1;
                    bool catDies = ns.Souls == 0;

                    var nwalk = Walkable(ns);
                    int[] ncomp; List<List<int>> ncells;
                    Components(nwalk, out ncomp, out ncells);
                    ns.Girl = ncells[ncomp[s.Girl]][0];
                    if (catDies) { ns.Cat = CAT_DEAD; ns.CatCell = -1; }
                    else ns.CatCell = ncells[ncomp[stand]][0];

                    var mv = new List<SolveStep>();
                    if (stand != s.CatCell) mv.Add(MoveC(stand));
                    mv.Add(new SolveStep { Op = "BRIDGE", Actor = "CAT", Cell = p });
                    AddEdge(ns, 1, mv);
                }
            }

            return edges;
        }

        Node Clone(Node s)
        {
            return new Node
            {
                Girl = s.Girl, Cat = s.Cat, CatCell = s.CatCell, Souls = s.Souls,
                GateMask = s.GateMask, DoorMask = s.DoorMask, Bridges = s.Bridges
            };
        }

        /// <summary>Small binary min-heap over (a, b, id) — lexicographic.</summary>
        class MinHeap
        {
            struct Item { public int A, B, Id; }
            Item[] heap = new Item[256];
            public int Count;

            public void Push(int a, int b, int id)
            {
                if (Count == heap.Length) Array.Resize(ref heap, heap.Length * 2);
                heap[Count] = new Item { A = a, B = b, Id = id };
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (Less(heap[i], heap[p])) { var tmp = heap[i]; heap[i] = heap[p]; heap[p] = tmp; i = p; }
                    else break;
                }
            }

            public void Pop(out int a, out int b, out int id)
            {
                a = heap[0].A; b = heap[0].B; id = heap[0].Id;
                heap[0] = heap[--Count];
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < Count && Less(heap[l], heap[m])) m = l;
                    if (r < Count && Less(heap[r], heap[m])) m = r;
                    if (m == i) break;
                    var tmp = heap[i]; heap[i] = heap[m]; heap[m] = tmp; i = m;
                }
            }

            static bool Less(Item x, Item y) { return x.A != y.A ? x.A < y.A : x.B < y.B; }
        }
    }
}
