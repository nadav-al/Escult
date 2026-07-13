using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Escult.ProcGen
{
    /// <summary>
    /// Escult Studio — the designer-facing level editor.
    /// Interactive paint canvas + live lint/solve + solution replay + two-way ESN text,
    /// with load/save as ESN artifacts or as playable level prefabs (via EscultPrefabConverter).
    /// Fully resizable UIToolkit layout: every panel scrolls, nothing gets cut on small screens.
    /// </summary>
    public class EscultStudioWindow : EditorWindow
    {
        const string RecentKey = "Escult.Studio.Recent";
        const string GateNewLabel = "＋ new gate";

        EscultStudioDoc doc;
        readonly EscultStudioCanvas canvas = new EscultStudioCanvas();

        // ui
        IMGUIContainer canvasHost;
        ScrollView canvasScroll;
        TextField nameField, esnField, resultsField;
        Label statusLeft, statusRight, replayCaption;
        VisualElement legendPane, toolsPane, replayBar;
        TwoPaneSplitView rightSplit;
        VisualElement mainHost, canvasPane, rightPane;
        SliderInt replaySlider;
        Slider zoomSlider;
        Button replayPlayBtn;
        ToolbarButton dockBtn;
        readonly Dictionary<StudioTool, Button> toolButtons = new Dictionary<StudioTool, Button>();
        PopupField<string> tierPopup, gatePopup, altarPopup;
        IntegerField widthField, heightField, soulsField;
        ToolbarToggle autoSolveToggle, artToggle, panelToggle, tabEsn, tabResults;
        VisualElement esnTab, resultsTab;

        // state
        ParseResult lastParse;
        EscultReport lastReport;
        string legendSignature = "";
        bool syncingText;
        bool rightCollapsed;
        bool panelBottom;
        int replayIntervalMs = 480;
        IVisualElementScheduledItem pendingSolve, pendingTextApply, replayTimer;

        static readonly string[] TierOptions = { "(auto tier)", "tutorial", "easy", "medium", "hard", "extreme" };

        [MenuItem("Escult/Studio")]
        public static void OpenWindow()
        {
            var win = GetWindow<EscultStudioWindow>("Escult Studio");
            win.minSize = new Vector2(640, 400);
        }

        /// <summary>Open the Studio on a specific .esn.txt file (used by the Level Browser).</summary>
        public static void OpenWith(string esnPath)
        {
            OpenWindow();
            GetWindow<EscultStudioWindow>().LoadEsnFile(esnPath);
        }

        void OnEnable() { wantsMouseMove = true; }

        public void CreateGUI()
        {
            doc = doc ?? EscultStudioDoc.NewDefault();
            HookDoc();
            panelBottom = EditorPrefs.GetBool("Escult.Studio.DockBottom", false);

            var root = rootVisualElement;
            root.Clear();
            root.focusable = true;

            root.Add(BuildToolbar());
            replayBar = BuildReplayBar();
            root.Add(replayBar);

            var split = new TwoPaneSplitView(0, 236, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            root.Add(split);

            split.Add(BuildLeftPane());

            canvasPane = BuildCanvasPane();
            rightPane = BuildRightPane();
            mainHost = new VisualElement { style = { flexGrow = 1 } };
            split.Add(mainHost);
            RebuildDock();

            root.Add(BuildStatusBar());

            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            canvas.OnPickTool = SelectTool;
            canvas.OnUndo = () => doc.Undo();
            canvas.OnRedo = () => doc.Redo();
            canvas.OnPan = d => { if (canvasScroll != null) canvasScroll.scrollOffset -= d; };

            canvas.ArtMode = EditorPrefs.GetBool("Escult.Studio.Art", true);
            canvas.Zoom = EditorPrefs.GetFloat("Escult.Studio.Zoom", 26f);
            artToggle.SetValueWithoutNotify(canvas.ArtMode);
            zoomSlider.SetValueWithoutNotify(canvas.Zoom);
            autoSolveToggle.SetValueWithoutNotify(EditorPrefs.GetBool("Escult.Studio.AutoSolve", true));

            SelectTool(StudioTool.Select);
            ShowTab(esn: false);
            OnDocChanged();
        }

        void HookDoc()
        {
            doc.Changed -= OnDocChanged;
            doc.Changed += OnDocChanged;
            canvas.Doc = doc;
            canvas.OnStatus = s => { if (statusRight != null) statusRight.text = s; canvasHost?.MarkDirtyRepaint(); };
            canvas.StructureMaybeChanged = () => { SyncCanvasSize(); RebuildLegendIfNeeded(); canvasHost?.MarkDirtyRepaint(); };
            canvas.OnGatePicked = g =>
            {
                if (gatePopup != null) gatePopup.SetValueWithoutNotify(g.ToString());
                canvas.GateLetter = g;
            };
        }

        // ==================================================================
        //  UI construction
        // ==================================================================

        VisualElement BuildToolbar()
        {
            var tb = new Toolbar();

            tb.Add(new ToolbarButton(ActionNew) { text = "New" });
            tb.Add(new ToolbarButton(ShowLoadMenu) { text = "Load ▾" });
            // primary Save: writes ESN + artifacts AND builds the prefab
            tb.Add(new ToolbarButton(() => ActionSaveBoth()) { text = "Save", tooltip = "Save ESN + emit artifacts + build/refresh the level prefab" });
            tb.Add(new ToolbarButton(ShowSaveMenu) { text = "▾", tooltip = "Other save options" });

            tb.Add(new ToolbarSpacer());
            tb.Add(new ToolbarButton(() => doc.Undo()) { text = "↶", tooltip = "Undo (Ctrl+Z)" });
            tb.Add(new ToolbarButton(() => doc.Redo()) { text = "↷", tooltip = "Redo (Ctrl+Shift+Z / Ctrl+Y)" });
            nameField = new TextField { value = doc.Name, tooltip = "Level name (folder + file names)" };
            nameField.style.minWidth = 110;
            nameField.RegisterValueChangedCallback(e => { doc.Name = string.IsNullOrWhiteSpace(e.newValue) ? "level" : e.newValue.Trim(); });
            tb.Add(nameField);

            tierPopup = new PopupField<string>(TierOptions.ToList(), 0) { tooltip = "Requested difficulty tier (validated against the solver's estimate)" };
            tb.Add(tierPopup);

            tb.Add(new ToolbarSpacer());
            tb.Add(new ToolbarButton(() => SolveNow()) { text = "Solve ✓", tooltip = "Lint + solve + score right now" });

            autoSolveToggle = new ToolbarToggle { text = "Auto", value = true, tooltip = "Re-solve automatically after every edit" };
            autoSolveToggle.RegisterValueChangedCallback(e => { EditorPrefs.SetBool("Escult.Studio.AutoSolve", e.newValue); if (e.newValue) ScheduleSolve(); });
            tb.Add(autoSolveToggle);

            tb.Add(new ToolbarButton(ActionToggleReplay) { text = "Replay ▶", tooltip = "Scrub through the solver's certified solution" });

            tb.Add(new ToolbarSpacer());
            artToggle = new ToolbarToggle { text = "Art", value = true, tooltip = "Preview with the game's real tiles/sprites" };
            artToggle.RegisterValueChangedCallback(e =>
            {
                canvas.ArtMode = e.newValue;
                EditorPrefs.SetBool("Escult.Studio.Art", e.newValue);
                canvasHost.MarkDirtyRepaint();
            });
            tb.Add(artToggle);

            panelToggle = new ToolbarToggle { text = "Panel", value = true, tooltip = "Show/hide the Results / ESN text panel" };
            panelToggle.RegisterValueChangedCallback(e => SetRightPaneVisible(e.newValue));
            tb.Add(panelToggle);

            dockBtn = new ToolbarButton(() =>
            {
                panelBottom = !panelBottom;
                EditorPrefs.SetBool("Escult.Studio.DockBottom", panelBottom);
                RebuildDock();
                dockBtn.text = panelBottom ? "Dock: Bottom" : "Dock: Side";
            })
            { text = panelBottom ? "Dock: Bottom" : "Dock: Side", tooltip = "Dock the Results / ESN panel on the side or along the bottom" };
            tb.Add(dockBtn);

            zoomSlider = new Slider(10, 48) { value = 26, tooltip = "Zoom (also Ctrl+wheel on the canvas)" };
            zoomSlider.style.width = 90;
            zoomSlider.RegisterValueChangedCallback(e =>
            {
                canvas.Zoom = e.newValue;
                EditorPrefs.SetFloat("Escult.Studio.Zoom", e.newValue);
                SyncCanvasSize();
                canvasHost.MarkDirtyRepaint();
            });
            tb.Add(zoomSlider);

            return tb;
        }

        VisualElement BuildReplayBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2,
                    backgroundColor = new Color(0.18f, 0.14f, 0.22f),
                    display = DisplayStyle.None,
                }
            };
            var toStart = new Button(() => { StopAutoPlay(); SetReplayStep(0); }) { text = "⏮", tooltip = "Jump to start" };
            var prev = new Button(() => { StopAutoPlay(); SetReplayStep(canvas.ReplayStep - 1); }) { text = "◀", tooltip = "Step back" };
            replayPlayBtn = new Button(ToggleAutoPlay) { text = "▶", tooltip = "Play / pause the solution animation" };
            var next = new Button(() => { StopAutoPlay(); SetReplayStep(canvas.ReplayStep + 1); }) { text = "▶▌", tooltip = "Step forward" };
            replaySlider = new SliderInt(0, 1) { style = { flexGrow = 1, marginLeft = 4, marginRight = 4 } };
            replaySlider.RegisterValueChangedCallback(e => { if (e.newValue != canvas.ReplayStep) StopAutoPlay(); SetReplayStep(e.newValue); });
            var speed = new PopupField<string>(new List<string> { "0.5×", "1×", "2×", "4×" }, 1) { tooltip = "Playback speed" };
            speed.RegisterValueChangedCallback(e =>
            {
                replayIntervalMs = e.newValue == "0.5×" ? 900 : e.newValue == "2×" ? 240 : e.newValue == "4×" ? 110 : 480;
                if (replayTimer != null) { StopAutoPlay(); ToggleAutoPlay(); }   // restart at new rate
            });
            replayCaption = new Label("") { style = { minWidth = 220, unityTextAlign = TextAnchor.MiddleLeft } };
            var close = new Button(ActionToggleReplay) { text = "✕", tooltip = "Close replay" };
            bar.Add(toStart); bar.Add(prev); bar.Add(replayPlayBtn); bar.Add(next);
            bar.Add(replaySlider); bar.Add(speed); bar.Add(replayCaption); bar.Add(close);
            return bar;
        }

        VisualElement BuildLeftPane()
        {
            var scroll = new ScrollView { style = { minWidth = 150 } };
            toolsPane = new VisualElement { style = { paddingLeft = 6, paddingRight = 6, paddingTop = 6 } };
            scroll.Add(toolsPane);

            toolsPane.Add(Header("Tools"));
            AddToolButton(StudioTool.Select, "Select  ⭘", "V — cursor only; clicking never changes the map");
            AddToolButton(StudioTool.Hand, "Hand  ✋", "H — drag to pan the canvas (middle-mouse drag pans with any tool)");
            AddToolButton(StudioTool.Wall, "Wall  #", "B — paint solid wall (throw backstop)");
            AddToolButton(StudioTool.Ground, "Ground  .", "G — paint walkable ground");
            AddToolButton(StudioTool.Pit, "Pit  ~", "R — paint hell/pit (bridgeable, costs souls)");
            AddToolButton(StudioTool.Gate, "Gate  A–Z", "T — drag to paint gate cells (pick which gate below)");
            AddToolButton(StudioTool.Altar, "Altar  1–9", "A — click to place an altar");
            AddToolButton(StudioTool.Door, "Door  O", "D — click to place the exit door");
            AddToolButton(StudioTool.Girl, "Girl  @", "F — click to move the girl spawn");
            AddToolButton(StudioTool.Cat, "Cat  C", "C — click to move the cat spawn");
            AddToolButton(StudioTool.Wire, "Wire  ⚡", "Q — click an altar, then click gates to toggle wiring");
            AddToolButton(StudioTool.Erase, "Erase", "X — set cells back to ground (right-drag does this with any tool)");

            var gateChoices = new List<string> { GateNewLabel };
            gateChoices.AddRange(EscultStudioDoc.GateLetters.Select(c => c.ToString()));
            gatePopup = new PopupField<string>("Paint gate", gateChoices, 0)
            { tooltip = "Which gate the Gate tool paints into. Pick an existing letter to ADD to that gate; '＋ new gate' starts a fresh one." };
            gatePopup.RegisterValueChangedCallback(e =>
            {
                canvas.GateLetter = e.newValue == GateNewLabel ? '\0' : e.newValue[0];
                canvasHost?.MarkDirtyRepaint();
            });
            toolsPane.Add(gatePopup);

            var altarChoices = new List<string> { "auto" };
            altarChoices.AddRange("123456789".Select(c => c.ToString()));
            altarPopup = new PopupField<string>("Place altar", altarChoices, 0);
            altarPopup.RegisterValueChangedCallback(e => canvas.AltarDigit = e.newValue == "auto" ? '\0' : e.newValue[0]);
            toolsPane.Add(altarPopup);

            toolsPane.Add(Header("Canvas"));
            widthField = new IntegerField("Width") { value = doc.W };
            heightField = new IntegerField("Height") { value = doc.H };
            var applySize = new Button(() =>
            {
                doc.BeginEdit();
                doc.ResizeCanvas(widthField.value, heightField.value);
                doc.Commit();
            }) { text = "Apply size" };
            toolsPane.Add(widthField);
            toolsPane.Add(heightField);
            toolsPane.Add(applySize);

            toolsPane.Add(Header("Legend"));
            soulsField = new IntegerField("Souls") { value = doc.Souls, tooltip = "Soul budget (engine default is 9)" };
            soulsField.RegisterValueChangedCallback(e =>
            {
                doc.BeginEdit();
                doc.Souls = Mathf.Clamp(e.newValue, 0, 99);
                doc.Commit();
            });
            toolsPane.Add(soulsField);

            legendPane = new VisualElement();
            toolsPane.Add(legendPane);

            return scroll;
        }

        void AddToolButton(StudioTool tool, string text, string tip)
        {
            var b = new Button(() => SelectTool(tool)) { text = text, tooltip = tip };
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            toolButtons[tool] = b;
            toolsPane.Add(b);
        }

        VisualElement BuildCanvasPane()
        {
            var holder = new VisualElement { style = { flexGrow = 1, minWidth = 120 } };
            canvasScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal) { style = { flexGrow = 1 } };
            canvasHost = new IMGUIContainer(() =>
            {
                var size = canvas.DesiredSize;
                canvas.OnGUI(new Rect(0, 0, size.x, size.y));
            });
            canvasHost.style.flexShrink = 0;
            // Focusing the IMGUIContainer routes key events (tool hotkeys, Ctrl+Z) into the
            // canvas's own handler instead of Unity's global shortcuts.
            canvasHost.focusable = true;
            canvasHost.RegisterCallback<PointerDownEvent>(_ => canvasHost.Focus());
            canvasScroll.Add(canvasHost);
            holder.Add(canvasScroll);
            SyncCanvasSize();
            return holder;
        }

        VisualElement BuildRightPane()
        {
            var pane = new VisualElement { style = { minWidth = 160 } };

            var tabs = new Toolbar();
            tabResults = new ToolbarToggle { text = "Results", value = true };
            tabEsn = new ToolbarToggle { text = "ESN text", value = false };
            tabResults.RegisterValueChangedCallback(e => ShowTab(esn: !e.newValue));
            tabEsn.RegisterValueChangedCallback(e => ShowTab(esn: e.newValue));
            tabs.Add(tabResults);
            tabs.Add(tabEsn);
            var hide = new ToolbarButton(() => SetRightPaneVisible(false)) { text = "✕", tooltip = "Hide this panel" };
            hide.style.marginLeft = 4;
            tabs.Add(hide);
            pane.Add(tabs);

            resultsTab = new ScrollView { style = { flexGrow = 1 } };
            resultsField = new TextField { multiline = true, isReadOnly = true };
            resultsField.style.whiteSpace = WhiteSpace.Normal;
            Mono(resultsField);
            ((ScrollView)resultsTab).Add(resultsField);
            pane.Add(resultsTab);

            esnTab = new ScrollView { style = { flexGrow = 1 } };
            esnField = new TextField { multiline = true };
            Mono(esnField);
            esnField.RegisterValueChangedCallback(e =>
            {
                if (syncingText) return;
                pendingTextApply?.Pause();
                pendingTextApply = rootVisualElement.schedule.Execute(() => ApplyTextToDoc(e.newValue));
                pendingTextApply.ExecuteLater(600);
            });
            ((ScrollView)esnTab).Add(esnField);
            pane.Add(esnTab);

            return pane;
        }

        static void Mono(TextField f)
        {
            var font = Font.CreateDynamicFontFromOSFont("Consolas", 12);
            if (font != null) f.style.unityFont = font;
            f.style.fontSize = 12;
        }

        void ShowTab(bool esn)
        {
            tabEsn.SetValueWithoutNotify(esn);
            tabResults.SetValueWithoutNotify(!esn);
            esnTab.style.display = esn ? DisplayStyle.Flex : DisplayStyle.None;
            resultsTab.style.display = esn ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void SetRightPaneVisible(bool visible)
        {
            if (rightSplit == null) return;
            if (visible && rightCollapsed) { rightSplit.UnCollapse(); rightCollapsed = false; }
            else if (!visible && !rightCollapsed) { rightSplit.CollapseChild(1); rightCollapsed = true; }
            if (panelToggle != null) panelToggle.SetValueWithoutNotify(visible);
        }

        /// <summary>(Re)build the canvas-vs-panel split, docking the panel on the side or the bottom.</summary>
        void RebuildDock()
        {
            if (mainHost == null) return;
            bool wasVisible = !rightCollapsed;
            mainHost.Clear();
            rightSplit = new TwoPaneSplitView(1,
                panelBottom ? 220 : 330,
                panelBottom ? TwoPaneSplitViewOrientation.Vertical : TwoPaneSplitViewOrientation.Horizontal)
            { style = { flexGrow = 1 } };
            rightSplit.Add(canvasPane);
            rightSplit.Add(rightPane);
            mainHost.Add(rightSplit);
            rightCollapsed = false;
            SyncCanvasSize();
            if (!wasVisible) rootVisualElement.schedule.Execute(() => SetRightPaneVisible(false));
        }

        VisualElement BuildStatusBar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 2, paddingBottom = 2,
                    borderTopWidth = 1, borderTopColor = new Color(0, 0, 0, 0.35f),
                }
            };
            statusLeft = new Label("");
            statusRight = new Label("") { style = { unityTextAlign = TextAnchor.MiddleRight } };
            bar.Add(statusLeft);
            bar.Add(statusRight);
            return bar;
        }

        static Label Header(string text)
        {
            return new Label(text)
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 8, marginBottom = 2 }
            };
        }

        // ==================================================================
        //  document sync
        // ==================================================================

        void OnDocChanged()
        {
            if (rootVisualElement.panel == null) return;

            if (canvas.ReplayStep >= 0) StopReplay();

            lastParse = doc.Parse();
            canvas.Topology = lastParse.Topology;

            syncingText = true;
            if (esnField != null && esnField.focusController?.focusedElement != esnField)
                esnField.SetValueWithoutNotify(doc.ToEsnText());
            syncingText = false;

            if (nameField != null && nameField.focusController?.focusedElement != nameField)
                nameField.SetValueWithoutNotify(doc.Name);
            if (widthField != null) widthField.SetValueWithoutNotify(doc.W);
            if (heightField != null) heightField.SetValueWithoutNotify(doc.H);
            if (soulsField != null) soulsField.SetValueWithoutNotify(doc.Souls);

            RebuildLegendIfNeeded();
            SyncCanvasSize();
            canvasHost?.MarkDirtyRepaint();

            if (lastParse.HasErrors)
            {
                lastReport = null;
                UpdateResults(LintText(lastParse));
                UpdateStatus();
            }
            else if (autoSolveToggle != null && autoSolveToggle.value) ScheduleSolve();
            else UpdateStatus();
        }

        void ApplyTextToDoc(string text)
        {
            if (!doc.ApplyEsnText(text))
                statusLeft.text = "ESN text has no readable canvas — keeping the previous layout";
        }

        void ScheduleSolve()
        {
            pendingSolve?.Pause();
            pendingSolve = rootVisualElement.schedule.Execute(() => SolveNow());
            pendingSolve.ExecuteLater(650);
        }

        string RequestedTier()
        {
            return tierPopup == null || tierPopup.index == 0 ? null : TierOptions[tierPopup.index];
        }

        void SolveNow()
        {
            pendingSolve?.Pause();
            lastParse = doc.Parse();
            canvas.Topology = lastParse.Topology;
            if (lastParse.HasErrors || lastParse.Topology == null)
            {
                lastReport = null;
                UpdateResults(LintText(lastParse));
                UpdateStatus();
                return;
            }
            try
            {
                lastReport = EscultValidator.Run(lastParse.Topology, lastParse.Lints, RequestedTier());
                UpdateResults(EscultValidator.ToMarkdown(lastReport, lastParse.Topology));
            }
            catch (Exception ex)
            {
                lastReport = null;
                UpdateResults("solver exception: " + ex.Message);
            }
            UpdateStatus();
        }

        static string LintText(ParseResult p)
        {
            var sb = new StringBuilder("PARSE / LINT\n\n");
            foreach (var l in p.Lints) sb.AppendLine("- " + l);
            if (p.Lints.Count == 0) sb.AppendLine("(clean)");
            return sb.ToString();
        }

        void UpdateResults(string text)
        {
            if (resultsField != null) resultsField.SetValueWithoutNotify(text);
        }

        void UpdateStatus()
        {
            if (statusLeft == null) return;
            if (lastParse == null) { statusLeft.text = ""; return; }
            if (lastParse.HasErrors)
            {
                int n = lastParse.Lints.Count(l => l.IsError);
                statusLeft.text = $"✗ {n} parse error(s) — see Results";
                return;
            }
            if (lastReport == null || lastReport.Solve == null)
            {
                statusLeft.text = $"{doc.W}×{doc.H} — not solved yet";
                return;
            }
            var s = lastReport.Solve;
            statusLeft.text = s.Solvable
                ? $"✓ solvable — minCost {s.MinCost}, slack {doc.Souls - s.MinCost}, tier {lastReport.TierEstimate ?? "?"}{(lastReport.Valid ? "" : "  (checks FAILED — see Results)")}"
                : $"✗ NOT solvable{(s.Error != null ? " — " + s.Error : "")}";
        }

        void SyncCanvasSize()
        {
            if (canvasHost == null) return;
            var size = canvas.DesiredSize;
            canvasHost.style.width = size.x;
            canvasHost.style.height = size.y;
        }

        void RebuildLegendIfNeeded()
        {
            rootVisualElement.schedule.Execute(RebuildLegendNow);
        }

        void RebuildLegendNow()
        {
            if (legendPane == null) return;
            string sig = doc.ToEsnText();
            if (sig == legendSignature) return;
            legendSignature = sig;
            legendPane.Clear();

            var gates = doc.PresentGates().ToList();
            var altars = doc.PresentAltars().ToList();

            foreach (var a in altars)
            {
                var row = new VisualElement { style = { marginTop = 4 } };
                row.Add(new Label($"Altar {a} →") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                var chips = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
                foreach (var g in gates)
                {
                    char altarId = a, gateId = g;
                    var chip = new Button { text = g.ToString() };
                    StyleChip(chip, doc.IsWired(a, g.ToString()));
                    chip.clicked += () =>
                    {
                        doc.BeginEdit();
                        doc.ToggleWiring(altarId, gateId.ToString());
                        doc.Commit();
                    };
                    chips.Add(chip);
                }
                var decoy = new Toggle("decoy") { value = doc.Decoys.Contains(a.ToString()) };
                decoy.RegisterValueChangedCallback(e =>
                {
                    doc.BeginEdit();
                    if (e.newValue) doc.Decoys.Add(a.ToString()); else doc.Decoys.Remove(a.ToString());
                    doc.Commit();
                });
                row.Add(chips);
                row.Add(decoy);
                legendPane.Add(row);
            }

            foreach (var g in gates)
            {
                char gateId = g;
                var row = new VisualElement { style = { marginTop = 4 } };
                row.Add(new Label($"Gate {g}") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                var open = new Toggle("starts OPEN") { value = doc.GateInitialOpen.TryGetValue(g, out var o) && o };
                open.RegisterValueChangedCallback(e =>
                {
                    doc.BeginEdit();
                    doc.GateInitialOpen[gateId] = e.newValue;
                    doc.Commit();
                });
                var pit = new Toggle("over pit") { value = doc.GateOverPit.TryGetValue(g, out var p) && p, tooltip = "Terrain beneath the gate is pit — needs a bridge even when open" };
                pit.RegisterValueChangedCallback(e =>
                {
                    doc.BeginEdit();
                    doc.GateOverPit[gateId] = e.newValue;
                    doc.Commit();
                });
                var decoy = new Toggle("decoy") { value = doc.Decoys.Contains(g.ToString()) };
                decoy.RegisterValueChangedCallback(e =>
                {
                    doc.BeginEdit();
                    if (e.newValue) doc.Decoys.Add(gateId.ToString()); else doc.Decoys.Remove(gateId.ToString());
                    doc.Commit();
                });
                row.Add(open);
                row.Add(pit);
                row.Add(decoy);
                legendPane.Add(row);
            }

            if (altars.Count == 0 && gates.Count == 0)
                legendPane.Add(new Label("(no altars/gates on canvas yet)") { style = { opacity = 0.6f } });
        }

        static void StyleChip(Button chip, bool on)
        {
            chip.style.minWidth = 26;
            chip.style.backgroundColor = on ? new Color(0.95f, 0.75f, 0.2f, 0.85f) : new Color(0, 0, 0, 0.25f);
            chip.style.color = on ? Color.black : new Color(1, 1, 1, 0.8f);
        }

        void SelectTool(StudioTool tool)
        {
            canvas.Tool = tool;
            if (tool != StudioTool.Wire) canvas.SelectedAltar = '\0';
            foreach (var kv in toolButtons)
                kv.Value.style.backgroundColor = kv.Key == tool
                    ? new Color(0.28f, 0.45f, 0.7f, 0.9f)
                    : new Color(0, 0, 0, 0f);
            statusRight.text = tool == StudioTool.Wire ? "wiring: click an altar, then click gates"
                             : tool == StudioTool.Gate ? "gate: pick which gate in 'Paint gate' to add cells to it"
                             : tool == StudioTool.Select ? "select: cursor only, nothing changes on click"
                             : tool == StudioTool.Hand ? "hand: drag to pan (middle-drag pans with any tool)"
                             : "";
            canvasHost?.MarkDirtyRepaint();
        }

        // ==================================================================
        //  keyboard
        // ==================================================================

        void OnKeyDown(KeyDownEvent e)
        {
            var focused = rootVisualElement.focusController?.focusedElement as VisualElement;
            bool inText = focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);

            // Undo / redo work everywhere except while typing in a text field.
            if (e.ctrlKey && e.keyCode == KeyCode.Z && !inText)
            {
                if (e.shiftKey) doc.Redo(); else doc.Undo();
                e.StopPropagation();
                return;
            }
            if (e.ctrlKey && e.keyCode == KeyCode.Y && !inText)
            {
                doc.Redo(); e.StopPropagation();
                return;
            }
            if (inText || e.ctrlKey || e.altKey) return;

            switch (e.keyCode)
            {
                case KeyCode.V: case KeyCode.Escape: SelectTool(StudioTool.Select); break;
                case KeyCode.H: SelectTool(StudioTool.Hand); break;
                case KeyCode.Q: SelectTool(StudioTool.Wire); break;
                case KeyCode.B: SelectTool(StudioTool.Wall); break;
                case KeyCode.G: SelectTool(StudioTool.Ground); break;
                case KeyCode.R: SelectTool(StudioTool.Pit); break;
                case KeyCode.T: SelectTool(StudioTool.Gate); break;
                case KeyCode.A: SelectTool(StudioTool.Altar); break;
                case KeyCode.D: SelectTool(StudioTool.Door); break;
                case KeyCode.F: SelectTool(StudioTool.Girl); break;
                case KeyCode.C: SelectTool(StudioTool.Cat); break;
                case KeyCode.X: SelectTool(StudioTool.Erase); break;
                default: return;
            }
            e.StopPropagation();
        }

        // ==================================================================
        //  replay
        // ==================================================================

        void ActionToggleReplay()
        {
            if (canvas.ReplayStep >= 0) { StopReplay(); return; }

            if (lastReport == null || lastReport.Solve == null || !lastReport.Solve.Solvable) SolveNow();
            if (lastReport == null || lastReport.Solve == null || !lastReport.Solve.Solvable || lastParse.Topology == null)
            {
                statusLeft.text = "replay needs a solvable level";
                return;
            }
            canvas.Replay = new EscultReplay(lastParse.Topology, lastReport.Solve.Witness);
            replaySlider.highValue = canvas.Replay.Frames.Count - 1;
            replayBar.style.display = DisplayStyle.Flex;
            SetReplayStep(0);
        }

        void StopReplay()
        {
            StopAutoPlay();
            canvas.ReplayStep = -1;
            canvas.Replay = null;
            if (replayBar != null) replayBar.style.display = DisplayStyle.None;
            canvasHost?.MarkDirtyRepaint();
        }

        void ToggleAutoPlay()
        {
            if (replayTimer != null) { StopAutoPlay(); return; }
            if (canvas.Replay == null) return;
            if (canvas.ReplayStep >= canvas.Replay.Frames.Count - 1) SetReplayStep(0);   // restart from the top
            if (replayPlayBtn != null) replayPlayBtn.text = "⏸";
            replayTimer = rootVisualElement.schedule.Execute(AutoTick).Every(replayIntervalMs);
        }

        void AutoTick()
        {
            if (canvas.Replay == null) { StopAutoPlay(); return; }
            if (canvas.ReplayStep >= canvas.Replay.Frames.Count - 1) { StopAutoPlay(); return; }
            SetReplayStep(canvas.ReplayStep + 1);
        }

        void StopAutoPlay()
        {
            replayTimer?.Pause();
            replayTimer = null;
            if (replayPlayBtn != null) replayPlayBtn.text = "▶";
        }

        void SetReplayStep(int step)
        {
            if (canvas.Replay == null) return;
            step = Mathf.Clamp(step, 0, canvas.Replay.Frames.Count - 1);
            canvas.ReplayStep = step;
            replaySlider.SetValueWithoutNotify(step);
            var f = canvas.Replay.Frames[step];
            replayCaption.text = $"step {step}/{canvas.Replay.Frames.Count - 1} — {f.Caption}   souls: {f.Souls}";
            canvasHost.MarkDirtyRepaint();
        }

        // ==================================================================
        //  recent files
        // ==================================================================

        static List<string> GetRecent()
        {
            var raw = EditorPrefs.GetString(RecentKey, "");
            return raw.Split('\n').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        }

        static void AddRecent(string path)
        {
            path = Path.GetFullPath(path);
            var list = GetRecent();
            list.RemoveAll(p => string.Equals(Path.GetFullPath(p), path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > 12) list = list.Take(12).ToList();
            EditorPrefs.SetString(RecentKey, string.Join("\n", list));
        }

        void ShowLoadMenu()
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Load ESN file…"), false, ActionLoadEsn);
            m.AddItem(new GUIContent("Load level prefab…"), false, ActionImportPrefab);
            var recents = GetRecent();
            if (recents.Count > 0)
            {
                m.AddSeparator("");
                foreach (var p in recents)
                {
                    string cap = "Recent/" + MenuCaption(p);
                    string path = p;
                    if (File.Exists(p)) m.AddItem(new GUIContent(cap), false, () => LoadEsnFile(path));
                    else m.AddDisabledItem(new GUIContent(cap + "  (missing)"));
                }
                m.AddSeparator("Recent/");
                m.AddItem(new GUIContent("Recent/Clear list"), false, () => EditorPrefs.DeleteKey(RecentKey));
            }
            m.ShowAsContext();
        }

        static string MenuCaption(string esnPath)
        {
            string fn = Path.GetFileName(esnPath);
            if (fn.EndsWith(".esn.txt")) fn = fn.Substring(0, fn.Length - ".esn.txt".Length);
            // disambiguate same-named levels by their parent folder
            string parent = Path.GetFileName(Path.GetDirectoryName(esnPath) ?? "");
            string cap = parent.Length > 0 && !string.Equals(parent, fn, StringComparison.OrdinalIgnoreCase)
                ? $"{fn}  ({parent})" : fn;
            return cap.Replace('/', '⁄');   // keep GenericMenu from treating it as a submenu
        }

        void ShowSaveMenu()
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Save ESN only"), false, () => ActionSaveEsn(false));
            m.AddItem(new GUIContent("Save ESN + emit artifacts"), false, () => ActionSaveEsn(true));
            m.AddItem(new GUIContent("Build level prefab"), false, () => ActionBuildPrefab(false));
            m.AddItem(new GUIContent("Build prefab + insert into scene"), false, () => ActionBuildPrefab(true));
            m.ShowAsContext();
        }

        // ==================================================================
        //  actions
        // ==================================================================

        void ActionNew()
        {
            if (!EditorUtility.DisplayDialog("Escult Studio", "Start a new level? Unsaved canvas changes are kept only in the undo history.", "New level", "Cancel"))
                return;
            doc = EscultStudioDoc.NewDefault();
            HookDoc();
            legendSignature = "\0invalid";
            OnDocChanged();
        }

        void ActionLoadEsn()
        {
            string start = Directory.Exists(EscultCli.LevelsRoot) ? EscultCli.LevelsRoot : EscultCli.ProjectRoot;
            string path = EditorUtility.OpenFilePanel("Load ESN level", start, "txt");
            if (string.IsNullOrEmpty(path)) return;
            LoadEsnFile(path);
        }

        public void LoadEsnFile(string path)
        {
            string fn = Path.GetFileName(path);
            string name = fn.EndsWith(".esn.txt") ? fn.Substring(0, fn.Length - ".esn.txt".Length) : Path.GetFileNameWithoutExtension(path);
            var d = EscultStudioDoc.FromEsnText(File.ReadAllText(path), name);
            if (d == null)
            {
                EditorUtility.DisplayDialog("Escult Studio", "That file has no readable ESN canvas.", "OK");
                return;
            }
            doc = d;
            HookDoc();
            legendSignature = "\0invalid";
            AddRecent(path);
            OnDocChanged();
        }

        void ActionImportPrefab()
        {
            string abs = EditorUtility.OpenFilePanel("Import level prefab", Path.GetFullPath("Assets/Prefabs/Levels"), "prefab");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs.Replace('\\', '/'));
            if (string.IsNullOrEmpty(rel)) { EditorUtility.DisplayDialog("Escult Studio", "Pick a prefab inside this project.", "OK"); return; }

            var res = EscultPrefabConverter.FromPrefabAsset(rel);
            if (!res.Ok)
            {
                EditorUtility.DisplayDialog("Escult Studio", "Import failed: " + res.Error, "OK");
                return;
            }
            doc = EscultStudioDoc.FromTopology(res.Topology);
            HookDoc();
            legendSignature = "\0invalid";
            OnDocChanged();
            if (res.Warnings.Count > 0)
                UpdateResults("PREFAB IMPORT WARNINGS\n\n- " + string.Join("\n- ", res.Warnings));
        }

        string LevelFolder() { return Path.Combine(EscultCli.LevelsRoot, Sanitized(doc.Name)); }
        static string Sanitized(string s)
        {
            foreach (char bad in Path.GetInvalidFileNameChars()) s = s.Replace(bad, '_');
            return s.Trim();
        }

        string WriteEsn()
        {
            string folder = LevelFolder();
            Directory.CreateDirectory(folder);
            string esnPath = Path.Combine(folder, Sanitized(doc.Name) + ".esn.txt");
            File.WriteAllText(esnPath, doc.ToEsnText(), new UTF8Encoding(false));
            AddRecent(esnPath);
            return esnPath;
        }

        void ActionSaveEsn(bool emitAll)
        {
            string esnPath = WriteEsn();
            if (emitAll)
            {
                UpdateResults(EscultCli.Check(esnPath, RequestedTier()));
                ShowTab(esn: false);
            }
            EscultCli.RefreshIfInsideAssets(Path.GetDirectoryName(esnPath));
            statusLeft.text = (emitAll ? "artifacts emitted: " : "saved: ") + esnPath;
        }

        /// <summary>Primary Save: ESN + all artifacts + (re)build the level prefab in one click.</summary>
        void ActionSaveBoth()
        {
            string esnPath = WriteEsn();
            UpdateResults(EscultCli.Check(esnPath, RequestedTier()));
            ShowTab(esn: false);

            lastParse = doc.Parse();
            if (lastParse.HasErrors || lastParse.Topology == null)
            {
                statusLeft.text = "saved ESN + artifacts (parse errors — prefab skipped, see Results)";
                return;
            }
            var res = EscultPrefabConverter.BuildPrefab(lastParse.Topology);
            if (!res.Ok)
            {
                statusLeft.text = "saved ESN + artifacts; prefab FAILED: " + res.Error;
                return;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(res.PrefabPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
            statusLeft.text = "saved ESN + artifacts + prefab: " + res.PrefabPath
                            + (res.Warnings.Count > 0 ? "  (" + res.Warnings.Count + " warning(s), see Console)" : "");
            if (res.Warnings.Count > 0) Debug.Log("Escult Studio prefab warnings:\n- " + string.Join("\n- ", res.Warnings));
        }

        void ActionBuildPrefab(bool insertIntoScene)
        {
            lastParse = doc.Parse();
            if (lastParse.HasErrors || lastParse.Topology == null)
            {
                EditorUtility.DisplayDialog("Escult Studio", "Fix the parse errors first (see Results).", "OK");
                return;
            }
            if (lastReport == null || lastReport.Solve == null) SolveNow();
            bool certified = lastReport != null && lastReport.Solve != null && lastReport.Solve.Solvable;
            if (!certified &&
                !EditorUtility.DisplayDialog("Escult Studio",
                    "The solver has NOT certified this level as solvable. Build the prefab anyway?",
                    "Build anyway", "Cancel"))
                return;

            var res = EscultPrefabConverter.BuildPrefab(lastParse.Topology);
            if (!res.Ok)
            {
                EditorUtility.DisplayDialog("Escult Studio", "Prefab build failed: " + res.Error, "OK");
                return;
            }

            string msg = "prefab: " + res.PrefabPath;
            if (insertIntoScene) msg += "\n" + EscultPrefabConverter.InsertIntoOpenScene(res.PrefabPath);
            if (res.Warnings.Count > 0) msg += "\nwarnings:\n- " + string.Join("\n- ", res.Warnings);
            statusLeft.text = msg.Replace('\n', ' ');
            Debug.Log("Escult Studio: " + msg);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(res.PrefabPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }
    }
}
