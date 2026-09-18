using System.Collections.Generic;
using System.Linq;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace TsiYuki.Wardrobe.Editor
{
    // Editing view over the YukiWardrobe components of one avatar. The window
    // holds no wardrobe data: every edit goes straight to the component
    // through Undo, so it can be closed at any time without losing anything.
    public class WardrobeWindow : EditorWindow
    {
        static YukiLocalizer L => WardrobeText.L;
        static GUILayoutOption IconHeight => GUILayout.Height(EditorGUIUtility.singleLineHeight);

        enum Page { Item, MenuPreview, Issues }
        enum OutfitTab { General, Pieces, Body, Objects, Menus, Variants }

        const int SettingsIndex = -1;
        const int LooksIndex = -2;

        [SerializeField] VRCAvatarDescriptor avatar;
        [SerializeField] int wardrobeIndex;
        [SerializeField] int selected = SettingsIndex;
        [SerializeField] OutfitTab tab;
        [SerializeField] Page page;
        [SerializeField] string newCategory = "Default";

        Vector2 leftScroll, rightScroll;
        ReorderableList list;
        YukiWardrobe listOwner;

        WardrobeSet set;
        List<Conflict> conflicts = new List<Conflict>();
        List<ParameterBudget.Usage> budget = new List<ParameterBudget.Usage>();
        double lastRefresh;
        readonly HashSet<string> folded = new HashSet<string>();

        static string _version;
        static string Version
        {
            get
            {
                if (_version != null) return _version;
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WardrobeWindow).Assembly);
                return _version = info != null ? "v" + info.version : "";
            }
        }

        [MenuItem(YukiMenu.Root + "Wardrobe Editor")]
        public static void ShowWindow() => Open(null);

        public static WardrobeWindow Open(YukiWardrobe config)
        {
            var window = GetWindow<WardrobeWindow>();
            window.titleContent = new GUIContent("Yuki Wardrobe");
            window.minSize = new Vector2(640, 420);
            if (config != null)
            {
                window.avatar = config.GetComponentInParent<VRCAvatarDescriptor>();
                window.Refresh(true);
                window.wardrobeIndex = Mathf.Max(0, window.Wardrobes.IndexOf(config));
            }
            return window;
        }

        void OnEnable()
        {
            YukiLanguage.Changed += OnChanged;
            Undo.undoRedoPerformed += OnChanged;
            Selection.selectionChanged += OnSelectionChanged;
        }

        void OnDisable()
        {
            YukiLanguage.Changed -= OnChanged;
            Undo.undoRedoPerformed -= OnChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            WardrobePreview.Stop();
        }

        void OnChanged()
        {
            lastRefresh = 0;
            Repaint();
        }

        void OnSelectionChanged()
        {
            if (avatar != null || Selection.activeGameObject == null) return;
            var found = Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>();
            if (found != null) { avatar = found; OnChanged(); }
        }

        List<YukiWardrobe> Wardrobes =>
            avatar == null ? new List<YukiWardrobe>() : avatar.GetComponentsInChildren<YukiWardrobe>(true).ToList();

        YukiWardrobe Current
        {
            get
            {
                var all = Wardrobes;
                if (all.Count == 0) return null;
                wardrobeIndex = Mathf.Clamp(wardrobeIndex, 0, all.Count - 1);
                return all[wardrobeIndex];
            }
        }

        void Refresh(bool force = false)
        {
            if (avatar == null) { set = null; return; }
            if (!force && EditorApplication.timeSinceStartup - lastRefresh < 0.5) return;
            lastRefresh = EditorApplication.timeSinceStartup;
            set = WardrobeSet.Resolve(avatar.transform);
            conflicts = ConflictChecker.Check(set);
            budget = ParameterBudget.Measure(avatar);
        }

        // ================================================================ GUI

        void OnGUI()
        {
            YukiGUI.Header("Yuki Wardrobe", Version);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField(L["ui.avatar"], avatar, typeof(VRCAvatarDescriptor), true);
                if (EditorGUI.EndChangeCheck()) { WardrobePreview.Stop(); OnChanged(); }
                if (WardrobePreview.Active && GUILayout.Button(L["ui.stop_preview"], GUILayout.Width(120)))
                    WardrobePreview.Stop();
            }
            if (avatar == null)
            {
                var found = FindObjectsOfType<VRCAvatarDescriptor>().Where(d => d.gameObject.activeInHierarchy).ToArray();
                if (found.Length == 1) avatar = found[0];
            }
            if (avatar == null)
            {
                EditorGUILayout.HelpBox(L["ui.pick_avatar"], MessageType.Info);
                return;
            }

            Refresh();
            DrawWardrobeTabs();
            var config = Current;
            if (config == null)
            {
                EditorGUILayout.HelpBox(L["ui.no_wardrobe"], MessageType.Info);
                if (GUILayout.Button(L["ui.create_wardrobe"], GUILayout.Height(28)))
                {
                    WardrobeActions.CreateWardrobe(avatar, "Wardrobe");
                    OnChanged();
                }
                return;
            }
            var model = set?.For(config);
            DrawBudget();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(270)))
                    DrawLeft(config, model);
                GUILayout.Box(GUIContent.none, GUILayout.Width(1), GUILayout.ExpandHeight(true));
                using (new EditorGUILayout.VerticalScope())
                    DrawRight(config, model);
            }
        }

        void DrawWardrobeTabs()
        {
            var all = Wardrobes;
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var label = WardrobeModel.Fallback(all[i].menuName, all[i].gameObject.name);
                    if (GUILayout.Toggle(i == wardrobeIndex, label, EditorStyles.toolbarButton, GUILayout.MinWidth(80)) && i != wardrobeIndex)
                    {
                        wardrobeIndex = i;
                        selected = SettingsIndex;
                        WardrobePreview.Stop();
                    }
                }
                if (GUILayout.Button(new GUIContent("+", L["ui.add_wardrobe.tip"]), EditorStyles.toolbarButton, GUILayout.Width(28)))
                {
                    var name = all.Count == 0 ? "Wardrobe" : "Wardrobe " + (all.Count + 1);
                    WardrobeActions.CreateWardrobe(avatar, name);
                    wardrobeIndex = all.Count;
                    selected = SettingsIndex;
                    OnChanged();
                }
                GUILayout.FlexibleSpace();
            }
        }

        void DrawBudget()
        {
            var total = budget.Sum(u => u.Bits);
            var rect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.78f, 0.78f, 0.78f));
            float x = rect.x;
            int index = 0;
            foreach (var u in budget)
            {
                var w = rect.width * u.Bits / (float)ParameterBudget.Limit;
                var r = new Rect(x, rect.y, Mathf.Min(w, rect.xMax - x), rect.height);
                EditorGUI.DrawRect(r, BudgetColor(u, index++));
                GUI.Label(r, new GUIContent("", $"{u.Source}: {u.Bits} bit"));
                x += w;
                if (x >= rect.xMax) break;
            }
            var status = total > ParameterBudget.Limit ? YukiStatus.Problem : total > ParameterBudget.Limit * 0.9f ? YukiStatus.Approximate : YukiStatus.None;
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = status == YukiStatus.None ? style.normal.textColor : YukiGUI.StatusColor(status);
            GUI.Label(rect, L.Tr("ui.budget", total, ParameterBudget.Limit), style);
        }

        static Color BudgetColor(ParameterBudget.Usage u, int index)
        {
            if (u.IsWardrobe) return new Color(0.36f, 0.62f, 0.95f, 0.9f);
            var palette = new[] { new Color(0.55f, 0.55f, 0.6f), new Color(0.62f, 0.52f, 0.72f), new Color(0.5f, 0.66f, 0.6f), new Color(0.72f, 0.6f, 0.45f) };
            return palette[index % palette.Length];
        }

        // ------------------------------------------------------------- left

        void DrawLeft(YukiWardrobe config, WardrobeModel model)
        {
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

            if (NavButton(L["ui.wardrobe_settings"], selected == SettingsIndex, EditorGUIUtility.IconContent("_Popup").image))
                Select(SettingsIndex);

            EnsureList(config, model);
            list.DoLayoutList();

            DrawDropArea(config);

            if (NavButton(L.Tr("ui.looks_n", config.looks.Count), selected == LooksIndex, EditorGUIUtility.IconContent("Favorite Icon").image))
                Select(LooksIndex);

            EditorGUILayout.EndScrollView();
        }

        void Select(int index)
        {
            selected = index;
            page = Page.Item;
            GUI.FocusControl(null);
        }

        static bool NavButton(string label, bool active, Texture icon)
        {
            var style = new GUIStyle(EditorStyles.label) { fontStyle = active ? FontStyle.Bold : FontStyle.Normal, padding = new RectOffset(6, 4, 4, 4) };
            var rect = GUILayoutUtility.GetRect(new GUIContent(label), style, GUILayout.Height(24), GUILayout.ExpandWidth(true));
            if (active) EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.59f, 0.6f) : new Color(0.6f, 0.75f, 1f, 0.6f));
            GUI.Label(rect, new GUIContent(" " + label, icon), style);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void EnsureList(YukiWardrobe config, WardrobeModel model)
        {
            if (list != null && listOwner == config) { list.list = config.outfits; return; }
            listOwner = config;
            list = new ReorderableList(config.outfits, typeof(OutfitGroup), true, true, false, true)
            {
                elementHeight = 40,
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, L["ui.outfits_header"], EditorStyles.miniBoldLabel),
                drawElementCallback = (rect, i, active, focused) => DrawOutfitRow(rect, config, i),
                onSelectCallback = l =>
                {
                    Undo.RegisterCompleteObjectUndo(config, "Reorder outfits");
                    Select(l.index);
                },
                onReorderCallbackWithDetails = (l, from, to) =>
                {
                    UndoEdit.End(config);
                    Select(to);
                },
                onRemoveCallback = l =>
                {
                    if (l.index < 0 || l.index >= config.outfits.Count) return;
                    UndoEdit.Begin(config, "Remove outfit");
                    config.outfits.RemoveAt(l.index);
                    UndoEdit.End(config);
                    Select(SettingsIndex);
                },
            };
        }

        void DrawOutfitRow(Rect rect, YukiWardrobe config, int index)
        {
            if (index >= config.outfits.Count) return;
            var group = config.outfits[index];
            var model = set?.For(config);
            var resolved = model?.Outfits.FirstOrDefault(o => o.Source == group);

            if (selected == index)
                EditorGUI.DrawRect(new Rect(rect.x - 18, rect.y, rect.width + 22, rect.height), EditorGUIUtility.isProSkin ? new Color(0.24f, 0.37f, 0.59f, 0.35f) : new Color(0.6f, 0.75f, 1f, 0.35f));

            var iconRect = new Rect(rect.x, rect.y + 4, 32, 32);
            var icon = group.icon != null ? (Texture)group.icon : EditorGUIUtility.IconContent("Prefab Icon").image;
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);

            var name = group.root == null ? L["ui.missing_object"] : WardrobeModel.Fallback(group.displayName, group.root.name);
            GUI.Label(new Rect(iconRect.xMax + 6, rect.y + 3, rect.width - 80, 18), name, EditorStyles.boldLabel);

            var badges = new List<string>();
            if (index == 0) badges.Add(L["ui.badge.default"]);
            if (!string.IsNullOrWhiteSpace(group.category) && group.category != "Default") badges.Add(group.category);
            if (group.toggleablePieces.Count > 0) badges.Add(L.Tr("ui.badge.pieces", group.toggleablePieces.Count));
            if (resolved != null && resolved.Menus.Count > 0) badges.Add(L.Tr("ui.badge.menus", resolved.Menus.Count));
            if (group.variants.Count > 1) badges.Add(L.Tr("ui.badge.variants", group.variants.Count));
            if (group.platform != OutfitPlatform.All) badges.Add(L["ui.platform." + group.platform]);
            GUI.Label(new Rect(iconRect.xMax + 6, rect.y + 21, rect.width - 80, 16), string.Join(" · ", badges), EditorStyles.miniLabel);

            var showing = WardrobePreview.IsShowing(config, group.root);
            var tryRect = new Rect(rect.xMax - 34, rect.y + 9, 32, 22);
            if (group.root != null && GUI.Button(tryRect, new GUIContent(showing ? "■" : "▶", L["ui.try_on.tip"]), EditorStyles.miniButton))
            {
                if (showing) WardrobePreview.Stop();
                else WardrobePreview.Show(avatar.transform, config, group.root);
            }
        }

        void DrawDropArea(YukiWardrobe config)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(L["ui.category"], GUILayout.Width(60));
                newCategory = EditorGUILayout.TextField(newCategory);
            }
            var rect = GUILayoutUtility.GetRect(0, 44, GUILayout.ExpandWidth(true));
            GUI.Box(rect, L["ui.drop_here"], new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
            var e = Event.current;
            if ((e.type == EventType.DragUpdated || e.type == EventType.DragPerform) && rect.Contains(e.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (e.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (var go in DragAndDrop.objectReferences.OfType<GameObject>())
                        WardrobeActions.AddOutfit(config, avatar, go, newCategory);
                    Select(config.outfits.Count - 1);
                    OnChanged();
                }
                e.Use();
            }
            if (GUILayout.Button(L["ui.add_selected"]))
            {
                foreach (var go in Selection.gameObjects)
                    WardrobeActions.AddOutfit(config, avatar, go, newCategory);
                OnChanged();
            }
        }

        // ------------------------------------------------------------ right

        void DrawRight(YukiWardrobe config, WardrobeModel model)
        {
            var issueCount = (model?.Warnings.Count ?? 0) + conflicts.Count;
            var pages = new[] { L["ui.page.item"], L["ui.page.menu"], issueCount > 0 ? L.Tr("ui.page.issues_n", issueCount) : L["ui.page.issues"] };
            page = (Page)GUILayout.Toolbar((int)page, pages, EditorStyles.toolbarButton);

            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            switch (page)
            {
                case Page.MenuPreview: DrawMenuPreview(config, model); break;
                case Page.Issues: DrawIssues(model); break;
                default:
                    if (selected == SettingsIndex) DrawSettings(config, model);
                    else if (selected == LooksIndex) DrawLooks(config, model);
                    else if (selected >= 0 && selected < config.outfits.Count) DrawOutfit(config, model, config.outfits[selected]);
                    else DrawSettings(config, model);
                    break;
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------------------------------------------- wardrobe settings

        void DrawSettings(YukiWardrobe config, WardrobeModel model)
        {
            YukiGUI.Section(L["ui.wardrobe_settings"]);
            EditorGUI.BeginChangeCheck();
            var objectName = EditorGUILayout.DelayedTextField(new GUIContent(L["ui.object_name"], L["ui.object_name.tip"]), config.gameObject.name);
            var menuName = EditorGUILayout.TextField(new GUIContent(L["ui.menu_name"], L["ui.menu_name.tip"]), config.menuName);
            var menuIcon = (Texture2D)EditorGUILayout.ObjectField(L["ui.icon"], config.menuIcon, typeof(Texture2D), false, IconHeight);
            var parameter = EditorGUILayout.TextField(new GUIContent(L["ui.parameter"], L["ui.parameter.tip"]), config.parameterName);
            if (string.IsNullOrWhiteSpace(config.parameterName) && model != null)
                EditorGUILayout.LabelField(" ", L.Tr("ui.parameter_auto", model.ParameterName), EditorStyles.miniLabel);
            var saved = EditorGUILayout.Toggle(new GUIContent(L["ui.saved"], L["ui.saved.tip"]), config.saved);
            var none = EditorGUILayout.Toggle(new GUIContent(L["ui.none"], L["ui.none.tip"]), config.includeNone);
            string noneLabel = config.noneLabel; Texture2D noneIcon = config.noneIcon;
            if (none)
            {
                EditorGUI.indentLevel++;
                noneLabel = EditorGUILayout.TextField(L["ui.none_label"], config.noneLabel);
                noneIcon = (Texture2D)EditorGUILayout.ObjectField(L["ui.icon"], config.noneIcon, typeof(Texture2D), false, IconHeight);
                EditorGUI.indentLevel--;
            }

            YukiGUI.Section(L["ui.effect"]);
            EditorGUILayout.LabelField(L["ui.effect.help"], YukiGUI.WrapMini);
            var effect = (GameObject)EditorGUILayout.ObjectField(L["ui.effect_object"], config.changeEffect, typeof(GameObject), true);
            var duration = EditorGUILayout.Slider(L["ui.effect_duration"], config.changeEffectDuration, 0.1f, 5f);

            if (EditorGUI.EndChangeCheck())
            {
                if (objectName != config.gameObject.name && !string.IsNullOrWhiteSpace(objectName))
                {
                    Undo.RecordObject(config.gameObject, "Rename wardrobe");
                    config.gameObject.name = objectName.Trim();
                }
                UndoEdit.Begin(config, "Edit wardrobe settings");
                config.menuName = menuName;
                config.menuIcon = menuIcon;
                config.parameterName = parameter;
                config.saved = saved;
                config.includeNone = none;
                config.noneLabel = noneLabel;
                config.noneIcon = noneIcon;
                config.changeEffect = effect;
                config.changeEffectDuration = duration;
                UndoEdit.End(config);
                OnChanged();
            }

            if (model != null)
            {
                YukiGUI.Section(L["ui.summary"]);
                EditorGUILayout.LabelField(L.Tr("ui.summary_text", model.Outfits.Count, model.AllElements.Count(), model.Looks.Count, model.TotalBits), YukiGUI.WrapMini);
            }

            EditorGUILayout.Space(12);
            if (GUILayout.Button(L["ui.select_object"], GUILayout.Width(200)))
                EditorGUIUtility.PingObject(Selection.activeObject = config.gameObject);
        }

        // ------------------------------------------------------------ outfit

        void DrawOutfit(YukiWardrobe config, WardrobeModel model, OutfitGroup group)
        {
            var resolved = model?.Outfits.FirstOrDefault(o => o.Source == group);
            using (new EditorGUILayout.HorizontalScope())
            {
                var icon = group.icon != null ? (Texture)group.icon : EditorGUIUtility.IconContent("Prefab Icon").image;
                GUILayout.Label(icon, GUILayout.Width(48), GUILayout.Height(48));
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label(group.root == null ? L["ui.missing_object"] : WardrobeModel.Fallback(group.displayName, group.root.name), YukiGUI.TitleStyle);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var showing = WardrobePreview.IsShowing(config, group.root);
                        if (group.root != null && GUILayout.Button(showing ? L["ui.stop_preview"] : L["ui.try_on"], GUILayout.Width(110)))
                        {
                            if (showing) WardrobePreview.Stop();
                            else WardrobePreview.Show(avatar.transform, config, group.root);
                        }
                        if (resolved != null && GUILayout.Button(L["ui.make_icon"], GUILayout.Width(110)))
                        {
                            WardrobePreview.Stop();
                            var tex = WardrobeIcons.Capture(avatar.transform, model, resolved);
                            if (tex != null)
                            {
                                UndoEdit.Begin(config, "Outfit icon");
                                group.icon = tex;
                                UndoEdit.End(config);
                            }
                        }
                        if (group.root != null && GUILayout.Button(L["ui.select_object"], GUILayout.Width(110)))
                            EditorGUIUtility.PingObject(Selection.activeObject = group.root);
                    }
                }
            }

            var tabs = System.Enum.GetValues(typeof(OutfitTab)).Cast<OutfitTab>().Select(t => new GUIContent(TabLabel(t, group, resolved))).ToArray();
            tab = (OutfitTab)GUILayout.Toolbar((int)tab, tabs);
            EditorGUILayout.Space(4);

            switch (tab)
            {
                case OutfitTab.General: DrawGeneral(config, group); break;
                case OutfitTab.Pieces: DrawPieces(config, group); break;
                case OutfitTab.Body: DrawBody(config, group, model); break;
                case OutfitTab.Objects: DrawObjects(config, group); break;
                case OutfitTab.Menus: DrawMenus(config, group, resolved); break;
                case OutfitTab.Variants: DrawVariants(config, group); break;
            }
        }

        string TabLabel(OutfitTab t, OutfitGroup g, ResolvedOutfit r)
        {
            var label = L["ui.tab." + t];
            int n = 0;
            switch (t)
            {
                case OutfitTab.Pieces: n = g.toggleablePieces.Count; break;
                case OutfitTab.Body: n = g.blendshapes.Count; break;
                case OutfitTab.Objects: n = g.objectOverrides.Count; break;
                case OutfitTab.Menus: n = r?.Menus.Count ?? 0; break;
                case OutfitTab.Variants: n = g.variants.Count; break;
            }
            return n > 0 ? $"{label} ({n})" : label;
        }

        void DrawGeneral(YukiWardrobe config, OutfitGroup group)
        {
            EditorGUI.BeginChangeCheck();
            var root = (GameObject)EditorGUILayout.ObjectField(new GUIContent(L["ui.outfit_root"], L["ui.outfit_root.tip"]), group.root, typeof(GameObject), true);
            var display = EditorGUILayout.TextField(new GUIContent(L["ui.display_name"], L["ui.display_name.tip"]), group.displayName);
            var icon = (Texture2D)EditorGUILayout.ObjectField(L["ui.icon"], group.icon, typeof(Texture2D), false, IconHeight);
            var category = EditorGUILayout.TextField(new GUIContent(L["ui.category"], L["ui.category.tip"]), group.category);
            var platformNames = System.Enum.GetNames(typeof(OutfitPlatform)).Select(n => L["ui.platform." + n]).ToArray();
            var platform = (OutfitPlatform)EditorGUILayout.Popup(new GUIContent(L["ui.platform"], L["ui.platform.tip"]), (int)group.platform, platformNames);
            if (EditorGUI.EndChangeCheck())
            {
                UndoEdit.Begin(config, "Edit outfit");
                if (root != group.root)
                {
                    group.root = root;
                    group.toggleablePieces.RemoveAll(p => p == null || root == null || p.transform.parent != root.transform);
                }
                group.displayName = display;
                group.icon = icon;
                group.category = category;
                group.platform = platform;
                UndoEdit.End(config);
                OnChanged();
            }
            if (config.outfits.IndexOf(group) == 0)
                EditorGUILayout.HelpBox(L["ui.default_outfit.help"], MessageType.None);
        }

        void DrawPieces(YukiWardrobe config, OutfitGroup group)
        {
            if (group.root == null) return;
            EditorGUILayout.LabelField(L["ui.pieces.help"], YukiGUI.WrapMini);
            EditorGUILayout.Space(2);
            foreach (Transform child in group.root.transform)
            {
                var go = child.gameObject;
                bool on = group.toggleablePieces.Contains(go);
                var settings = group.FindPiece(go);
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool now = EditorGUILayout.ToggleLeft(child.name, on, GUILayout.Width(180));
                    if (now != on)
                    {
                        UndoEdit.Begin(config, "Edit toggleable pieces");
                        if (now) group.toggleablePieces.Add(go);
                        else group.toggleablePieces.Remove(go);
                        UndoEdit.End(config);
                        OnChanged();
                    }
                    using (new EditorGUI.DisabledScope(!now))
                    {
                        EditorGUI.BeginChangeCheck();
                        var name = EditorGUILayout.TextField(settings?.displayName ?? "");
                        var icon = (Texture2D)EditorGUILayout.ObjectField(settings?.icon, typeof(Texture2D), false, GUILayout.Width(110), IconHeight);
                        if (EditorGUI.EndChangeCheck())
                        {
                            UndoEdit.Begin(config, "Edit piece");
                            if (settings == null) group.pieceSettings.Add(settings = new PieceSettings { target = go });
                            settings.displayName = name;
                            settings.icon = icon;
                            UndoEdit.End(config);
                        }
                        GUILayout.Label(go.activeSelf ? L["ui.default_on"] : L["ui.default_off"], EditorStyles.miniLabel, GUILayout.Width(60));
                    }
                }
            }
        }

        void DrawBody(YukiWardrobe config, OutfitGroup group, WardrobeModel model)
        {
            EditorGUILayout.LabelField(L["ui.body.help"], YukiGUI.WrapMini);
            BlendshapeOverride remove = null;
            foreach (var over in group.blendshapes)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(over.renderer, typeof(SkinnedMeshRenderer), true, GUILayout.Width(150));
                    var shape = BlendshapePopup(renderer, over.blendshape, GUILayout.Width(170));
                    var value = EditorGUILayout.Slider(over.value, 0, 100);
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoEdit.Begin(config, "Edit blendshape override");
                        over.renderer = renderer;
                        over.blendshape = shape;
                        over.value = value;
                        UndoEdit.End(config);
                        OnChanged();
                    }
                    if (GUILayout.Button("×", GUILayout.Width(22))) remove = over;
                }
            }
            if (remove != null)
            {
                UndoEdit.Begin(config, "Remove blendshape override");
                group.blendshapes.Remove(remove);
                UndoEdit.End(config);
                OnChanged();
            }
            if (GUILayout.Button(L["ui.add"], GUILayout.Width(80)))
            {
                UndoEdit.Begin(config, "Add blendshape override");
                var body = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Body" || r.name == "Body_base");
                group.blendshapes.Add(new BlendshapeOverride { renderer = body, value = 100 });
                UndoEdit.End(config);
            }

            if (model == null) return;
            var candidates = WardrobeActions.ShrinkCandidates(avatar.transform, model)
                .Where(c => !group.blendshapes.Any(b => b.renderer == c.renderer && b.blendshape == c.shape)).ToList();
            if (candidates.Count == 0) return;
            YukiGUI.Section(L["ui.suggestions"]);
            EditorGUILayout.LabelField(L["ui.suggestions.help"], YukiGUI.WrapMini);
            foreach (var (renderer, shape) in candidates.Take(40))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"{renderer.name} / {shape}", EditorStyles.miniLabel);
                    if (GUILayout.Button(L["ui.add"], EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        UndoEdit.Begin(config, "Add blendshape override");
                        group.blendshapes.Add(new BlendshapeOverride { renderer = renderer, blendshape = shape, value = 100 });
                        UndoEdit.End(config);
                        OnChanged();
                    }
                }
            }
        }

        void DrawObjects(YukiWardrobe config, OutfitGroup group)
        {
            EditorGUILayout.LabelField(L["ui.objects.help"], YukiGUI.WrapMini);
            ObjectOverride remove = null;
            foreach (var over in group.objectOverrides)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var target = (GameObject)EditorGUILayout.ObjectField(over.target, typeof(GameObject), true);
                    var enable = GUILayout.Toggle(over.enable, over.enable ? L["ui.on"] : L["ui.off"], "Button", GUILayout.Width(60));
                    if (EditorGUI.EndChangeCheck())
                    {
                        UndoEdit.Begin(config, "Edit object override");
                        over.target = target;
                        over.enable = enable;
                        UndoEdit.End(config);
                        OnChanged();
                    }
                    if (GUILayout.Button("×", GUILayout.Width(22))) remove = over;
                }
            }
            if (remove != null)
            {
                UndoEdit.Begin(config, "Remove object override");
                group.objectOverrides.Remove(remove);
                UndoEdit.End(config);
                OnChanged();
            }
            if (GUILayout.Button(L["ui.add"], GUILayout.Width(80)))
            {
                UndoEdit.Begin(config, "Add object override");
                group.objectOverrides.Add(new ObjectOverride());
                UndoEdit.End(config);
            }
        }

        void DrawMenus(YukiWardrobe config, OutfitGroup group, ResolvedOutfit resolved)
        {
            EditorGUILayout.LabelField(L["ui.menus.help"], YukiGUI.WrapMini);
            EditorGUI.BeginChangeCheck();
            var modeNames = System.Enum.GetNames(typeof(OutfitMenuMode)).Select(n => L["ui.menu_mode." + n]).ToArray();
            var mode = (OutfitMenuMode)EditorGUILayout.Popup(L["ui.menu_mode"], (int)group.menuMode, modeNames);
            if (EditorGUI.EndChangeCheck())
            {
                UndoEdit.Begin(config, "Outfit menu mode");
                group.menuMode = mode;
                UndoEdit.End(config);
                OnChanged();
            }

            if (resolved == null || resolved.Menus.Count == 0)
            {
                EditorGUILayout.HelpBox(L["ui.menus.none"], MessageType.None);
                return;
            }
            foreach (var menu in resolved.Menus)
            {
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(menu.Source == OutfitMenuSource.VRCFury ? "VRCFury" : "Modular Avatar", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                        if (GUILayout.Button(L["ui.select_object"], EditorStyles.miniButton, GUILayout.Width(90)))
                            EditorGUIUtility.PingObject(Selection.activeObject = menu.Component.gameObject);
                    }
                    if (!menu.CanAbsorb) EditorGUILayout.LabelField(L["ui.menus.vrcfury"], YukiGUI.WrapMini);
                    DrawNode(menu.Root, 0, "m" + menu.Component.GetInstanceID());
                }
            }

            var convertible = WardrobeActions.ConvertibleToggles(group);
            if (convertible.Count > 0)
            {
                YukiGUI.Section(L["ui.convert"]);
                EditorGUILayout.LabelField(L.Tr("ui.convert.help", string.Join(", ", convertible.Select(c => c.target.name))), YukiGUI.WrapMini);
                if (GUILayout.Button(L["ui.convert.button"], GUILayout.Width(220)) &&
                    EditorUtility.DisplayDialog("Yuki Wardrobe", L["ui.convert.confirm"], L["ui.ok"], L["ui.cancel"]))
                {
                    WardrobeActions.ConvertToPieces(config, group, convertible);
                    OnChanged();
                }
            }
        }

        static GUIStyle _nodeStyle;
        static GUIStyle NodeStyle => _nodeStyle ??= new GUIStyle(EditorStyles.label) { richText = true };

        void DrawNode(MenuNode node, int depth, string key)
        {
            var type = node.Type.ToString();
            var detail = $"{type}{(string.IsNullOrEmpty(node.Parameter) ? "" : "  " + node.Parameter)}{(node.IsDefault == true ? "  " + L["ui.default_on"] : "")}";
            var text = $"{node.Label}  <color=#8a8a8a>{detail}</color>";
            var rect = GUILayoutUtility.GetRect(new GUIContent(text), NodeStyle, GUILayout.ExpandWidth(true));
            rect.xMin += depth * 16;
            var arrow = new Rect(rect.x, rect.y, 14, rect.height);
            var label = new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height);
            bool open = !folded.Contains(key);
            if (node.Children.Count > 0)
            {
                var now = EditorGUI.Foldout(arrow, open, GUIContent.none, true);
                if (now != open) { if (now) folded.Remove(key); else folded.Add(key); open = now; }
            }
            GUI.Label(label, text, NodeStyle);
            if (node.Toggles.Count > 0)
            {
                var t = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                t.xMin += depth * 16 + 30;
                GUI.Label(t, string.Join(", ", node.Toggles.Select(x => (x.active ? "+" : "−") + x.target.name)), EditorStyles.miniLabel);
            }
            if (!open) return;
            for (int i = 0; i < node.Children.Count; i++)
                DrawNode(node.Children[i], depth + 1, key + "/" + i);
        }

        void DrawVariants(YukiWardrobe config, OutfitGroup group)
        {
            EditorGUILayout.LabelField(L["ui.variants.help"], YukiGUI.WrapMini);
            OutfitVariant removeVariant = null;
            for (int v = 0; v < group.variants.Count; v++)
            {
                var variant = group.variants[v];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        var name = EditorGUILayout.TextField(variant.displayName);
                        var icon = (Texture2D)EditorGUILayout.ObjectField(variant.icon, typeof(Texture2D), false, GUILayout.Width(110), IconHeight);
                        if (EditorGUI.EndChangeCheck())
                        {
                            UndoEdit.Begin(config, "Edit variant");
                            variant.displayName = name;
                            variant.icon = icon;
                            UndoEdit.End(config);
                        }
                        if (v == 0) GUILayout.Label(L["ui.badge.default"], EditorStyles.miniBoldLabel, GUILayout.Width(50));
                        if (group.root != null && GUILayout.Button(L["ui.try_on"], EditorStyles.miniButton, GUILayout.Width(70)))
                            WardrobePreview.Show(avatar.transform, config, group.root, v);
                        if (GUILayout.Button("×", GUILayout.Width(22))) removeVariant = variant;
                    }

                    MaterialSlotOverride removeSlot = null;
                    foreach (var slot in variant.materials)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUI.BeginChangeCheck();
                            var renderer = (Renderer)EditorGUILayout.ObjectField(slot.renderer, typeof(Renderer), true, GUILayout.Width(150));
                            var count = renderer != null ? renderer.sharedMaterials.Length : 0;
                            var slotIndex = EditorGUILayout.Popup(Mathf.Clamp(slot.slot, 0, Mathf.Max(0, count - 1)),
                                Enumerable.Range(0, Mathf.Max(1, count)).Select(i => renderer != null && i < count && renderer.sharedMaterials[i] != null ? $"{i}: {renderer.sharedMaterials[i].name}" : i.ToString()).ToArray(), GUILayout.Width(150));
                            var material = (Material)EditorGUILayout.ObjectField(slot.material, typeof(Material), false);
                            if (EditorGUI.EndChangeCheck())
                            {
                                UndoEdit.Begin(config, "Edit variant material");
                                slot.renderer = renderer;
                                slot.slot = slotIndex;
                                slot.material = material;
                                UndoEdit.End(config);
                            }
                            if (GUILayout.Button("×", GUILayout.Width(22))) removeSlot = slot;
                        }
                    }
                    if (removeSlot != null)
                    {
                        UndoEdit.Begin(config, "Remove variant material");
                        variant.materials.Remove(removeSlot);
                        UndoEdit.End(config);
                    }
                    if (GUILayout.Button(L["ui.add_material"], EditorStyles.miniButton, GUILayout.Width(120)))
                    {
                        UndoEdit.Begin(config, "Add variant material");
                        variant.materials.Add(new MaterialSlotOverride { renderer = group.root != null ? group.root.GetComponentInChildren<Renderer>(true) : null });
                        UndoEdit.End(config);
                    }
                }
            }
            if (removeVariant != null)
            {
                UndoEdit.Begin(config, "Remove variant");
                group.variants.Remove(removeVariant);
                UndoEdit.End(config);
                OnChanged();
            }
            if (GUILayout.Button(L["ui.add_variant"], GUILayout.Width(120)))
            {
                UndoEdit.Begin(config, "Add variant");
                group.variants.Add(new OutfitVariant { displayName = group.variants.Count == 0 ? L["ui.variant_original"] : "" });
                UndoEdit.End(config);
                OnChanged();
            }
        }

        // ------------------------------------------------------------- looks

        void DrawLooks(YukiWardrobe config, WardrobeModel model)
        {
            YukiGUI.Section(L["ui.looks"]);
            EditorGUILayout.LabelField(L["ui.looks.help"], YukiGUI.WrapMini);
            var outfitRoots = config.outfits.Where(o => o.root != null).Select(o => o.root).ToList();
            var outfitNames = config.outfits.Where(o => o.root != null).Select(o => WardrobeModel.Fallback(o.displayName, o.root.name)).ToArray();

            WardrobeLook remove = null;
            foreach (var look in config.looks)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUI.BeginChangeCheck();
                        var name = EditorGUILayout.TextField(look.displayName);
                        var icon = (Texture2D)EditorGUILayout.ObjectField(look.icon, typeof(Texture2D), false, GUILayout.Width(110), IconHeight);
                        var index = EditorGUILayout.Popup(Mathf.Max(0, outfitRoots.IndexOf(look.outfit)), outfitNames, GUILayout.Width(140));
                        if (EditorGUI.EndChangeCheck())
                        {
                            UndoEdit.Begin(config, "Edit look");
                            look.displayName = name;
                            look.icon = icon;
                            if (index >= 0 && index < outfitRoots.Count && look.outfit != outfitRoots[index])
                            {
                                look.outfit = outfitRoots[index];
                                look.pieces.Clear();
                            }
                            UndoEdit.End(config);
                            OnChanged();
                        }
                        if (GUILayout.Button("×", GUILayout.Width(22))) remove = look;
                    }
                    var group = config.outfits.FirstOrDefault(o => o.root == look.outfit);
                    if (group == null) continue;
                    foreach (var piece in group.toggleablePieces.Where(p => p != null))
                    {
                        var state = look.pieces.FirstOrDefault(p => p.piece == piece);
                        bool on = state != null ? state.on : piece.activeSelf;
                        bool now = EditorGUILayout.ToggleLeft(WardrobeModel.Fallback(group.FindPiece(piece)?.displayName, piece.name), on);
                        if (now != on)
                        {
                            UndoEdit.Begin(config, "Edit look");
                            if (state == null) look.pieces.Add(state = new LookPiece { piece = piece });
                            state.on = now;
                            UndoEdit.End(config);
                        }
                    }
                }
            }
            if (remove != null)
            {
                UndoEdit.Begin(config, "Remove look");
                config.looks.Remove(remove);
                UndoEdit.End(config);
                OnChanged();
            }
            using (new EditorGUI.DisabledScope(outfitRoots.Count == 0))
                if (GUILayout.Button(L["ui.add_look"], GUILayout.Width(120)))
                {
                    UndoEdit.Begin(config, "Add look");
                    config.looks.Add(new WardrobeLook { outfit = outfitRoots.FirstOrDefault(), displayName = L.Tr("ui.look_n", config.looks.Count + 1) });
                    UndoEdit.End(config);
                    OnChanged();
                }
        }

        // ----------------------------------------------------- menu preview

        void DrawMenuPreview(YukiWardrobe config, WardrobeModel model)
        {
            EditorGUILayout.LabelField(L["ui.menu_preview.help"], YukiGUI.WrapMini);
            if (set == null) return;
            foreach (var m in set.Models)
            {
                EditorGUILayout.Space(4);
                var root = BuildPreviewTree(m);
                DrawNode(root, 0, "p" + m.Config.GetInstanceID());
            }
        }

        static MenuNode BuildPreviewTree(WardrobeModel model)
        {
            var root = new MenuNode { Label = "▣ " + model.MenuName, Type = VRCExpressionsMenu.Control.ControlType.SubMenu };
            if (model.IncludeNone) root.Children.Add(Toggle(model.NoneLabel, model.ParameterName, model.NoneIndex));
            foreach (var outfit in model.Outfits.Where(o => MenuGenerator.IsDefaultCategory(o.Category)))
                root.Children.Add(OutfitNode(outfit, model));
            foreach (var category in model.Outfits.Where(o => !MenuGenerator.IsDefaultCategory(o.Category)).GroupBy(o => o.Category))
            {
                var folder = new MenuNode { Label = category.Key, Type = VRCExpressionsMenu.Control.ControlType.SubMenu };
                foreach (var outfit in category) folder.Children.Add(OutfitNode(outfit, model));
                root.Children.Add(folder);
            }
            if (model.Looks.Count > 0)
            {
                var looks = new MenuNode { Label = WardrobeText.L["menu.looks"], Type = VRCExpressionsMenu.Control.ControlType.SubMenu };
                foreach (var look in model.Looks)
                    looks.Children.Add(new MenuNode { Label = look.DisplayName, Type = VRCExpressionsMenu.Control.ControlType.Button, Parameter = $"{model.LookParameter} = {look.Value}" });
                root.Children.Add(looks);
            }
            return root;
        }

        static MenuNode OutfitNode(ResolvedOutfit outfit, WardrobeModel model)
        {
            if (!outfit.HasSubmenu) return Toggle(outfit.DisplayName, model.ParameterName, outfit.Index);
            var node = new MenuNode { Label = outfit.DisplayName, Type = VRCExpressionsMenu.Control.ControlType.SubMenu };
            node.Children.Add(Toggle(WardrobeText.L["menu.wear"], model.ParameterName, outfit.Index));
            foreach (var e in outfit.Elements)
                node.Children.Add(new MenuNode { Label = e.DisplayName, Type = VRCExpressionsMenu.Control.ControlType.Toggle, Parameter = e.ParameterName, IsDefault = e.DefaultOn });
            if (outfit.VariantParameter != null)
            {
                var colors = new MenuNode { Label = WardrobeText.L["menu.variants"], Type = VRCExpressionsMenu.Control.ControlType.SubMenu };
                foreach (var v in outfit.Variants) colors.Children.Add(Toggle(v.DisplayName, outfit.VariantParameter, v.Index));
                node.Children.Add(colors);
            }
            if (outfit.MenuMode == OutfitMenuMode.Absorb)
                foreach (var m in outfit.Menus.Where(m => m.CanAbsorb))
                    node.Children.Add(m.Root);
            return node;
        }

        static MenuNode Toggle(string label, string parameter, int value) =>
            new MenuNode { Label = label, Type = VRCExpressionsMenu.Control.ControlType.Toggle, Parameter = $"{parameter} = {value}" };

        // ------------------------------------------------------------ issues

        void DrawIssues(WardrobeModel model)
        {
            var any = false;
            if (set != null)
                foreach (var m in set.Models)
                    foreach (var w in m.Warnings)
                    {
                        any = true;
                        IssueRow(MessageType.Warning, w.Message, w.Context);
                    }
            foreach (var c in conflicts)
            {
                any = true;
                IssueRow(MessageType.Info, c.Message, c.Objects.FirstOrDefault());
            }
            if (!any) EditorGUILayout.HelpBox(L["ui.no_issues"], MessageType.None);
        }

        static void IssueRow(MessageType type, string message, Object context)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox(message, type);
                if (context != null && GUILayout.Button(L["ui.select_object"], GUILayout.Width(80), GUILayout.Height(38)))
                    EditorGUIUtility.PingObject(Selection.activeObject = context);
            }
        }

        // ----------------------------------------------------------- helpers

        static string BlendshapePopup(SkinnedMeshRenderer renderer, string current, params GUILayoutOption[] options)
        {
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
            {
                GUILayout.Label(L["ui.no_blendshapes"], EditorStyles.miniLabel, options);
                return current;
            }
            var names = new List<string>(mesh.blendShapeCount);
            for (int i = 0; i < mesh.blendShapeCount; i++) names.Add(mesh.GetBlendShapeName(i));
            int index = names.IndexOf(current);
            bool placeholder = index < 0;
            if (placeholder)
            {
                names.Insert(0, string.IsNullOrEmpty(current) ? L["ui.select"] : current + " (" + L["ui.missing"] + ")");
                index = 0;
            }
            int chosen = EditorGUILayout.Popup(index, names.ToArray(), options);
            if (placeholder && chosen == 0) return current;
            return names[chosen];
        }
    }
}
