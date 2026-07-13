using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Escult.ProcGen
{
    /// <summary>
    /// Escult Level Browser — dashboard over every ProcGen level artifact folder and over the
    /// levels wired into the open scene's GameManager. One click to open a level in the Studio,
    /// build its prefab, insert it into the scene, reorder the roster, or enter play mode
    /// starting at any level.
    /// </summary>
    public class EscultLevelBrowser : EditorWindow
    {
        class LevelRow
        {
            public string Name, EsnPath, Folder, Tier;
            public bool HasReport, Valid;
            public int MinCost = -1, SoulBudget = -1;
            public string PrefabPath;               // null when not built yet
        }

        ScrollView listView;
        VisualElement sceneSection;
        Label countLabel;

        [MenuItem("Escult/Level Browser")]
        static void Open()
        {
            var win = GetWindow<EscultLevelBrowser>("Escult Levels");
            win.minSize = new Vector2(520, 300);
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.Clear();

            var tb = new Toolbar();
            tb.Add(new ToolbarButton(Rescan) { text = "⟳ Rescan" });
            tb.Add(new ToolbarButton(() => EscultStudioWindow.OpenWindow()) { text = "Open Studio" });
            tb.Add(new ToolbarButton(() =>
            {
                string report = EscultCli.CheckAll();
                Debug.Log(report);
                Rescan();
                EditorUtility.DisplayDialog("Escult", "Validated all levels — full report in the Console.", "OK");
            }) { text = "Validate all" });
            tb.Add(new ToolbarSpacer());
            countLabel = new Label("") { style = { unityTextAlign = TextAnchor.MiddleLeft } };
            tb.Add(countLabel);
            root.Add(tb);

            var split = new ScrollView { style = { flexGrow = 1 } };
            listView = new ScrollView { style = { flexGrow = 0 } };
            split.Add(SectionHeader("ProcGen levels  (" + "Assets/ProcGen/Levels" + ")"));
            split.Add(listView);
            sceneSection = new VisualElement();
            split.Add(SectionHeader("Scene roster  (GameManager.levels in the open scene)"));
            split.Add(sceneSection);
            root.Add(split);

            Rescan();
        }

        static Label SectionHeader(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold, fontSize = 13,
                    paddingLeft = 6, paddingTop = 8, paddingBottom = 4,
                }
            };
        }

        void Rescan()
        {
            var rows = Scan();
            countLabel.text = $"{rows.Count} level(s), {rows.Count(r => r.Valid)} valid, {rows.Count(r => r.PrefabPath != null)} with prefab";

            listView.Clear();
            if (rows.Count == 0)
                listView.Add(new Label("no *.esn.txt under Assets/ProcGen/Levels yet — create one in the Studio")
                { style = { paddingLeft = 10, opacity = 0.7f } });
            foreach (var row in rows) listView.Add(BuildRow(row));

            RebuildSceneSection();
        }

        List<LevelRow> Scan()
        {
            var rows = new List<LevelRow>();
            if (!Directory.Exists(EscultCli.LevelsRoot)) return rows;
            foreach (var esn in Directory.GetFiles(EscultCli.LevelsRoot, "*.esn.txt", SearchOption.AllDirectories)
                                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                string fn = Path.GetFileName(esn);
                var row = new LevelRow
                {
                    Name = fn.Substring(0, fn.Length - ".esn.txt".Length),
                    EsnPath = esn,
                    Folder = Path.GetDirectoryName(esn),
                };
                string baseName = Path.Combine(row.Folder, row.Name);
                if (File.Exists(baseName + ".report.json"))
                {
                    row.HasReport = true;
                    string rep = File.ReadAllText(baseName + ".report.json");
                    row.Valid = Regex.IsMatch(rep, "\"valid\":\\s*true");
                    var tier = Regex.Match(rep, "\"tier\":\\s*\"([^\"]+)\"");
                    row.Tier = tier.Success ? tier.Groups[1].Value : "?";
                }
                if (File.Exists(baseName + ".solution.json"))
                {
                    var mc = Regex.Match(File.ReadAllText(baseName + ".solution.json"), "\"minCost\":\\s*(-?\\d+)");
                    if (mc.Success) row.MinCost = int.Parse(mc.Groups[1].Value);
                }
                if (File.Exists(baseName + ".level.json"))
                {
                    var sb = Regex.Match(File.ReadAllText(baseName + ".level.json"), "\"soulBudget\":\\s*(\\d+)");
                    if (sb.Success) row.SoulBudget = int.Parse(sb.Groups[1].Value);
                }
                string prefab = $"{EscultPrefabConverter.GeneratedPrefabFolder}/{row.Name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) != null) row.PrefabPath = prefab;
                rows.Add(row);
            }
            return rows;
        }

        VisualElement BuildRow(LevelRow row)
        {
            var e = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap,
                    paddingLeft = 8, paddingTop = 3, paddingBottom = 3,
                    borderBottomWidth = 1, borderBottomColor = new Color(0, 0, 0, 0.25f),
                }
            };

            string status = !row.HasReport ? "—" : row.Valid ? "✓" : "✗";
            var statusLabel = new Label(status)
            {
                style =
                {
                    width = 16, unityFontStyleAndWeight = FontStyle.Bold,
                    color = !row.HasReport ? Color.gray : row.Valid ? new Color(0.4f, 0.85f, 0.4f) : new Color(0.95f, 0.4f, 0.4f),
                }
            };
            e.Add(statusLabel);
            e.Add(new Label(row.Name) { style = { minWidth = 150, unityFontStyleAndWeight = FontStyle.Bold } });
            string info = row.HasReport
                ? $"tier {row.Tier ?? "?"}" + (row.MinCost >= 0 ? $" · cost {row.MinCost}" + (row.SoulBudget >= 0 ? $" · slack {row.SoulBudget - row.MinCost}" : "") : "")
                : "not validated yet";
            if (row.PrefabPath != null) info += " · prefab ✓";
            e.Add(new Label(info) { style = { minWidth = 170, opacity = 0.8f } });

            e.Add(new Button(() => EscultStudioWindow.OpenWith(row.EsnPath)) { text = "Studio" });
            e.Add(new Button(() =>
            {
                var parse = EsnParser.Parse(File.ReadAllText(row.EsnPath), row.Name);
                if (parse.HasErrors || parse.Topology == null)
                {
                    EditorUtility.DisplayDialog("Escult", "ESN does not parse:\n" + string.Join("\n", parse.Lints.Select(l => l.ToString())), "OK");
                    return;
                }
                var res = EscultPrefabConverter.BuildPrefab(parse.Topology);
                Debug.Log(res.Ok ? "Escult: prefab built at " + res.PrefabPath : "Escult: FAILED — " + res.Error);
                Rescan();
            }) { text = row.PrefabPath == null ? "Build prefab" : "Rebuild prefab" });
            if (row.PrefabPath != null)
            {
                e.Add(new Button(() =>
                {
                    string msg = EscultPrefabConverter.InsertIntoOpenScene(row.PrefabPath);
                    Debug.Log("Escult: " + msg);
                    EditorUtility.DisplayDialog("Escult", msg, "OK");
                    RebuildSceneSection();
                }) { text = "Insert into scene" });
            }
            e.Add(new Button(() => EditorUtility.RevealInFinder(row.EsnPath)) { text = "Files" });
            return e;
        }

        // ------------------------------------------------------------------
        //  scene roster
        // ------------------------------------------------------------------

        void RebuildSceneSection()
        {
            sceneSection.Clear();
            var gm = FindObjectOfType<GameManager>(true);
            if (gm == null)
            {
                sceneSection.Add(new Label("no GameManager in the open scene — open SampleScene to manage the roster")
                { style = { paddingLeft = 10, opacity = 0.7f } });
                return;
            }
            var so = new SerializedObject(gm);
            var levels = so.FindProperty("levels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                int index = i;
                var lm = levels.GetArrayElementAtIndex(i).objectReferenceValue as LevelManager;
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row, alignItems = Align.Center,
                        paddingLeft = 8, paddingTop = 2, paddingBottom = 2,
                    }
                };
                row.Add(new Label($"{i}") { style = { width = 22, opacity = 0.7f } });
                row.Add(new Label(lm != null ? lm.gameObject.name : "(missing!)")
                { style = { minWidth = 200, unityFontStyleAndWeight = FontStyle.Bold, color = lm != null ? new StyleColor(StyleKeyword.Null) : (StyleColor)new Color(0.95f, 0.4f, 0.4f) } });

                row.Add(new Button(() => Reorder(index, index - 1)) { text = "↑" });
                row.Add(new Button(() => Reorder(index, index + 1)) { text = "↓" });
                row.Add(new Button(() =>
                {
                    if (lm != null) { Selection.activeGameObject = lm.gameObject; EditorGUIUtility.PingObject(lm.gameObject); }
                }) { text = "Select" });
                row.Add(new Button(() =>
                {
                    PlayerPrefs.SetInt("EscultDebugStartLevel", index);
                    PlayerPrefs.Save();
                    EditorApplication.EnterPlaymode();
                }) { text = "▶ Play from here", tooltip = "Enter play mode starting at this level (one-shot, editor only)" });
                sceneSection.Add(row);
            }
            if (levels.arraySize == 0)
                sceneSection.Add(new Label("GameManager.levels is empty") { style = { paddingLeft = 10, opacity = 0.7f } });
        }

        void Reorder(int from, int to)
        {
            var gm = FindObjectOfType<GameManager>(true);
            if (gm == null) return;
            var so = new SerializedObject(gm);
            var levels = so.FindProperty("levels");
            if (from < 0 || from >= levels.arraySize || to < 0 || to >= levels.arraySize) return;
            levels.MoveArrayElement(from, to);
            so.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gm.gameObject.scene);
            RebuildSceneSection();
        }
    }
}
