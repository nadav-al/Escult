using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Escult.ProcGen
{
    /// <summary>
    /// Two-way converter between the ProcGen Topology (ESN) and playable level prefabs.
    ///
    /// Forward  (Topology → prefab): builds the exact hierarchy the hand-made levels use —
    /// root (LevelManager) → "Grid - name" (Grid) → nested Ground/Hell/Walls/Gate tilemap
    /// prefabs + Alter/Door prefab instances — paints BasicTiles, wires every serialized
    /// reference, and saves under <see cref="GeneratedPrefabFolder"/>.
    ///
    /// Reverse  (prefab → Topology): reads any level prefab back into a Topology so it can
    /// be linted, solved, edited in the Studio and re-emitted as ESN artifacts.
    /// </summary>
    public static class EscultPrefabConverter
    {
        // ---------- project assets the converter builds from ----------
        // Terrain uses the shipped "Tiles Final" tileset (tile sheet.psd) — the same tiles the
        // released levels render — with a graceful fall back to the old BasicTiles placeholders.
        const string FloorTilePath  = "Assets/Prefabs/TileMaps/Tiles Final/tile sheet_5.asset";
        const string PitTilePath    = "Assets/Prefabs/TileMaps/Tiles Final/Hell Tile 1.asset";
        const string WallTilePath   = "Assets/Prefabs/TileMaps/Tiles Final/tile sheet_60.asset";
        const string GateTilePath   = "Assets/Prefabs/TileMaps/Tiles Final/tile sheet_42.asset";
        const string FloorTileFallback = "Assets/Sprites/BasicTiles/floor.asset";
        const string PitTileFallback   = "Assets/Sprites/BasicTiles/pit.asset";
        const string WallTileFallback  = "Assets/Sprites/BasicTiles/walls.asset";
        const string GateTileFallback  = "Assets/Sprites/BasicTiles/gate.asset";
        const string GroundPrefabPath = "Assets/Prefabs/TileMaps/Ground.prefab";
        const string HellPrefabPath   = "Assets/Prefabs/TileMaps/Hell.prefab";
        const string WallsPrefabPath  = "Assets/Prefabs/TileMaps/Collision - Walls.prefab";
        const string GatePrefabPath   = "Assets/Prefabs/TileMaps/Gate.prefab";
        const string AlterPrefabPath  = "Assets/Prefabs/Alter.prefab";
        const string DoorPrefabPath   = "Assets/Prefabs/Door.prefab";
        // Decorative "souls remaining" panel every shipped level carries (child of the Grid,
        // sorting layer "Props", fixed top-left placement). Faithfully replicated here.
        const string BestPanelSpritePath = "Assets/Sprites/Best/Asset 34.png";
        static readonly Vector3 BestPanelLocalPos = new Vector3(-2.55f, 4.55f, 0f);

        public const string GeneratedPrefabFolder = "Assets/Prefabs/Levels/Generated";
        public const float CellSize = 0.64f;   // matches every existing level Grid

        public class ConvertResult
        {
            public GameObject Root;             // temp object (forward) or inspected root (reverse)
            public string PrefabPath;
            public Topology Topology;
            public List<string> Warnings = new List<string>();
            public string Error;
            public bool Ok { get { return Error == null; } }
        }

        static T LoadRequired<T>(string path, ConvertResult res) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) res.Error = $"required asset missing: {path}";
            return a;
        }

        static TileBase LoadTile(string path, string fallback, ConvertResult res)
        {
            var a = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (a == null) a = AssetDatabase.LoadAssetAtPath<TileBase>(fallback);
            if (a == null) res.Error = $"required tile missing: {path} (and fallback {fallback})";
            return a;
        }

        // =====================================================================
        //  FORWARD:  Topology → prefab
        // =====================================================================

        /// <summary>ESN cell (col,row row0=top) → Unity tilemap cell, centered on the grid origin.</summary>
        public static Vector3Int CellOf(Topology t, int i)
        {
            return new Vector3Int(t.Col(i) - t.W / 2, (t.H - 1 - t.Row(i)) - t.H / 2, 0);
        }

        static Vector3 CenterOf(Topology t, int i)
        {
            var c = CellOf(t, i);
            return new Vector3((c.x + 0.5f) * CellSize, (c.y + 0.5f) * CellSize, 0f);
        }

        /// <summary>Build a playable level prefab from a topology. Returns the saved prefab path.</summary>
        public static ConvertResult BuildPrefab(Topology t, string prefabPath = null)
        {
            var res = new ConvertResult { Topology = t };

            var floorTile = LoadTile(FloorTilePath, FloorTileFallback, res);
            var pitTile   = LoadTile(PitTilePath, PitTileFallback, res);
            var wallTile  = LoadTile(WallTilePath, WallTileFallback, res);
            var gateTile  = LoadTile(GateTilePath, GateTileFallback, res);
            var groundPrefab = LoadRequired<GameObject>(GroundPrefabPath, res);
            var hellPrefab   = LoadRequired<GameObject>(HellPrefabPath, res);
            var wallsPrefab  = LoadRequired<GameObject>(WallsPrefabPath, res);
            var gatePrefab   = LoadRequired<GameObject>(GatePrefabPath, res);
            var alterPrefab  = LoadRequired<GameObject>(AlterPrefabPath, res);
            var doorPrefab   = LoadRequired<GameObject>(DoorPrefabPath, res);
            if (!res.Ok) return res;

            string safeName = Sanitize(t.Name);
            if (prefabPath == null)
                prefabPath = $"{GeneratedPrefabFolder}/{safeName}.prefab";

            GameObject root = null;
            try
            {
                root = new GameObject(safeName);
                root.SetActive(false);                       // GameManager activates the current level
                var lm = root.AddComponent<LevelManager>();

                var gridGo = new GameObject("Grid - " + safeName);
                gridGo.tag = Tags.Grid;
                var grid = gridGo.AddComponent<Grid>();
                grid.cellSize = new Vector3(CellSize, CellSize, 0f);
                gridGo.transform.SetParent(root.transform, false);

                Tilemap NewLayer(GameObject prefab)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.SetParent(gridGo.transform, false);
                    var tm = go.GetComponent<Tilemap>();
                    tm.ClearAllTiles();          // the source prefabs ship with baked example tiles
                    return tm;
                }

                var ground = NewLayer(groundPrefab);
                var hell   = NewLayer(hellPrefab);
                var walls  = NewLayer(wallsPrefab);

                AddBestPanel(gridGo, res);

                // ---- terrain (Topology.Terrain already has over=PIT gates resolved to 'P') ----
                for (int i = 0; i < t.N; i++)
                {
                    var cell = CellOf(t, i);
                    switch (t.Terrain[i])
                    {
                        case 'G': ground.SetTile(cell, floorTile); break;
                        case 'P': hell.SetTile(cell, pitTile); break;
                        case 'W': walls.SetTile(cell, wallTile); break;
                    }
                }

                // ---- auto-tile polish: replace the flat placeholders above with the same
                // wall/pit/floor variants + WallTops/Props decoration the shipped levels use,
                // learned from those levels themselves (there are no RuleTiles in this project) ----
                try
                {
                    EscultAutoTiler.Decorate(t, gridGo, ground, hell, walls, res.Warnings);
                }
                catch (Exception ex)
                {
                    res.Warnings.Add("auto-tiler polish failed (" + ex.Message + ") — kept flat placeholder tiles");
                }

                // ---- gates: one Gate prefab instance (tilemap + GateContoller) per id ----
                var gateGoById = new Dictionary<string, GameObject>();
                foreach (var g in t.Gates)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(gatePrefab);
                    go.name = "Gate " + g.Id;
                    go.transform.SetParent(gridGo.transform, false);
                    var tm = go.GetComponent<Tilemap>();
                    tm.ClearAllTiles();          // Gate.prefab also ships with baked tiles
                    foreach (var c in g.Cells) tm.SetTile(CellOf(t, c), gateTile);
                    // INITIAL_GATE_STATUS: true = tilemap active = blocking (ESN initial=CLOSED)
                    var so = new SerializedObject(go.GetComponent<GateContoller>());
                    so.FindProperty("INITIAL_GATE_STATUS").boolValue = g.InitialClosed;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    gateGoById[g.Id] = go;
                }

                // ---- altars ----
                var altarGos = new List<GameObject>();
                foreach (var a in t.Altars)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(alterPrefab);
                    go.name = "Alter " + a.Id;
                    go.transform.SetParent(gridGo.transform, false);
                    go.transform.position = CenterOf(t, a.Cell);
                    var connected = new List<GameObject>();
                    foreach (var target in a.Targets)
                    {
                        if (gateGoById.TryGetValue(target, out var gateGo)) connected.Add(gateGo);
                        else res.Warnings.Add($"altar {a.Id}: target '{target}' is not a gate (door wiring is retired in ruleset v1.1) — skipped");
                    }
                    var so = new SerializedObject(go.GetComponent<AlterController>());
                    var listProp = so.FindProperty("connectedObjects");
                    listProp.arraySize = connected.Count;
                    for (int k = 0; k < connected.Count; k++)
                        listProp.GetArrayElementAtIndex(k).objectReferenceValue = connected[k];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    altarGos.Add(go);
                }

                // ---- doors (always open per design law; never listed in LevelManager.doors) ----
                foreach (var d in t.Doors)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab);
                    go.name = t.Doors.Count == 1 ? "Door" : "Door " + d.Id;
                    go.transform.SetParent(gridGo.transform, false);
                    go.transform.position = CenterOf(t, d.Cell);
                    if (!d.InitialOpen)
                        res.Warnings.Add($"door {d.Id} marked CLOSED in ESN — the engine treats doors as always open (v1.1); prefab keeps it open");
                    var so = new SerializedObject(go.GetComponent<DoorController>());
                    so.FindProperty("openStatus").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // ---- LevelManager wiring ----
                var lmSo = new SerializedObject(lm);
                lmSo.FindProperty("girlPos").vector3Value = CenterOf(t, t.GirlSpawn);
                lmSo.FindProperty("catPos").vector3Value = t.CatInLevel ? CenterOf(t, t.CatSpawn) : CenterOf(t, t.GirlSpawn);
                lmSo.FindProperty("isCatInLevel").boolValue = t.CatInLevel;
                lmSo.FindProperty("hellTile").objectReferenceValue = pitTile;
                lmSo.FindProperty("groundMap").objectReferenceValue = ground;
                lmSo.FindProperty("hellMap").objectReferenceValue = hell;
                lmSo.FindProperty("isDoorOpen").boolValue = true;    // doors list stays empty by design — this must never matter
                var gatesProp = lmSo.FindProperty("gates");
                gatesProp.arraySize = t.Gates.Count;
                for (int k = 0; k < t.Gates.Count; k++)
                    gatesProp.GetArrayElementAtIndex(k).objectReferenceValue = gateGoById[t.Gates[k].Id];
                var altarsProp = lmSo.FindProperty("altars");
                altarsProp.arraySize = altarGos.Count;
                for (int k = 0; k < altarGos.Count; k++)
                    altarsProp.GetArrayElementAtIndex(k).objectReferenceValue = altarGos[k];
                lmSo.FindProperty("doors").arraySize = 0;
                lmSo.ApplyModifiedPropertiesWithoutUndo();

                Directory.CreateDirectory(Path.GetFullPath(Path.GetDirectoryName(prefabPath)));
                AssetDatabase.Refresh();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                if (!ok) { res.Error = "PrefabUtility.SaveAsPrefabAsset failed for " + prefabPath; return res; }
                res.PrefabPath = prefabPath;
                return res;
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Add the decorative "BestSoulsRemaining" panel every shipped level has.</summary>
        static void AddBestPanel(GameObject gridGo, ConvertResult res)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BestPanelSpritePath);
            if (sprite == null) { res.Warnings.Add("Best decoration sprite missing: " + BestPanelSpritePath + " — panel skipped"); return; }
            var go = new GameObject("BestSoulsRemaining");
            go.transform.SetParent(gridGo.transform, false);
            go.transform.localPosition = BestPanelLocalPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Props";
            sr.sortingOrder = 0;
        }

        static string Sanitize(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ' ? c : '_');
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "level" : s;
        }

        // =====================================================================
        //  SCENE INSERTION
        // =====================================================================

        /// <summary>
        /// Instantiate a level prefab into the open scene, parent it under the GameManager,
        /// inject the scene Girl/Cat into its LevelManager and append it to GameManager.levels.
        /// Marks the scene dirty; the caller/designer decides when to save.
        /// </summary>
        public static string InsertIntoOpenScene(string prefabPath, int levelIndex = -1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return "ERROR: prefab not found: " + prefabPath;

            var gm = UnityEngine.Object.FindObjectOfType<GameManager>(true);
            if (gm == null)
                return "ERROR: no GameManager in the open scene — open Assets/Scenes/SampleScene.unity first.";

            var gmSo = new SerializedObject(gm);
            var girl = gmSo.FindProperty("girl").objectReferenceValue;
            var cat = gmSo.FindProperty("cat").objectReferenceValue;
            if (girl == null || cat == null)
                return "ERROR: GameManager has no girl/cat references to inject.";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gm.transform);
            Undo.RegisterCreatedObjectUndo(instance, "Insert Escult level");
            instance.SetActive(false);

            var lm = instance.GetComponent<LevelManager>();
            var lmSo = new SerializedObject(lm);
            lmSo.FindProperty("girl").objectReferenceValue = girl;
            lmSo.FindProperty("cat").objectReferenceValue = cat;
            lmSo.ApplyModifiedPropertiesWithoutUndo();

            var levels = gmSo.FindProperty("levels");
            int at = (levelIndex < 0 || levelIndex > levels.arraySize) ? levels.arraySize : levelIndex;
            levels.InsertArrayElementAtIndex(at);
            levels.GetArrayElementAtIndex(at).objectReferenceValue = lm;
            gmSo.ApplyModifiedProperties();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            return $"OK: '{instance.name}' inserted as level index {at} of {levels.arraySize} (scene not saved yet — Ctrl+S to keep).";
        }

        // =====================================================================
        //  REVERSE:  prefab → Topology
        // =====================================================================

        /// <summary>Read a level prefab asset back into a Topology (ESN model).</summary>
        public static ConvertResult FromPrefabAsset(string prefabPath)
        {
            var res = new ConvertResult();
            if (!File.Exists(Path.GetFullPath(prefabPath))) { res.Error = "prefab not found: " + prefabPath; return res; }
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                FromInstance(root, res, Path.GetFileNameWithoutExtension(prefabPath));
                return res;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                res.Root = null;                              // contents are unloaded — never hand this out
            }
        }

        /// <summary>Read a level root (scene instance or loaded prefab contents) into a Topology.</summary>
        public static void FromInstance(GameObject root, ConvertResult res, string name)
        {
            res.Root = root;
            var lm = root.GetComponentInChildren<LevelManager>(true);
            if (lm == null) { res.Error = "no LevelManager found under " + root.name; return; }
            var lmSo = new SerializedObject(lm);

            var grid = root.GetComponentInChildren<Grid>(true);
            if (grid == null) { res.Error = "no Grid found under " + root.name; return; }

            // ---- classify tilemaps ----
            var groundMap = lmSo.FindProperty("groundMap").objectReferenceValue as Tilemap;
            var hellMap = lmSo.FindProperty("hellMap").objectReferenceValue as Tilemap;
            var gates = root.GetComponentsInChildren<GateContoller>(true)
                            .OrderBy(g => g.gameObject.name, StringComparer.OrdinalIgnoreCase).ToList();
            var gateMaps = new HashSet<Tilemap>(gates.Select(g => g.GetComponent<Tilemap>()).Where(m => m != null));
            Tilemap wallsMap = null;
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
            {
                if (tm == groundMap || tm == hellMap || gateMaps.Contains(tm)) continue;
                if (tm.gameObject.CompareTag(Tags.Wall)) { wallsMap = tm; break; }
            }
            if (wallsMap == null)   // fall back to name match (some hand-made levels use odd tags)
                wallsMap = root.GetComponentsInChildren<Tilemap>(true)
                               .FirstOrDefault(tm => tm != groundMap && tm != hellMap && !gateMaps.Contains(tm)
                                                     && tm.gameObject.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0);
            if (groundMap == null || hellMap == null)
            {
                foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
                {
                    if (gateMaps.Contains(tm) || tm == wallsMap) continue;
                    if (groundMap == null && tm.gameObject.name.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0) groundMap = tm;
                    if (hellMap == null && tm.gameObject.name.IndexOf("hell", StringComparison.OrdinalIgnoreCase) >= 0) hellMap = tm;
                }
            }
            if (groundMap == null) { res.Error = "could not identify the Ground tilemap"; return; }
            if (hellMap == null) { res.Error = "could not identify the Hell tilemap"; return; }
            if (wallsMap == null) res.Warnings.Add("no walls tilemap identified — level will have no '#' cells (V1 will fail)");

            var altars = root.GetComponentsInChildren<AlterController>(true)
                             .OrderBy(a => a.gameObject.name, StringComparer.OrdinalIgnoreCase).ToList();
            var doors = root.GetComponentsInChildren<DoorController>(true)
                            .OrderBy(d => d.gameObject.name, StringComparer.OrdinalIgnoreCase).ToList();

            // ---- bounds over everything that exists ----
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            void Grow(Vector3Int c)
            {
                minX = Math.Min(minX, c.x); maxX = Math.Max(maxX, c.x);
                minY = Math.Min(minY, c.y); maxY = Math.Max(maxY, c.y);
            }
            void GrowMap(Tilemap tm)
            {
                if (tm == null) return;
                tm.CompressBounds();
                foreach (var p in tm.cellBounds.allPositionsWithin)
                    if (tm.HasTile(p)) Grow(p);
            }
            GrowMap(groundMap); GrowMap(hellMap); GrowMap(wallsMap);
            foreach (var g in gateMaps) GrowMap(g);
            foreach (var a in altars) Grow(grid.WorldToCell(a.transform.position));
            foreach (var d in doors) Grow(grid.WorldToCell(d.transform.position));
            Vector3 girlPos = lmSo.FindProperty("girlPos").vector3Value;
            Vector3 catPos = lmSo.FindProperty("catPos").vector3Value;
            bool catInLevel = lmSo.FindProperty("isCatInLevel").boolValue;
            Grow(grid.WorldToCell(girlPos));
            if (catInLevel) Grow(grid.WorldToCell(catPos));

            if (minX > maxX) { res.Error = "level appears empty (no tiles, no interactables)"; return; }

            var t = new Topology
            {
                Name = Sanitize(name).Replace(' ', '_').ToLowerInvariant(),
                W = maxX - minX + 1,
                H = maxY - minY + 1,
            };
            if ((long)t.W * t.H > 80 * 60)
            {
                res.Error = $"level bounds {t.W}x{t.H} are unreasonably large for ESN — is this really a grid level?";
                return;
            }
            t.Terrain = new char[t.N];

            int IdxOfCell(Vector3Int c) { return t.Idx(c.x - minX, maxY - c.y); }

            // ---- terrain, precedence wall > pit > ground > void('.') ----
            int voidCells = 0, conflicts = 0;
            for (int r = 0; r < t.H; r++)
                for (int c = 0; c < t.W; c++)
                {
                    var cell = new Vector3Int(minX + c, maxY - r, 0);
                    bool w = wallsMap != null && wallsMap.HasTile(cell);
                    bool p = hellMap.HasTile(cell);
                    bool g = groundMap.HasTile(cell);
                    if (w) { t.Terrain[t.Idx(c, r)] = 'W'; if (p || g) conflicts++; }
                    else if (p) { t.Terrain[t.Idx(c, r)] = 'P'; if (g) conflicts++; }
                    else if (g) t.Terrain[t.Idx(c, r)] = 'G';
                    else { t.Terrain[t.Idx(c, r)] = 'G'; voidCells++; }   // void = walkable, throw-transparent ≈ ground
                }
            if (voidCells > 0) res.Warnings.Add($"{voidCells} empty cell(s) inside bounds mapped to ground '.' (engine treats void as walkable) — review them");
            if (conflicts > 0) res.Warnings.Add($"{conflicts} cell(s) had overlapping terrain tiles; used precedence wall > pit > ground");

            // ---- gates (split mixed over-pit/over-ground gate objects into co-wired ids) ----
            string letters = "ABDEFGHIJKLMNPQRSTUVWYZ";   // C, O, X are reserved glyphs
            int nextLetter = 0;
            var idsByController = new Dictionary<GateContoller, List<string>>();
            var claimedGateCells = new HashSet<int>();     // one gate per cell in ESN
            foreach (var gc in gates)
            {
                var tm = gc.GetComponent<Tilemap>();
                if (tm == null) { res.Warnings.Add($"gate '{gc.gameObject.name}' has no Tilemap — skipped"); continue; }
                var overPit = new List<int>(); var overGround = new List<int>();
                int stolen = 0;
                tm.CompressBounds();
                foreach (var p in tm.cellBounds.allPositionsWithin)
                {
                    if (!tm.HasTile(p)) continue;
                    int idx = IdxOfCell(p);
                    if (!claimedGateCells.Add(idx)) { stolen++; continue; }
                    (hellMap.HasTile(p) ? overPit : overGround).Add(idx);
                }
                if (stolen > 0)
                    res.Warnings.Add($"gate '{gc.gameObject.name}': {stolen} cell(s) already claimed by another gate — ESN allows one gate per cell, dropped");
                if (overPit.Count + overGround.Count == 0) { res.Warnings.Add($"gate '{gc.gameObject.name}' has no tiles — skipped"); continue; }
                bool initialClosed = new SerializedObject(gc).FindProperty("INITIAL_GATE_STATUS").boolValue;

                var ids = new List<string>();
                foreach (var (cells, isPit) in new[] { (overGround, false), (overPit, true) })
                {
                    if (cells.Count == 0) continue;
                    if (nextLetter >= letters.Length) { res.Error = "more than 23 gate segments — out of ESN gate letters"; return; }
                    string id = letters[nextLetter++].ToString();
                    t.Gates.Add(new Gate { Id = id, Cells = cells, InitialClosed = initialClosed, OverPit = isPit });
                    ids.Add(id);
                }
                if (ids.Count > 1)
                    res.Warnings.Add($"gate '{gc.gameObject.name}' spans pit AND ground — split into co-wired ESN gates {string.Join("+", ids)}");
                idsByController[gc] = ids;
            }
            // gate cells override terrain: ESN wants the *underlying* terrain there
            foreach (var g in t.Gates)
                foreach (var c in g.Cells)
                    t.Terrain[c] = g.OverPit ? 'P' : 'G';

            // ---- altars ----
            if (altars.Count > 9) { res.Error = $"{altars.Count} altars — ESN supports at most 9"; return; }
            for (int i = 0; i < altars.Count; i++)
            {
                var a = altars[i];
                var altar = new Altar { Id = (i + 1).ToString(), Cell = IdxOfCell(grid.WorldToCell(a.transform.position)) };
                var so = new SerializedObject(a);
                var list = so.FindProperty("connectedObjects");
                for (int k = 0; k < list.arraySize; k++)
                {
                    var obj = list.GetArrayElementAtIndex(k).objectReferenceValue as GameObject;
                    if (obj == null) continue;
                    var gc = obj.GetComponent<GateContoller>();
                    if (gc != null && idsByController.TryGetValue(gc, out var ids)) altar.Targets.AddRange(ids);
                    else if (obj.GetComponent<DoorController>() != null)
                        res.Warnings.Add($"altar '{a.gameObject.name}' is wired to a door — retired in ruleset v1.1, dropped from ESN");
                    else res.Warnings.Add($"altar '{a.gameObject.name}' wired to unrecognized object '{obj.name}' — dropped");
                }
                t.Altars.Add(altar);
            }

            // ---- doors ----
            for (int i = 0; i < doors.Count; i++)
                t.Doors.Add(new Door { Id = "d" + (i + 1), Cell = IdxOfCell(grid.WorldToCell(doors[i].transform.position)), InitialOpen = true });
            if (doors.Count == 0) res.Warnings.Add("no door found — the resulting ESN cannot be won (parser will flag it)");

            // ---- spawns ----
            t.GirlSpawn = IdxOfCell(grid.WorldToCell(girlPos));
            t.CatSpawn = catInLevel ? IdxOfCell(grid.WorldToCell(catPos)) : -1;

            // interactable cells must sit on ground in ESN
            foreach (var a in t.Altars) t.Terrain[a.Cell] = 'G';
            foreach (var d in t.Doors) t.Terrain[d.Cell] = 'G';
            t.Terrain[t.GirlSpawn] = 'G';
            if (t.CatSpawn >= 0) t.Terrain[t.CatSpawn] = 'G';

            t.BuildLookups();
            res.Topology = t;
        }

        /// <summary>Convert a prefab to ESN text (canvas + legend).</summary>
        public static ConvertResult PrefabToEsn(string prefabPath)
        {
            var res = FromPrefabAsset(prefabPath);
            return res;
        }

        // =====================================================================
        //  Menu items
        // =====================================================================

        [MenuItem("Escult/Convert/ESN File → Level Prefab...")]
        static void MenuEsnToPrefab()
        {
            string start = Directory.Exists(EscultCli.LevelsRoot) ? EscultCli.LevelsRoot : EscultCli.ProjectRoot;
            string path = EditorUtility.OpenFilePanel("Choose an .esn.txt level", start, "txt");
            if (string.IsNullOrEmpty(path)) return;
            string name = Path.GetFileName(path);
            name = name.EndsWith(".esn.txt") ? name.Substring(0, name.Length - ".esn.txt".Length) : Path.GetFileNameWithoutExtension(path);
            var parse = EsnParser.Parse(File.ReadAllText(path), name);
            if (parse.HasErrors || parse.Topology == null)
            {
                EditorUtility.DisplayDialog("Escult", "ESN does not parse:\n" + string.Join("\n", parse.Lints), "OK");
                return;
            }
            var res = BuildPrefab(parse.Topology);
            string msg = res.Ok ? "Prefab saved: " + res.PrefabPath : "FAILED: " + res.Error;
            if (res.Warnings.Count > 0) msg += "\n\n" + string.Join("\n", res.Warnings);
            Debug.Log("Escult converter: " + msg);
            EditorUtility.DisplayDialog("Escult", msg, "OK");
        }

        [MenuItem("Escult/Convert/Level Prefab → ESN Artifacts...")]
        static void MenuPrefabToEsn()
        {
            string abs = EditorUtility.OpenFilePanel("Choose a level prefab", Path.GetFullPath("Assets/Prefabs/Levels"), "prefab");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs.Replace('\\', '/'));
            if (string.IsNullOrEmpty(rel)) { EditorUtility.DisplayDialog("Escult", "Pick a prefab inside this project.", "OK"); return; }

            var res = FromPrefabAsset(rel);
            if (!res.Ok) { EditorUtility.DisplayDialog("Escult", "FAILED: " + res.Error, "OK"); return; }

            string esn = EsnParser.ToEsn(res.Topology);
            string folder = Path.Combine(EscultCli.LevelsRoot, res.Topology.Name);
            Directory.CreateDirectory(folder);
            string esnPath = Path.Combine(folder, res.Topology.Name + ".esn.txt");
            File.WriteAllText(esnPath, esn, new UTF8Encoding(false));
            string report = EscultCli.Check(esnPath);          // lints, solves, emits all artifacts next to it
            string msg = $"ESN + artifacts written to {folder}\n\n"
                       + (res.Warnings.Count > 0 ? "Conversion warnings:\n- " + string.Join("\n- ", res.Warnings) + "\n\n" : "")
                       + report;
            Debug.Log("Escult converter: " + msg);
            EditorUtility.DisplayDialog("Escult", msg.Length > 900 ? msg.Substring(0, 900) + "\n… (full report in Console)" : msg, "OK");
        }

        [MenuItem("Escult/Convert/Insert Level Prefab Into Open Scene...")]
        static void MenuInsertIntoScene()
        {
            string abs = EditorUtility.OpenFilePanel("Choose a level prefab", Path.GetFullPath(GeneratedPrefabFolder), "prefab");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs.Replace('\\', '/'));
            if (string.IsNullOrEmpty(rel)) { EditorUtility.DisplayDialog("Escult", "Pick a prefab inside this project.", "OK"); return; }
            string msg = InsertIntoOpenScene(rel);
            Debug.Log("Escult converter: " + msg);
            EditorUtility.DisplayDialog("Escult", msg, "OK");
        }
    }
}
