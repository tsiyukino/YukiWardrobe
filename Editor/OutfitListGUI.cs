using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace TsiYuki.Wardrobe.Editor
{
    // Reorderable outfit list bound to one YukiWardrobe component. Order is
    // meaning: the top row is the default outfit. Rows expand to piece
    // checkboxes and blendshape overrides. All edits go through UndoEdit.
    class OutfitListGUI
    {
        readonly YukiWardrobe config;
        readonly ReorderableList list;
        readonly HashSet<OutfitGroup> expanded = new HashSet<OutfitGroup>();

        static float LineH => EditorGUIUtility.singleLineHeight + 3;

        public OutfitListGUI(YukiWardrobe config)
        {
            this.config = config;
            list = new ReorderableList(config.outfits, typeof(OutfitGroup), true, true, false, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Outfits — top one is the default; drag to reorder"),
                elementHeightCallback = i => RowHeight(config.outfits[i]),
                drawElementCallback = (rect, i, active, focused) => DrawRow(rect, config.outfits[i], i),
                onSelectCallback = l => Undo.RegisterCompleteObjectUndo(config, "Reorder outfits"),
                onReorderCallback = l => UndoEdit.End(config),
                onRemoveCallback = l =>
                {
                    UndoEdit.Begin(config, "Remove outfit");
                    config.outfits.RemoveAt(l.index);
                    UndoEdit.End(config);
                },
            };
        }

        public bool Owns(YukiWardrobe candidate) => candidate == config;

        public void Draw()
        {
            list.list = config.outfits;
            list.DoLayoutList();
        }

        float RowHeight(OutfitGroup outfit)
        {
            float height = LineH + 3;
            if (outfit.root == null || !expanded.Contains(outfit)) return height;
            height += LineH * (outfit.root.transform.childCount + 1); // header + piece checkboxes
            height += LineH * (outfit.blendshapes.Count + 1);         // header + override rows
            height += LineH * (outfit.objectOverrides.Count + 1);     // header + object-override rows
            return height;
        }

        void DrawRow(Rect rect, OutfitGroup outfit, int index)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + 2;
            const float defaultWidth = 50, foldWidth = 44, categoryWidth = 76, pad = 4;

            var objRect = new Rect(rect.x, y, rect.width - categoryWidth - foldWidth - defaultWidth - pad * 3, line);
            var catRect = new Rect(objRect.xMax + pad, y, categoryWidth, line);
            var foldRect = new Rect(catRect.xMax + pad + 12, y, foldWidth, line);
            var defRect = new Rect(foldRect.xMax + pad, y, defaultWidth, line);

            EditorGUI.BeginChangeCheck();
            var root = (GameObject)EditorGUI.ObjectField(objRect, outfit.root, typeof(GameObject), true);
            string category = EditorGUI.TextField(catRect, outfit.category);
            if (EditorGUI.EndChangeCheck())
            {
                UndoEdit.Begin(config, "Edit outfit");
                if (root != outfit.root)
                {
                    outfit.root = root;
                    outfit.toggleablePieces.RemoveAll(p => p == null || root == null || p.transform.parent != root.transform);
                }
                outfit.category = category;
                UndoEdit.End(config);
            }

            bool open = EditorGUI.Foldout(foldRect, expanded.Contains(outfit), "Edit", true);
            if (open) expanded.Add(outfit); else expanded.Remove(outfit);

            if (index == 0)
                EditorGUI.LabelField(defRect, "Default", EditorStyles.miniBoldLabel);

            if (!open || outfit.root == null) return;

            y += LineH + 1;
            DrawPieces(rect, outfit, ref y);
            DrawBlendshapes(rect, outfit, ref y);
            DrawObjectOverrides(rect, outfit, ref y);
        }

        void DrawPieces(Rect rect, OutfitGroup outfit, ref float y)
        {
            float line = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x + 18, y, rect.width - 18, line), "Toggleable pieces", EditorStyles.miniBoldLabel);
            y += LineH;

            foreach (Transform child in outfit.root.transform)
            {
                var pieceRect = new Rect(rect.x + 18, y, rect.width - 18, line);
                bool on = outfit.toggleablePieces.Contains(child.gameObject);
                bool now = EditorGUI.ToggleLeft(pieceRect, child.name, on);
                if (now != on)
                {
                    UndoEdit.Begin(config, "Edit toggleable pieces");
                    if (now) outfit.toggleablePieces.Add(child.gameObject);
                    else outfit.toggleablePieces.Remove(child.gameObject);
                    UndoEdit.End(config);
                }
                y += LineH;
            }
        }

        void DrawBlendshapes(Rect rect, OutfitGroup outfit, ref float y)
        {
            float line = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x + 18, y, 150, line), "Blendshapes", EditorStyles.miniBoldLabel);
            if (GUI.Button(new Rect(rect.xMax - 44, y, 44, line), "Add"))
            {
                UndoEdit.Begin(config, "Add blendshape override");
                outfit.blendshapes.Add(new BlendshapeOverride());
                UndoEdit.End(config);
            }
            y += LineH;

            BlendshapeOverride remove = null;
            foreach (var over in outfit.blendshapes)
            {
                float x = rect.x + 18;
                float width = rect.xMax - x;
                var rendRect = new Rect(x, y, width * 0.34f, line);
                var nameRect = new Rect(rendRect.xMax + 4, y, width * 0.28f, line);
                var removeRect = new Rect(rect.xMax - 20, y, 20, line);
                var valueRect = new Rect(nameRect.xMax + 4, y, removeRect.x - nameRect.xMax - 8, line);

                EditorGUI.BeginChangeCheck();
                var renderer = (SkinnedMeshRenderer)EditorGUI.ObjectField(rendRect, over.renderer, typeof(SkinnedMeshRenderer), true);
                string blendshape = BlendshapePopup(nameRect, renderer, over.blendshape);
                float value = EditorGUI.Slider(valueRect, over.value, 0f, 100f);
                if (EditorGUI.EndChangeCheck())
                {
                    UndoEdit.Begin(config, "Edit blendshape override");
                    over.renderer = renderer;
                    over.blendshape = blendshape;
                    over.value = value;
                    UndoEdit.End(config);
                }
                if (GUI.Button(removeRect, "x"))
                    remove = over;
                y += LineH;
            }

            if (remove != null)
            {
                UndoEdit.Begin(config, "Remove blendshape override");
                outfit.blendshapes.Remove(remove);
                UndoEdit.End(config);
            }
        }

        // Objects forced on or off while this outfit is worn; unmentioned
        // outfits restore the scene state.
        void DrawObjectOverrides(Rect rect, OutfitGroup outfit, ref float y)
        {
            float line = EditorGUIUtility.singleLineHeight;
            EditorGUI.LabelField(new Rect(rect.x + 18, y, 220, line), "Objects (state while worn)", EditorStyles.miniBoldLabel);
            if (GUI.Button(new Rect(rect.xMax - 44, y, 44, line), "Add"))
            {
                UndoEdit.Begin(config, "Add object override");
                outfit.objectOverrides.Add(new ObjectOverride());
                UndoEdit.End(config);
            }
            y += LineH;

            ObjectOverride remove = null;
            foreach (var over in outfit.objectOverrides)
            {
                var objRect = new Rect(rect.x + 18, y, rect.xMax - rect.x - 18 - 40 - 24 - 8, line);
                var stateRect = new Rect(objRect.xMax + 4, y, 40, line);
                var removeRect = new Rect(rect.xMax - 20, y, 20, line);

                EditorGUI.BeginChangeCheck();
                var target = (GameObject)EditorGUI.ObjectField(objRect, over.target, typeof(GameObject), true);
                bool enable = GUI.Toggle(stateRect, over.enable, over.enable ? "On" : "Off", "Button");
                if (EditorGUI.EndChangeCheck())
                {
                    UndoEdit.Begin(config, "Edit object override");
                    over.target = target;
                    over.enable = enable;
                    UndoEdit.End(config);
                }
                if (GUI.Button(removeRect, "x"))
                    remove = over;
                y += LineH;
            }

            if (remove != null)
            {
                UndoEdit.Begin(config, "Remove object override");
                outfit.objectOverrides.Remove(remove);
                UndoEdit.End(config);
            }
        }

        // Popup listing the mesh's blendshape names. A stale or not-yet-chosen
        // value shows as a placeholder first entry so nothing is overwritten
        // until the user actively picks.
        static string BlendshapePopup(Rect rect, SkinnedMeshRenderer renderer, string current)
        {
            var mesh = renderer == null ? null : renderer.sharedMesh;
            if (mesh == null || mesh.blendShapeCount == 0)
            {
                EditorGUI.LabelField(rect, "no blendshapes");
                return current;
            }

            var names = new List<string>(mesh.blendShapeCount);
            for (int i = 0; i < mesh.blendShapeCount; i++)
                names.Add(mesh.GetBlendShapeName(i));

            int index = names.IndexOf(current);
            bool placeholder = index < 0;
            if (placeholder)
            {
                names.Insert(0, string.IsNullOrEmpty(current) ? "(select)" : current + " (missing)");
                index = 0;
            }

            int chosen = EditorGUI.Popup(rect, index, names.ToArray());
            if (placeholder && chosen == 0) return current;
            return names[chosen];
        }
    }
}
