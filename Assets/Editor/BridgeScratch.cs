using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using System.Reflection;
using System.Collections.Generic;
using System.Text;
using System.Linq;

/// <summary>
/// Scratch pad for Unity Bridge one-off scripts.
/// Edit Run() and execute via: {"type": "scratch"}
/// </summary>
public static class BridgeScratch
{
    private static T GetPrivate<T>(object obj, string field)
    {
        var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        return f == null ? default : (T)f.GetValue(obj);
    }

    public static string Run()
    {
        var gmGO = GameObject.Find("GameManager");
        var levelT = gmGO.transform.Find("Level 4");
        var grid = levelT.Find("Grid");

        var ground = grid.Find("Ground").GetComponent<Tilemap>();
        var hell = grid.Find("Collision - Hell").GetComponent<Tilemap>();
        var gate1GO = grid.Find("Gates/Gate 1").gameObject;
        var gate2GO = grid.Find("Gates/Gate 2").gameObject;
        var gate1 = gate1GO.GetComponent<Tilemap>();
        var gate2 = gate2GO.GetComponent<Tilemap>();
        var gate1Ctrl = gate1GO.GetComponent<GateContoller>();
        var gate2Ctrl = gate2GO.GetComponent<GateContoller>();

        var alter1 = grid.Find("Alters/Alter 1");
        var alter2 = grid.Find("Alters/Alter 2");
        var door = grid.Find("Door");

        var lm = levelT.GetComponent<LevelManager>();
        var catPos = GetPrivate<Vector3>(lm, "catPos");
        var girlPos = GetPrivate<Vector3>(lm, "girlPos");

        var catCell = ground.WorldToCell(catPos);
        var girlCell = ground.WorldToCell(girlPos);
        var alter1Cell = ground.WorldToCell(alter1.position);
        var alter2Cell = ground.WorldToCell(alter2.position);
        var doorCell = ground.WorldToCell(door.position);

        bool g1Init = GetPrivate<bool>(gate1Ctrl, "INITIAL_GATE_STATUS");
        bool g2Init = GetPrivate<bool>(gate2Ctrl, "INITIAL_GATE_STATUS");

        var sb = new StringBuilder();
        sb.AppendLine($"Cat start: {catCell}  Girl start: {girlCell}  Alter1: {alter1Cell}  Alter2: {alter2Cell}  Door: {doorCell}");
        sb.AppendLine($"Gate1 INITIAL_GATE_STATUS(activeSelf-on-reset)={g1Init}, Gate2={g2Init}");

        // what's beneath each gate tile (hell or ground)?
        void DescribeGate(string label, Tilemap gate)
        {
            var cells = new List<Vector3Int>();
            foreach (var pos in gate.cellBounds.allPositionsWithin)
                if (gate.HasTile(pos)) cells.Add(pos);
            int hellBeneath = cells.Count(c => hell.HasTile(c));
            int groundBeneath = cells.Count(c => ground.HasTile(c));
            sb.AppendLine($"{label}: {cells.Count} cells, {hellBeneath} over hell/pit, {groundBeneath} over ground. Cells: {string.Join(" ", cells)}");
        }
        DescribeGate("Gate1", gate1);
        DescribeGate("Gate2", gate2);

        // BFS: cat traversal, gates locked according to given open flags (true=locked, since activeSelf blocks per hasGates)
        List<Vector3Int> CatReachable(Vector3Int start, bool gate1Locked, bool gate2Locked, HashSet<Vector3Int> extraBridged)
        {
            var visited = new HashSet<Vector3Int> { start };
            var queue = new Queue<Vector3Int>();
            queue.Enqueue(start);
            var dirs = new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                foreach (var d in dirs)
                {
                    var next = cur + d;
                    if (visited.Contains(next)) continue;
                    bool locked = (gate1Locked && gate1.HasTile(next)) || (gate2Locked && gate2.HasTile(next));
                    if (locked) continue;
                    bool passable = ground.HasTile(next) || hell.HasTile(next) || extraBridged.Contains(next)
                                     || (gate1.HasTile(next) && !gate1Locked) || (gate2.HasTile(next) && !gate2Locked);
                    if (!passable) continue;
                    visited.Add(next);
                    queue.Enqueue(next);
                }
            }
            return visited.ToList();
        }

