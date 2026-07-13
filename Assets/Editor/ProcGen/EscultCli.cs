using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Escult.ProcGen
{
    /// <summary>
    /// Headless entry points for the ProcGen toolchain. Designed to be called from
    /// unity-bridge scratch scripts (AI agents) and from menu items (humans).
    ///
    /// Typical agent loop:
    ///   1. write/edit  Assets/ProcGen/Levels/&lt;name&gt;/&lt;name&gt;.esn.txt
    ///   2. BridgeScratch.Run() { return Escult.ProcGen.EscultCli.CheckAll(); }
    ///   3. read the returned markdown / the emitted .report.json, fix, repeat
    /// </summary>
    public static class EscultCli
    {
        public static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        /// <summary>Levels live under Assets so artifacts are visible/importable in the editor.</summary>
        public static string LevelsRoot
        {
            get { return Path.Combine(Application.dataPath, "ProcGen", "Levels"); }
        }

        /// <summary>Import freshly written artifact files when they landed inside Assets/.</summary>
        public static void RefreshIfInsideAssets(string folder)
        {
            string full = Path.GetFullPath(folder);
            if (full.StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase))
                AssetDatabase.Refresh();
        }

        /// <summary>Validate + score one ESN file; emit all artifacts next to it. Returns a markdown summary.</summary>
        public static string Check(string esnPath, string requestedTier = null)
        {
            if (!Path.IsPathRooted(esnPath)) esnPath = Path.Combine(ProjectRoot, esnPath);
            if (!File.Exists(esnPath)) return $"ERROR: file not found: {esnPath}";

            string name = Path.GetFileName(esnPath);
            if (name.EndsWith(".esn.txt")) name = name.Substring(0, name.Length - ".esn.txt".Length);
            else name = Path.GetFileNameWithoutExtension(esnPath);

            string esnText = File.ReadAllText(esnPath);
            var parse = EsnParser.Parse(esnText, name);
            if (parse.HasErrors || parse.Topology == null)
            {
                var sb0 = new StringBuilder();
                sb0.AppendLine($"## {name} — PARSE FAILED");
                foreach (var l in parse.Lints) sb0.AppendLine("- " + l);
                return sb0.ToString();
            }

            var rep = EscultValidator.Run(parse.Topology, parse.Lints, requestedTier);
            EscultArtifacts.Emit(Path.GetDirectoryName(esnPath), esnText, parse.Topology, rep);
            RefreshIfInsideAssets(Path.GetDirectoryName(esnPath));
            return EscultValidator.ToMarkdown(rep, parse.Topology);
        }

        /// <summary>Validate + score raw ESN text without touching disk. Returns a markdown summary.</summary>
        public static string CheckText(string esnText, string name = "adhoc", string requestedTier = null)
        {
            var parse = EsnParser.Parse(esnText, name);
            if (parse.HasErrors || parse.Topology == null)
            {
                var sb0 = new StringBuilder();
                sb0.AppendLine($"## {name} — PARSE FAILED");
                foreach (var l in parse.Lints) sb0.AppendLine("- " + l);
                return sb0.ToString();
            }
            var rep = EscultValidator.Run(parse.Topology, parse.Lints, requestedTier);
            return EscultValidator.ToMarkdown(rep, parse.Topology);
        }

        /// <summary>Validate every *.esn.txt under Assets/ProcGen/Levels (recursive). Returns a combined report.</summary>
        public static string CheckAll()
        {
            if (!Directory.Exists(LevelsRoot))
                return $"No levels folder yet ({LevelsRoot}). Create Assets/ProcGen/Levels/<name>/<name>.esn.txt first.";

            var files = Directory.GetFiles(LevelsRoot, "*.esn.txt", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            if (files.Length == 0)
                return $"No *.esn.txt files under {LevelsRoot}.";

            var sb = new StringBuilder();
            sb.AppendLine($"# Escult level validation — {files.Length} level(s)");
            sb.AppendLine();
            int valid = 0;
            foreach (var f in files)
            {
                string md = Check(f);
                if (md.Contains("— VALID")) valid++;
                sb.AppendLine(md);
                sb.AppendLine("---");
            }
            sb.Insert(0, $"**{valid}/{files.Length} valid**\n\n");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // menu items for humans
        // ------------------------------------------------------------------
        [MenuItem("Escult/Validate All ESN Levels")]
        static void MenuCheckAll()
        {
            string report = CheckAll();
            string outPath = Path.Combine(LevelsRoot, "_validation_report.md");
            Directory.CreateDirectory(LevelsRoot);
            File.WriteAllText(outPath, report, new UTF8Encoding(false));
            RefreshIfInsideAssets(LevelsRoot);
            Debug.Log(report);
            Debug.Log($"Escult: full validation report written to {outPath}");
        }

        [MenuItem("Escult/Validate ESN File...")]
        static void MenuCheckFile()
        {
            string start = Directory.Exists(LevelsRoot) ? LevelsRoot : ProjectRoot;
            string path = EditorUtility.OpenFilePanel("Choose an .esn.txt level", start, "txt");
            if (string.IsNullOrEmpty(path)) return;
            Debug.Log(Check(path));
        }
    }
}
