using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Escult.ProcGen
{
    /// <summary>
    /// Reproduces the hand-painted "juice" of the shipped levels on generated ones by LEARNING
    /// neighbourhood→tile rules from the real level prefabs (there are no RuleTiles in the project).
    ///
    /// For each layer role (wall face/edge/corner, wall-top overlay, pit rim, floor variety, wall
    /// shadow props) it records, over every reference cell, which tile the artist placed for a given
    /// 8-neighbour occupancy pattern, then majority-votes. Generated levels look up the same patterns.
    /// Unseen 8-patterns fall back to the coarser 4-neighbour vote, then to a base tile.
    /// </summary>
    public static class EscultAutoTiler
    {
        // The polished reference levels to learn from (deduped).
        static readonly string[] ReferencePrefabs =
        {
            "Assets/Prefabs/Levels/Level 3 New - put as lvl 7 Variant.prefab",
            "Assets/Prefabs/Levels/Level 2 New - put as level 6 Variant.prefab",
            "Assets/Prefabs/Levels/Level 4 New - put as lvl 8 Variant.prefab",
            "Assets/Prefabs/Levels/Level easy 1 - put as lvl 5 Variant.prefab",
            "Assets/Prefabs/Levels/ThrowCatOverAltar Variant.prefab",
            "Assets/Prefabs/Levels/Tutorial 1 - Or Level Variant.prefab",
            "Assets/Prefabs/Levels/Tutorial 2 - Or level Variant.prefab",
            "Assets/Prefabs/Levels/Tutorial 3 - Or level Variant.prefab",
            "Assets/Prefabs/Levels/Tutorial 4 - Or level Variant.prefab",
        };

        const string NONE = "";   // sentinel: "no tile here" (a real learned outcome, distinct from unseen)

        class Learner
        {
            // key(8-bit or 4-bit) -> tileName -> votes
            public Dictionary<int, Dictionary<string, int>> By8 = new Dictionary<int, Dictionary<string, int>>();
            public Dictionary<int, Dictionary<string, int>> By4 = new Dictionary<int, Dictionary<string, int>>();
            public void Vote(int m8, int m4, string tile)
            {
                Add(By8, m8, tile); Add(By4, m4, tile);
            }
            static void Add(Dictionary<int, Dictionary<string, int>> d, int k, string tile)
            {
                if (!d.TryGetValue(k, out var m)) d[k] = m = new Dictionary<string, int>();
                m[tile] = m.TryGetValue(tile, out var v) ? v + 1 : 1;
            }
            public string Resolve(int m8, int m4)
            {
                if (By8.TryGetValue(m8, out var a)) return Best(a);
                if (By4.TryGetValue(m4, out var b)) return Best(b);
                return null;   // unseen — caller uses its own base tile
            }
            static string Best(Dictionary<string, int> m)
            {
                string best = null; int bv = -1;
                foreach (var kv in m) if (kv.Value > bv) { bv = kv.Value; best = kv.Key; }
                return best;
            }
        }

        static bool learned;
        static readonly Learner wall = new Learner();
        static readonly Learner top = new Learner();     // wall-top overlay (keyed on wall neighbourhood)
        static readonly Learner hell = new Learner();    // pit rim (keyed on pit neighbourhood)
        static readonly Learner prop = new Learner();    // wall shadows on open cells (keyed on wall neighbourhood)
        static readonly Dictionary<string, TileBase> tileByName = new Dictionary<string, TileBase>();
        static readonly List<KeyValuePair<string, int>> groundWeights = new List<KeyValuePair<string, int>>();
        static int groundTotal;

        [MenuItem("Escult/Convert/Rebuild Auto-Tiler Cache")]
        public static void ClearCache()
        {
            learned = false;
            wall.By8.Clear(); wall.By4.Clear(); top.By8.Clear(); top.By4.Clear();
            hell.By8.Clear(); hell.By4.Clear(); prop.By8.Clear(); prop.By4.Clear();
            tileByName.Clear(); groundWeights.Clear(); groundTotal = 0;
            EnsureLearned();
            Debug.Log($"Escult auto-tiler: relearned. wall8={wall.By8.Count} top8={top.By8.Count} hell8={hell.By8.Count} prop8={prop.By8.Count} groundVariants={groundWeights.Count}");
        }

        // ---- neighbourhood masks (bit0 N, then clockwise; y up = north) ----
        static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] DY = { 1, 1, 0, -1, -1, -1, 0, 1 };
        static int Mask8(Func<int, int, bool> occ)
        {
            int m = 0;
            for (int i = 0; i < 8; i++) if (occ(DX[i], DY[i])) m |= 1 << i;
            return m;
        }
        static int To4(int m8) { return (m8 & 1) | ((m8 >> 1) & 2) | ((m8 >> 2) & 4) | ((m8 >> 3) & 8); }

        // ==================================================================
        //  learning
        // ==================================================================

        public static void EnsureLearned()
        {
            if (learned) return;
            learned = true;
            foreach (var path in ReferencePrefabs)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try { LearnFrom(root); }
                catch (Exception e) { Debug.LogWarning($"Escult auto-tiler: failed to learn from {path}: {e.Message}"); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        static void LearnFrom(GameObject root)
        {
            Tilemap walls = null, hellMap = null, ground = null;
            var tops = new List<Tilemap>();
            var props = new List<Tilemap>();
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                string n = tm.gameObject.name;
                if (n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 && n.IndexOf("Top", StringComparison.OrdinalIgnoreCase) >= 0) tops.Add(tm);
                else if (n.StartsWith("Props", StringComparison.OrdinalIgnoreCase)) props.Add(tm);
                else if (tm.gameObject.CompareTag(Tags.Wall) && n.IndexOf("Gate", StringComparison.OrdinalIgnoreCase) < 0) walls = tm;
                else if (tm.gameObject.CompareTag("Hell")) hellMap = tm;
                else if (n.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) >= 0) ground = tm;
            }
            if (walls == null) return;

            var wallSet = OccupiedSet(walls);
            var hellSet = hellMap != null ? OccupiedSet(hellMap) : new HashSet<Vector2Int>();
            RegisterTiles(walls); foreach (var t in tops) RegisterTiles(t);
            if (hellMap != null) RegisterTiles(hellMap);
            foreach (var pm in props) RegisterTiles(pm);
            if (ground != null) RegisterTiles(ground);

            // wall face/edge/corner
            foreach (var c in wallSet)
            {
                int m8 = Mask8((dx, dy) => wallSet.Contains(new Vector2Int(c.x + dx, c.y + dy)));
                var t = walls.GetTile((Vector3Int)new Vector3Int(c.x, c.y, 0));
                if (t != null) wall.Vote(m8, To4(m8), t.name);
            }
            // wall tops: for every wall cell, is there a top tile (any tops layer, topmost wins)?
            foreach (var c in wallSet)
            {
                int m8 = Mask8((dx, dy) => wallSet.Contains(new Vector2Int(c.x + dx, c.y + dy)));
                string topName = NONE;
                for (int i = tops.Count - 1; i >= 0; i--)
                {
                    var t = tops[i].GetTile(new Vector3Int(c.x, c.y, 0));
                    if (t != null) { topName = t.name; break; }
                }
                top.Vote(m8, To4(m8), topName);
            }
            // pit rim
            foreach (var c in hellSet)
            {
                int m8 = Mask8((dx, dy) => hellSet.Contains(new Vector2Int(c.x + dx, c.y + dy)));
                var t = hellMap.GetTile(new Vector3Int(c.x, c.y, 0));
                if (t != null) hell.Vote(m8, To4(m8), t.name);
            }
            // wall shadow props: on open (non-wall) cells, keyed by surrounding walls
            foreach (var pm in props)
                foreach (var p in pm.cellBounds.allPositionsWithin)
                {
                    var t = pm.GetTile(p);
                    if (t == null) continue;
                    var c = new Vector2Int(p.x, p.y);
                    if (wallSet.Contains(c)) continue;   // props sit on open cells (shadows cast onto floor/pit)
                    int m8 = Mask8((dx, dy) => wallSet.Contains(new Vector2Int(c.x + dx, c.y + dy)));
                    prop.Vote(m8, To4(m8), t.name);
                }
            // also teach "most open cells have no prop", so we don't smear shadows everywhere
            if (props.Count > 0)
            {
                var propCells = new HashSet<Vector2Int>();
                foreach (var pm in props) foreach (var p in pm.cellBounds.allPositionsWithin) if (pm.GetTile(p) != null) propCells.Add(new Vector2Int(p.x, p.y));
                // sample open cells adjacent to walls that got NO prop
                foreach (var c in wallSet)
                    foreach (var dir in new[] { new Vector2Int(0, -1), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(-1, 0) })
                    {
                        var o = c + dir;
                        if (wallSet.Contains(o) || propCells.Contains(o)) continue;
                        int m8 = Mask8((dx, dy) => wallSet.Contains(new Vector2Int(o.x + dx, o.y + dy)));
                        prop.Vote(m8, To4(m8), NONE);
                    }
            }
            // ground variety weights
            if (ground != null)
                foreach (var p in ground.cellBounds.allPositionsWithin)
                {
                    var t = ground.GetTile(p);
                    if (t != null) AddGroundWeight(t.name);
                }
        }

        static void AddGroundWeight(string name)
        {
            for (int i = 0; i < groundWeights.Count; i++)
                if (groundWeights[i].Key == name) { groundWeights[i] = new KeyValuePair<string, int>(name, groundWeights[i].Value + 1); groundTotal++; return; }
            groundWeights.Add(new KeyValuePair<string, int>(name, 1)); groundTotal++;
        }

        static HashSet<Vector2Int> OccupiedSet(Tilemap tm)
        {
            tm.CompressBounds();
            var s = new HashSet<Vector2Int>();
            foreach (var p in tm.cellBounds.allPositionsWithin)
                if (tm.HasTile(p)) s.Add(new Vector2Int(p.x, p.y));
            return s;
        }

        static void RegisterTiles(Tilemap tm)
        {
            tm.CompressBounds();
            foreach (var p in tm.cellBounds.allPositionsWithin)
            {
                var t = tm.GetTile(p);
                if (t != null && !tileByName.ContainsKey(t.name)) tileByName[t.name] = t;
            }
        }

        // ==================================================================
        //  applying
        // ==================================================================

        public class Stats { public int wallCells, wallHit, hellCells, hellHit, tops, props; }

        /// <summary>Decorate a freshly-built generated level so it matches the shipped polish.</summary>
        public static Stats Decorate(Topology t, GameObject gridGo, Tilemap ground, Tilemap hellMap, Tilemap walls, List<string> warnings)
        {
            EnsureLearned();
            var st = new Stats();
            if (tileByName.Count == 0) { warnings?.Add("auto-tiler learned nothing (reference prefabs missing?) — using flat tiles"); return st; }

            bool IsWall(int col, int row) => col >= 0 && col < t.W && row >= 0 && row < t.H && t.Terrain[t.Idx(col, row)] == 'W';
            bool IsPit(int col, int row) => col >= 0 && col < t.W && row >= 0 && row < t.H && t.Terrain[t.Idx(col, row)] == 'P';

            // ---- ground variety ----
            if (groundTotal > 0)
                for (int i = 0; i < t.N; i++)
                    if (t.Terrain[i] == 'G')
                    {
                        var tile = PickGround(t.Col(i), t.Row(i));
                        if (tile != null) ground.SetTile(EscultPrefabConverter.CellOf(t, i), tile);
                    }

            // ---- wall faces/edges/corners ----
            for (int i = 0; i < t.N; i++)
            {
                if (t.Terrain[i] != 'W') continue;
                int col = t.Col(i), row = t.Row(i);
                st.wallCells++;
                int m8 = Mask8((dx, dy) => IsWall(col + dx, row - dy));   // row-dy: y up = north
                string name = wall.Resolve(m8, To4(m8));
                if (name != null && tileByName.TryGetValue(name, out var tile)) { walls.SetTile(EscultPrefabConverter.CellOf(t, i), tile); st.wallHit++; }
            }

            // ---- pit rim ----
            for (int i = 0; i < t.N; i++)
            {
                if (t.Terrain[i] != 'P') continue;
                int col = t.Col(i), row = t.Row(i);
                st.hellCells++;
                int m8 = Mask8((dx, dy) => IsPit(col + dx, row - dy));
                string name = hell.Resolve(m8, To4(m8));
                if (name != null && tileByName.TryGetValue(name, out var tile)) { hellMap.SetTile(EscultPrefabConverter.CellOf(t, i), tile); st.hellHit++; }
            }

            // ---- wall tops overlay ----
            var topsMap = MakeLayer(gridGo, "WallTops", "Default", 4, 12);
            for (int i = 0; i < t.N; i++)
            {
                if (t.Terrain[i] != 'W') continue;
                int col = t.Col(i), row = t.Row(i);
                int m8 = Mask8((dx, dy) => IsWall(col + dx, row - dy));
                string name = top.Resolve(m8, To4(m8));
                if (!string.IsNullOrEmpty(name) && tileByName.TryGetValue(name, out var tile)) { topsMap.SetTile(EscultPrefabConverter.CellOf(t, i), tile); st.tops++; }
            }

            // ---- wall shadow props on open cells ----
            var propsMap = MakeLayer(gridGo, "Props", "Props", 0, 12);
            for (int i = 0; i < t.N; i++)
            {
                if (t.Terrain[i] == 'W') continue;
                int col = t.Col(i), row = t.Row(i);
                bool nearWall = false;
                for (int k = 0; k < 8 && !nearWall; k++) nearWall = IsWall(col + DX[k], row - DY[k]);
                if (!nearWall) continue;
                int m8 = Mask8((dx, dy) => IsWall(col + dx, row - dy));
                string name = prop.Resolve(m8, To4(m8));
                if (!string.IsNullOrEmpty(name) && tileByName.TryGetValue(name, out var tile)) { propsMap.SetTile(EscultPrefabConverter.CellOf(t, i), tile); st.props++; }
            }

            return st;
        }

        static TileBase PickGround(int col, int row)
        {
            if (groundTotal == 0) return null;
            // deterministic weighted pick per cell
            int h = (col * 73856093) ^ (row * 19349663);
            int r = (int)((uint)h % (uint)groundTotal);
            foreach (var kv in groundWeights) { if (r < kv.Value) return tileByName.TryGetValue(kv.Key, out var t) ? t : null; r -= kv.Value; }
            return tileByName.TryGetValue(groundWeights[0].Key, out var t0) ? t0 : null;
        }

        static Tilemap MakeLayer(GameObject gridGo, string name, string sortingLayer, int order, int unityLayer)
        {
            var host = gridGo.transform.Find("JuiceLayers");
            if (host == null)
            {
                var h = new GameObject("JuiceLayers"); h.transform.SetParent(gridGo.transform, false); host = h.transform;
            }
            var go = new GameObject(name);
            go.transform.SetParent(host, false);
            go.layer = unityLayer;
            var tm = go.AddComponent<Tilemap>();
            var tr = go.AddComponent<TilemapRenderer>();
            tr.sortingLayerName = sortingLayer;
            tr.sortingOrder = order;
            return tm;
        }
    }
}