        var reachInit = CatReachable(catCell, g1Init, g2Init, new HashSet<Vector3Int>());
        sb.AppendLine($"\nCat reachable from start with gates in INITIAL state (Gate1 locked={g1Init}, Gate2 locked={g2Init}): {reachInit.Count} cells");
        sb.AppendLine($"  Alter1 reachable? {reachInit.Contains(alter1Cell)}");
        sb.AppendLine($"  Alter2 reachable? {reachInit.Contains(alter2Cell)}");
        sb.AppendLine($"  Door reachable? {reachInit.Contains(doorCell)}");

        // if alter1 reachable, open gate1, recompute
        if (reachInit.Contains(alter1Cell))
        {
            var reach2 = CatReachable(catCell, false, g2Init, new HashSet<Vector3Int>());
            sb.AppendLine($"\nAfter opening Gate1 (sacrifice at Alter1): reachable cells={reach2.Count}, Alter2 reachable? {reach2.Contains(alter2Cell)}, Door reachable? {reach2.Contains(doorCell)}");
            if (reach2.Contains(alter2Cell))
            {
                var reach3 = CatReachable(catCell, false, false, new HashSet<Vector3Int>());
                sb.AppendLine($"After also opening Gate2: reachable cells={reach3.Count}, Door reachable? {reach3.Contains(doorCell)}, Girl-start reachable(for cat)? {reach3.Contains(girlCell)}");

                sb.AppendLine("\nDoor neighbor diagnostic:");
                foreach (var d in new[] { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right })
                {
                    var n = doorCell + d;
                    sb.AppendLine($"  {d} -> {n}: ground={ground.HasTile(n)} hell={hell.HasTile(n)} gate1={gate1.HasTile(n)} gate2={gate2.HasTile(n)} inReach3={reach3.Contains(n)}");
                }
                sb.AppendLine($"  door itself: ground={ground.HasTile(doorCell)} hell={hell.HasTile(doorCell)}");

                // print the connected component containing alter1 as a mini map region bounds
                int minX = reach3.Min(c => c.x), maxX = reach3.Max(c => c.x);
                int minY = reach3.Min(c => c.y), maxY = reach3.Max(c => c.y);
                sb.AppendLine($"  reach3 bounding box: x[{minX}..{maxX}] y[{minY}..{maxY}]");
            }
        }

        return sb.ToString();
    }

    public static string RunOld()
    {
        var sb = new StringBuilder();
        var gm = GameObject.Find("GameManager");

        foreach (var levelName in new[] { "Level 1", "Level 2", "Level 3", "Level 4" })
        {
            var levelT = gm.transform.Find(levelName);
            if (levelT == null) { sb.AppendLine($"{levelName}: NOT FOUND"); continue; }
            var lm = levelT.GetComponent<LevelManager>();
            if (lm == null) { sb.AppendLine($"{levelName}: no LevelManager"); continue; }

            var gates = GetPrivate<List<GameObject>>(lm, "gates") ?? new List<GameObject>();
            var doors = GetPrivate<List<GameObject>>(lm, "doors") ?? new List<GameObject>();
            var altars = GetPrivate<List<GameObject>>(lm, "altars") ?? new List<GameObject>();
            var isDoorOpen = GetPrivate<bool>(lm, "isDoorOpen");
            var girlPos = GetPrivate<Vector3>(lm, "girlPos");
            var catPos = GetPrivate<Vector3>(lm, "catPos");
            var isCatInLevel = GetPrivate<bool>(lm, "isCatInLevel");

            sb.AppendLine($"## {levelName}");
            sb.AppendLine($"CatInLevel={isCatInLevel} GirlStart={girlPos} CatStart={catPos} DoorsStartOpen={isDoorOpen}");
            sb.AppendLine($"Gates ({gates.Count}): {string.Join(", ", gates.Select(g => g ? g.name : "null"))}");
            sb.AppendLine($"Doors ({doors.Count}): {string.Join(", ", doors.Select(d => d ? d.name : "null"))}");
            sb.AppendLine($"Altars ({altars.Count}):");
            foreach (var altar in altars)
            {
                if (altar == null) { sb.AppendLine("  - null"); continue; }
                var ac = altar.GetComponent<AlterController>();
                var connected = GetPrivate<List<GameObject>>(ac, "connectedObjects") ?? new List<GameObject>();
                sb.AppendLine($"  - {altar.name} -> opens/closes: {string.Join(", ", connected.Select(c => c ? c.name : "null"))}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
