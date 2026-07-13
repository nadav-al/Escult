using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Escult.ProcGen
{
    // "Select" is a no-op cursor tool; "Hand" pans the canvas without editing.
    public enum StudioTool { Select, Hand, Wall, Ground, Pit, Gate, Altar, Door, Girl, Cat, Wire, Erase }

    /// <summary>
    /// Lazy cache of the project's real art for canvas preview. Terrain uses the shipped
    /// "Tiles Final" tileset (tile sheet.psd) — the same tiles the released levels render —
    /// not the early BasicTiles placeholders.
    /// </summary>
    public static class EscultArtCache
    {
        const string FinalDir = "Assets/Prefabs/TileMaps/Tiles Final/";

        static bool loaded;
        public static Sprite Floor, Pit, Wall, WallTop, Gate, Bridge, Altar, Door, Girl, Cat;

        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Floor   = TileSprite(FinalDir + "tile sheet_5.asset")  ?? TileSprite("Assets/Sprites/BasicTiles/floor.asset");
            Pit     = TileSprite(FinalDir + "Hell Tile 1.asset")   ?? TileSprite("Assets/Sprites/BasicTiles/pit.asset");
            Wall    = TileSprite(FinalDir + "tile sheet_60.asset") ?? TileSprite("Assets/Sprites/BasicTiles/walls.asset");
            WallTop = TileSprite(FinalDir + "tile sheet_9.asset");
            Gate    = TileSprite(FinalDir + "tile sheet_42.asset") ?? TileSprite("Assets/Sprites/BasicTiles/gate.asset");
            Bridge  = TileSprite("Assets/Sprites/BasicTiles/cat blood bridge.asset");
            Altar   = PrefabSprite("Assets/Prefabs/Alter.prefab");
            Girl    = PrefabSprite("Assets/Prefabs/Girl.prefab");
            Cat     = PrefabSprite("Assets/Prefabs/Cat.prefab");
            Door    = PrefabSprite("Assets/Prefabs/Door.prefab", "DoorOpened");
        }

        static Sprite TileSprite(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Tile>(path);
            return t != null ? t.sprite : null;
        }

        static Sprite PrefabSprite(string path, string childName = null)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) return null;
            if (childName != null)
            {
                var child = go.transform.Find(childName);
                if (child == null) return null;
                var csr = child.GetComponent<SpriteRenderer>();
                return csr != null ? csr.sprite : null;
            }
            var sr = go.GetComponentInChildren<SpriteRenderer>(true);
            return sr != null ? sr.sprite : null;
        }

        public static void Draw(Rect r, Sprite s, Color fallback, float tintA = 1f)
        {
            Draw(r, s, fallback, new Color(1, 1, 1, tintA));
        }

        public static void Draw(Rect r, Sprite s, Color fallback, Color tint)
        {
            if (s == null || s.texture == null)
            {
                var c = fallback; c.a *= tint.a;
                EditorGUI.DrawRect(r, c);
                return;
            }
            var tr = s.textureRect;
            var uv = new Rect(tr.x / s.texture.width, tr.y / s.texture.height,
                              tr.width / s.texture.width, tr.height / s.texture.height);
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTextureWithTexCoords(r, s.texture, uv);
            GUI.color = old;
        }
    }

    /// <summary>
    /// The Studio's interactive level canvas: IMGUI grid with drag painting, wiring clicks,
    /// coordinate rulers, art or schematic rendering, and solver-witness replay overlay.
    /// </summary>
    public class EscultStudioCanvas
    {
        public EscultStudioDoc Doc;
        public StudioTool Tool = StudioTool.Select;
        public char GateLetter = '\0';        // '\0' = new/auto gate
        public char AltarDigit = '\0';
        public bool ArtMode = true;
        public float Zoom = 26f;
        public char SelectedAltar = '\0';     // wire-tool selection
        public EscultReplay Replay;
        public int ReplayStep = -1;           // -1 = replay off
        public Topology Topology;             // last good parse (for replay/wiring hints)

        public Action<string> OnStatus;       // hover / feedback line
        public Action StructureMaybeChanged;  // notify window (legend rebuild, resync)
        public Action<char> OnGatePicked;      // active gate changed via canvas (sync the picker)
        public Action<StudioTool> OnPickTool;  // tool hotkey pressed while the canvas has focus
        public Action OnUndo, OnRedo;          // undo/redo hotkeys while the canvas has focus
        public Action<Vector2> OnPan;          // hand-drag / middle-drag pan delta (pixels)

        const float Pad = 6f;
        const float Ruler = 17f;              // coordinate gutter (top + left)
        bool stroking, panning;
        int strokeButton;
        char strokeGate;

        GUIStyle cellLabel, rulerLabel, rulerHot;

        static readonly Color ColWall = new Color(0.24f, 0.24f, 0.29f);
        static readonly Color ColGround = new Color(0.79f, 0.75f, 0.68f);
        static readonly Color ColPit = new Color(0.36f, 0.08f, 0.13f);
        static readonly Color ColGateC = new Color(0.42f, 0.31f, 0.63f);
        static readonly Color ColGateO = new Color(0.66f, 0.56f, 0.84f);
        static readonly Color ColAltar = new Color(0.85f, 0.64f, 0.25f);
        static readonly Color ColDoor = new Color(0.54f, 0.35f, 0.24f);
        static readonly Color ColGirl = new Color(0.88f, 0.48f, 0.60f);
        static readonly Color ColCat = new Color(0.55f, 0.60f, 0.68f);
        static readonly Color Accent = new Color(1f, 0.82f, 0.28f);

        public Vector2 DesiredSize
        {
            get
            {
                if (Doc == null) return new Vector2(200, 200);
                return new Vector2(Doc.W * Zoom + Pad * 2 + Ruler, Doc.H * Zoom + Pad * 2 + Ruler);
            }
        }

        void EnsureStyles()
        {
            if (cellLabel != null) return;
            cellLabel = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            cellLabel.normal.textColor = Color.white;
            rulerLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
            rulerLabel.normal.textColor = new Color(0.75f, 0.75f, 0.8f);
            rulerHot = new GUIStyle(rulerLabel);
            rulerHot.normal.textColor = Accent;
            rulerHot.fontStyle = FontStyle.Bold;
        }

        public void OnGUI(Rect area)
        {
            if (Doc == null) return;
            EscultArtCache.EnsureLoaded();
            EnsureStyles();

            float ox = area.x + Pad + Ruler, oy = area.y + Pad + Ruler;
            var e = Event.current;

            HandleKeys(e);
            HandleInput(e, ox, oy);

            if (e.type != EventType.Repaint) return;

            EditorGUI.DrawRect(area, new Color(0.13f, 0.13f, 0.16f));

            var replayFrame = (Replay != null && ReplayStep >= 0 && ReplayStep < Replay.Frames.Count)
                ? Replay.Frames[ReplayStep] : null;

            var hover = CellAt(e.mousePosition, ox, oy);

            DrawRulers(ox, oy, hover);

            for (int r = 0; r < Doc.H; r++)
            {
                for (int c = 0; c < Doc.W; c++)
                {
                    var rect = new Rect(ox + c * Zoom, oy + r * Zoom, Zoom - 1, Zoom - 1);
                    DrawCell(rect, Doc.Get(c, r), c, r, replayFrame);
                }
            }

            if (replayFrame != null) DrawReplayActors(replayFrame, ox, oy);

            if (hover.x >= 0)
            {
                var hr = new Rect(ox + hover.x * Zoom, oy + hover.y * Zoom, Zoom - 1, Zoom - 1);
                DrawBorder(hr, new Color(1f, 1f, 1f, 0.55f), 1.5f);
            }
        }

        void DrawRulers(float ox, float oy, Vector2Int hover)
        {
            // step so labels never overlap when zoomed out
            int step = Zoom >= 16 ? 1 : Zoom >= 11 ? 2 : 5;
            for (int c = 0; c < Doc.W; c++)
            {
                if (c % step != 0 && c != Doc.W - 1 && c != hover.x) continue;
                var r = new Rect(ox + c * Zoom, oy - Ruler, Zoom - 1, Ruler - 2);
                GUI.Label(r, c.ToString(), c == hover.x ? rulerHot : rulerLabel);
            }
            for (int r = 0; r < Doc.H; r++)
            {
                if (r % step != 0 && r != Doc.H - 1 && r != hover.y) continue;
                var rr = new Rect(ox - Ruler, oy + r * Zoom, Ruler - 2, Zoom - 1);
                GUI.Label(rr, r.ToString(), r == hover.y ? rulerHot : rulerLabel);
            }
        }

        void DrawCell(Rect rect, char ch, int c, int r, EscultReplay.Frame frame)
        {
            bool isGate = EscultStudioDoc.GateLetters.IndexOf(ch) >= 0;
            bool isAltar = ch >= '1' && ch <= '9';

            char terrain = ch == '#' ? '#' : ch == '~' ? '~' : '.';
            if (isGate && Doc.GateOverPit.TryGetValue(ch, out var op) && op) terrain = '~';

            bool bridged = false;
            if (frame != null && Topology != null && Topology.W == Doc.W && Topology.H == Doc.H)
                bridged = frame.Bridges.Contains(Topology.Idx(c, r));

            if (ArtMode)
            {
                if (terrain == '#') EscultArtCache.Draw(rect, EscultArtCache.Wall, ColWall);
                else if (terrain == '~' && !bridged) EscultArtCache.Draw(rect, EscultArtCache.Pit, ColPit);
                else if (terrain == '~') { EscultArtCache.Draw(rect, EscultArtCache.Pit, ColPit); EscultArtCache.Draw(rect, EscultArtCache.Bridge, ColGround); }
                else EscultArtCache.Draw(rect, EscultArtCache.Floor, ColGround);
            }
            else
            {
                var col = terrain == '#' ? ColWall : terrain == '~' ? (bridged ? Color.Lerp(ColPit, ColGround, 0.6f) : ColPit) : ColGround;
                EditorGUI.DrawRect(rect, col);
            }

            if (isGate)
            {
                bool open = Doc.GateInitialOpen.TryGetValue(ch, out var o) && o;
                if (frame != null && Topology != null)
                {
                    int gi = Topology.GateIndex(ch.ToString());
                    if (gi >= 0 && gi < frame.GateOpen.Length) open = frame.GateOpen[gi];
                }
                var tint = open ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Gate, open ? ColGateO : ColGateC, tint);
                else EditorGUI.DrawRect(Inset(rect, 1), open ? ColGateO : ColGateC);
                bool wired = SelectedAltar != '\0' && Doc.IsWired(SelectedAltar, ch.ToString());
                if (wired) DrawBorder(rect, Accent, 2.5f);
                else if (ch == GateLetter && Tool == StudioTool.Gate) DrawBorder(rect, new Color(0.4f, 0.8f, 1f, 0.9f), 2f);
                if (Zoom >= 13) GUI.Label(rect, ch.ToString(), cellLabel);
            }
            else if (isAltar)
            {
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Altar, ColAltar);
                else EditorGUI.DrawRect(Inset(rect, 1), ColAltar);
                if (ch == SelectedAltar) DrawBorder(rect, Accent, 2.5f);
                if (Zoom >= 13) GUI.Label(rect, ch.ToString(), cellLabel);
            }
            else if (ch == 'O' || ch == 'X')
            {
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Door, ColDoor);
                else EditorGUI.DrawRect(Inset(rect, 1), ch == 'O' ? new Color(0.50f, 0.65f, 0.42f) : ColDoor);
                if (Zoom >= 13) GUI.Label(rect, ch.ToString(), cellLabel);
            }
            else if (ch == '@' && frame == null)
            {
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Girl, ColGirl);
                else EditorGUI.DrawRect(Inset(rect, 2), ColGirl);
                if (Zoom >= 13 && !ArtMode) GUI.Label(rect, "@", cellLabel);
            }
            else if (ch == 'C' && frame == null)
            {
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Cat, ColCat);
                else EditorGUI.DrawRect(Inset(rect, 2), ColCat);
                if (Zoom >= 13 && !ArtMode) GUI.Label(rect, "C", cellLabel);
            }
            else if (ch == '=')
            {
                if (ArtMode) EscultArtCache.Draw(Inset(rect, 1), EscultArtCache.Bridge, ColGround);
                else GUI.Label(rect, "=", cellLabel);
            }
        }

        void DrawReplayActors(EscultReplay.Frame f, float ox, float oy)
        {
            var t = Replay.T;
            Rect CellRect(int idx) { return new Rect(ox + t.Col(idx) * Zoom, oy + t.Row(idx) * Zoom, Zoom - 1, Zoom - 1); }
            if (f.Cat >= 0 && !f.CatHeld)
            {
                var r = CellRect(f.Cat);
                if (ArtMode) EscultArtCache.Draw(Inset(r, 1), EscultArtCache.Cat, ColCat, f.CatDead ? new Color(1, 1, 1, 0.3f) : Color.white);
                else EditorGUI.DrawRect(Inset(r, 2), ColCat);
            }
            if (f.Girl >= 0)
            {
                var r = CellRect(f.Girl);
                if (ArtMode) EscultArtCache.Draw(Inset(r, 1), EscultArtCache.Girl, ColGirl);
                else EditorGUI.DrawRect(Inset(r, 2), ColGirl);
                if (f.CatHeld)
                {
                    var mini = new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f, r.height * 0.55f);
                    EscultArtCache.Draw(mini, EscultArtCache.Cat, ColCat);
                }
            }
        }

        static Rect Inset(Rect r, float px) { return new Rect(r.x + px, r.y + px, r.width - px * 2, r.height - px * 2); }

        static void DrawBorder(Rect r, Color c, float w)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        Vector2Int CellAt(Vector2 mouse, float ox, float oy)
        {
            int c = Mathf.FloorToInt((mouse.x - ox) / Zoom);
            int r = Mathf.FloorToInt((mouse.y - oy) / Zoom);
            return Doc.InBounds(c, r) ? new Vector2Int(c, r) : new Vector2Int(-1, -1);
        }

        // ------------------------------------------------------------------
        //  keyboard (fires when the canvas IMGUIContainer has focus)
        // ------------------------------------------------------------------

        void HandleKeys(Event e)
        {
            if (e.type != EventType.KeyDown) return;
            if (e.control || e.command)
            {
                if (e.keyCode == KeyCode.Z) { if (e.shift) OnRedo?.Invoke(); else OnUndo?.Invoke(); e.Use(); }
                else if (e.keyCode == KeyCode.Y) { OnRedo?.Invoke(); e.Use(); }
                return;
            }
            if (e.shift || e.alt) return;
            StudioTool tool;
            switch (e.keyCode)
            {
                case KeyCode.V: case KeyCode.Escape: tool = StudioTool.Select; break;
                case KeyCode.H: tool = StudioTool.Hand; break;
                case KeyCode.B: tool = StudioTool.Wall; break;
                case KeyCode.G: tool = StudioTool.Ground; break;
                case KeyCode.R: tool = StudioTool.Pit; break;
                case KeyCode.T: tool = StudioTool.Gate; break;
                case KeyCode.A: tool = StudioTool.Altar; break;
                case KeyCode.D: tool = StudioTool.Door; break;
                case KeyCode.F: tool = StudioTool.Girl; break;
                case KeyCode.C: tool = StudioTool.Cat; break;
                case KeyCode.Q: tool = StudioTool.Wire; break;
                case KeyCode.X: tool = StudioTool.Erase; break;
                default: return;
            }
            OnPickTool?.Invoke(tool);
            e.Use();
        }

        // ------------------------------------------------------------------
        //  mouse
        // ------------------------------------------------------------------

        void HandleInput(Event e, float ox, float oy)
        {
            int id = GUIUtility.GetControlID("EscultStudioCanvas".GetHashCode(), FocusType.Passive);

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                {
                    var cell = CellAt(e.mousePosition, ox, oy);
                    if (e.button == 2 || Tool == StudioTool.Hand)      // pan (middle mouse anywhere, or Hand tool)
                    {
                        GUIUtility.hotControl = id; panning = true; e.Use(); break;
                    }
                    if (Tool == StudioTool.Select) { break; }          // no-op cursor
                    if (cell.x < 0 || (e.button != 0 && e.button != 1)) break;
                    GUIUtility.hotControl = id;
                    stroking = true;
                    strokeButton = e.button;
                    strokeGate = '\0';
                    Doc.BeginEdit();
                    ApplyAt(cell, isDown: true);
                    e.Use();
                    break;
                }
                case EventType.MouseDrag:
                {
                    if (GUIUtility.hotControl != id) break;
                    if (panning) { OnPan?.Invoke(e.delta); e.Use(); break; }
                    if (!stroking) break;
                    var cell = CellAt(e.mousePosition, ox, oy);
                    if (cell.x >= 0) ApplyAt(cell, isDown: false);
                    e.Use();
                    break;
                }
                case EventType.MouseUp:
                {
                    if (GUIUtility.hotControl != id) break;
                    GUIUtility.hotControl = 0;
                    if (panning) { panning = false; e.Use(); break; }
                    stroking = false;
                    Doc.Commit();
                    StructureMaybeChanged?.Invoke();
                    e.Use();
                    break;
                }
                case EventType.MouseMove:
                {
                    var cell = CellAt(e.mousePosition, ox, oy);
                    if (cell.x >= 0)
                        OnStatus?.Invoke($"({cell.x},{cell.y})  '{Doc.Get(cell.x, cell.y)}'");
                    break;
                }
                case EventType.ScrollWheel:
                {
                    if (!e.control) break;
                    Zoom = Mathf.Clamp(Zoom - e.delta.y * 1.4f, 10f, 48f);
                    e.Use();
                    StructureMaybeChanged?.Invoke();
                    break;
                }
            }
        }

        void ApplyAt(Vector2Int cell, bool isDown)
        {
            int c = cell.x, r = cell.y;

            if (strokeButton == 1) { Doc.SetCell(c, r, '.'); return; }   // RMB always erases to ground

            switch (Tool)
            {
                case StudioTool.Wall: Doc.SetCell(c, r, '#'); break;
                case StudioTool.Ground: Doc.SetCell(c, r, '.'); break;
                case StudioTool.Erase: Doc.SetCell(c, r, '.'); break;
                case StudioTool.Pit: Doc.SetCell(c, r, '~'); break;

                case StudioTool.Gate:
                {
                    if (strokeGate == '\0')
                    {
                        char existing = Doc.Get(c, r);
                        if (GateLetter != '\0')
                            strokeGate = GateLetter;                          // an explicit gate is picked → always add to it
                        else if (isDown && EscultStudioDoc.GateLetters.IndexOf(existing) >= 0)
                            strokeGate = existing;                            // started on a gate → extend it
                        else
                            strokeGate = Doc.NextFreeGate();                  // brand-new gate
                        if (strokeGate == '\0') { OnStatus?.Invoke("no free gate letters left"); return; }
                        OnGatePicked?.Invoke(strokeGate);                     // make the choice sticky in the picker
                    }
                    Doc.SetCell(c, r, strokeGate);
                    break;
                }

                case StudioTool.Altar:
                {
                    if (!isDown) break;
                    char digit = AltarDigit != '\0' ? AltarDigit : Doc.NextFreeAltar();
                    if (digit == '\0') { OnStatus?.Invoke("all 9 altar ids used"); return; }
                    Doc.SetCell(c, r, digit);
                    break;
                }

                case StudioTool.Door: if (isDown) Doc.SetCell(c, r, 'O'); break;
                case StudioTool.Girl: if (isDown) Doc.SetCell(c, r, '@'); break;
                case StudioTool.Cat: if (isDown) Doc.SetCell(c, r, 'C'); break;

                case StudioTool.Wire:
                {
                    if (!isDown) break;
                    char ch = Doc.Get(c, r);
                    if (ch >= '1' && ch <= '9')
                    {
                        SelectedAltar = SelectedAltar == ch ? '\0' : ch;
                        OnStatus?.Invoke(SelectedAltar == '\0' ? "wiring: no altar selected"
                            : $"wiring altar {SelectedAltar}: click gates to connect/disconnect");
                    }
                    else if (EscultStudioDoc.GateLetters.IndexOf(ch) >= 0 && SelectedAltar != '\0')
                    {
                        Doc.ToggleWiring(SelectedAltar, ch.ToString());
                        OnStatus?.Invoke($"altar {SelectedAltar} ↔ gate {ch}: {(Doc.IsWired(SelectedAltar, ch.ToString()) ? "connected" : "disconnected")}");
                    }
                    else SelectedAltar = '\0';
                    break;
                }
            }
        }
    }
}
