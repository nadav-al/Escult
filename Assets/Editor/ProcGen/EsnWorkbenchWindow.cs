using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Escult.ProcGen
{
    /// <summary>
    /// Designer-facing ESN editor: edit the sketch as text, see the grid live,
    /// solve/validate with one click, emit the four pipeline artifacts.
    /// </summary>
    public class EsnWorkbenchWindow : EditorWindow
    {
        string esnText =
            "###########\n" +
            "#...~~~...#\n" +
            "#@..~A~..X#\n" +
            "#C.1~~~..2#\n" +
            "#...~~~...#\n" +
            "###########\n" +
            "\n" +
            "souls: 9\n" +
            "1 -> A, X\n" +
            "2 -> A\n" +
            "A: initial=CLOSED over=PIT\n" +
            "decoys: 2\n";

        string levelName = "new_level";
        int tierIndex = 0;
        static readonly string[] TierOptions = { "(none)", "tutorial", "easy", "medium", "hard", "extreme" };

        string resultText = "";
        ParseResult lastParse;
        Vector2 textScroll, resultScroll;

        [MenuItem("Escult/ESN Workbench")]
        static void Open()
        {
            var win = GetWindow<EsnWorkbenchWindow>("ESN Workbench");
            win.minSize = new Vector2(760, 500);
        }

        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();

            // ---------- left: text + actions ----------
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.45f));
            EditorGUILayout.BeginHorizontal();
            levelName = EditorGUILayout.TextField("Level name", levelName);
            tierIndex = EditorGUILayout.Popup(tierIndex, TierOptions, GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load .esn.txt..."))
            {
                string start = Directory.Exists(EscultCli.LevelsRoot) ? EscultCli.LevelsRoot : EscultCli.ProjectRoot;
                string path = EditorUtility.OpenFilePanel("Load ESN", start, "txt");
                if (!string.IsNullOrEmpty(path))
                {
                    esnText = File.ReadAllText(path);
                    string fn = Path.GetFileName(path);
                    levelName = fn.EndsWith(".esn.txt") ? fn.Substring(0, fn.Length - ".esn.txt".Length) : Path.GetFileNameWithoutExtension(path);
                    lastParse = null; resultText = "";
                }
            }
            if (GUILayout.Button("Lint")) DoParse();
            if (GUILayout.Button("Solve && Validate")) DoValidate();
            if (GUILayout.Button("Emit Artifacts")) DoEmit();
            EditorGUILayout.EndHorizontal();

            textScroll = EditorGUILayout.BeginScrollView(textScroll, GUILayout.ExpandHeight(true));
            var mono = new GUIStyle(EditorStyles.textArea) { font = EditorStyles.miniFont, fontSize = 13 };
            mono.font = Font.CreateDynamicFontFromOSFont("Consolas", 13) ?? mono.font;
            EditorGUI.BeginChangeCheck();
            esnText = EditorGUILayout.TextArea(esnText, mono, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck()) lastParse = null;   // preview refresh on next repaint
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // ---------- right: preview + results ----------
            EditorGUILayout.BeginVertical();
            if (lastParse == null) lastParse = EsnParser.Parse(esnText, levelName);
            DrawPreview();
            resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.ExpandHeight(true));
            EditorGUILayout.TextArea(resultText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        void DoParse()
        {
            lastParse = EsnParser.Parse(esnText, levelName);
            var sb = new StringBuilder();
            sb.AppendLine(lastParse.HasErrors ? "PARSE FAILED" : "Parse OK.");
            foreach (var l in lastParse.Lints) sb.AppendLine("- " + l);
            if (lastParse.Topology != null)
                sb.AppendLine($"\n{lastParse.Topology.W}x{lastParse.Topology.H}, gates {lastParse.Topology.Gates.Count}, altars {lastParse.Topology.Altars.Count}, doors {lastParse.Topology.Doors.Count}, souls {lastParse.Topology.SoulBudget}");
            resultText = sb.ToString();
        }

        string RequestedTier() { return tierIndex == 0 ? null : TierOptions[tierIndex]; }

        void DoValidate()
        {
            resultText = EscultCli.CheckText(esnText, levelName, RequestedTier());
            lastParse = EsnParser.Parse(esnText, levelName);
        }

        void DoEmit()
        {
            lastParse = EsnParser.Parse(esnText, levelName);
            if (lastParse.HasErrors || lastParse.Topology == null) { DoParse(); return; }
            var rep = EscultValidator.Run(lastParse.Topology, lastParse.Lints, RequestedTier());
            string folder = Path.Combine(EscultCli.LevelsRoot, levelName);
            EscultArtifacts.Emit(folder, esnText, lastParse.Topology, rep);
            resultText = $"Artifacts written to {folder}\n\n" + EscultValidator.ToMarkdown(rep, lastParse.Topology);
        }

        void DrawPreview()
        {
            var t = lastParse != null ? lastParse.Topology : null;
            float maxW = position.width * 0.52f, maxH = position.height * 0.5f;
            if (t == null)
            {
                EditorGUILayout.HelpBox("Canvas does not parse yet:\n" + LintSummary(), MessageType.Warning);
                return;
            }
            float cs = Mathf.Clamp(Mathf.Min(maxW / t.W, maxH / t.H), 8, 30);
            Rect area = GUILayoutUtility.GetRect(t.W * cs + 8, t.H * cs + 8);
            float ox = area.x + 4, oy = area.y + 4;

            Color wall = new Color(0.24f, 0.24f, 0.29f);
            Color ground = new Color(0.79f, 0.75f, 0.68f);
            Color pit = new Color(0.36f, 0.08f, 0.13f);
            Color gateC = new Color(0.42f, 0.31f, 0.63f);
            Color gateO = new Color(0.66f, 0.56f, 0.84f);
            Color altarC = new Color(0.85f, 0.64f, 0.25f);
            Color doorC = new Color(0.54f, 0.35f, 0.24f);
            Color doorO = new Color(0.50f, 0.65f, 0.42f);

            var label = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            label.normal.textColor = Color.white;

            for (int i = 0; i < t.N; i++)
            {
                var r = new Rect(ox + t.Col(i) * cs, oy + t.Row(i) * cs, cs - 1, cs - 1);
                char ter = t.Terrain[i];
                EditorGUI.DrawRect(r, ter == 'W' ? wall : ter == 'P' ? pit : ground);
                string glyph = null; Color? over = null;
                if (t.GateAt[i] >= 0) { var g = t.Gates[t.GateAt[i]]; over = g.InitialClosed ? gateC : gateO; glyph = g.Id; }
                else if (t.AltarAt[i] >= 0) { over = altarC; glyph = t.Altars[t.AltarAt[i]].Id; }
                else if (t.DoorAt[i] >= 0) { var d = t.Doors[t.DoorAt[i]]; over = d.InitialOpen ? doorO : doorC; glyph = d.InitialOpen ? "O" : "X"; }
                else if (i == t.GirlSpawn) { over = new Color(0.88f, 0.48f, 0.60f); glyph = "@"; }
                else if (i == t.CatSpawn) { over = new Color(0.55f, 0.60f, 0.68f); glyph = "C"; }
                if (over.HasValue)
                {
                    EditorGUI.DrawRect(new Rect(r.x + 1, r.y + 1, r.width - 2, r.height - 2), over.Value);
                    if (cs >= 12) GUI.Label(r, glyph, label);
                }
            }
        }

        string LintSummary()
        {
            if (lastParse == null) return "";
            var sb = new StringBuilder();
            foreach (var l in lastParse.Lints) if (l.IsError) sb.AppendLine(l.Text);
            return sb.ToString();
        }
    }
}
