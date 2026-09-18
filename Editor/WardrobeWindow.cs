using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace TsiYuki.Wardrobe.Editor
{
    // Dockable editing view over a YukiWardrobe component. Holds no wardrobe
    // data of its own: every edit goes straight to the component through
    // Undo, so the window can be closed or the editor restarted at any time
    // without losing anything.
    public class WardrobeWindow : EditorWindow
    {
        VRCAvatarDescriptor avatar;
        string newCategory = "Default";
        Vector2 scroll;
        OutfitListGUI outfitList;

        [MenuItem("TsiYuki/Wardrobe Editor")]
        public static void ShowWindow() => GetWindow<WardrobeWindow>("Yuki Wardrobe");

        public static void Open(YukiWardrobe config)
        {
            var window = GetWindow<WardrobeWindow>("Yuki Wardrobe");
            window.avatar = config.GetComponentInParent<VRCAvatarDescriptor>();
        }

        void OnGUI()
        {
            avatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Avatar", avatar, typeof(VRCAvatarDescriptor), true);
            if (avatar == null)
            {
                var found = FindObjectsOfType<VRCAvatarDescriptor>();
                if (found.Length == 1) avatar = found[0];
            }
            if (avatar == null)
            {
                EditorGUILayout.HelpBox("Select an avatar to edit its wardrobe.", MessageType.Info);
                return;
            }

            var config = avatar.GetComponentInChildren<YukiWardrobe>(true);
            if (config == null)
            {
                EditorGUILayout.HelpBox("This avatar has no wardrobe yet.", MessageType.Info);
                if (GUILayout.Button("Create wardrobe on this avatar"))
                    CreateConfig();
                return;
            }

            DrawSettings(config);
            EditorGUILayout.Space();
            DrawAddControls(config);

            if (outfitList == null || !outfitList.Owns(config))
                outfitList = new OutfitListGUI(config);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            outfitList.Draw();
            EditorGUILayout.EndScrollView();

            DrawStatus(config);
        }

        void CreateConfig()
        {
            var host = new GameObject("Wardrobe");
            host.transform.SetParent(avatar.transform, false);
            host.AddComponent<YukiWardrobe>();
            Undo.RegisterCreatedObjectUndo(host, "Create Wardrobe");
        }

        void DrawSettings(YukiWardrobe config)
        {
            EditorGUI.BeginChangeCheck();
            string parameterName = EditorGUILayout.TextField("Parameter", config.parameterName);
            string menuName = EditorGUILayout.TextField("Menu name", config.menuName);
            bool saved = EditorGUILayout.Toggle("Saved parameters", config.saved);
            bool includeNone = EditorGUILayout.Toggle("None option (naked)", config.includeNone);
            if (EditorGUI.EndChangeCheck())
            {
                UndoEdit.Begin(config, "Edit wardrobe settings");
                config.parameterName = parameterName;
                config.menuName = menuName;
                config.saved = saved;
                config.includeNone = includeNone;
                UndoEdit.End(config);
            }
        }

        void DrawAddControls(YukiWardrobe config)
        {
            EditorGUILayout.BeginHorizontal();
            newCategory = EditorGUILayout.TextField("Category", newCategory);
            if (GUILayout.Button("Add selected as outfits", GUILayout.Width(170)))
                AddSelection(config);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Select outfit parent objects in the Hierarchy, then add them. Expand a row to pick toggleable pieces and blendshape overrides.", MessageType.None);
        }

        void AddSelection(YukiWardrobe config)
        {
            var toAdd = Selection.gameObjects
                .Where(go => go != null && go.transform != avatar.transform && go.transform.IsChildOf(avatar.transform))
                .Where(go => config.outfits.All(o => o.root != go))
                .ToList();
            if (toAdd.Count == 0) return;

            UndoEdit.Begin(config, "Add outfits");
            foreach (var go in toAdd)
                config.outfits.Add(new OutfitGroup { root = go, category = newCategory });
            UndoEdit.End(config);
        }

        void DrawStatus(YukiWardrobe config)
        {
            var model = WardrobeModel.Resolve(config, avatar.transform);
            foreach (var warning in model.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            int max = VRCExpressionParameters.MAX_PARAMETER_COST;
            EditorGUILayout.HelpBox(
                $"Parameter memory used by the wardrobe: {model.TotalBits} bits (avatar total limit {max}). Blendshape overrides cost no parameter memory.",
                model.TotalBits > max ? MessageType.Error : MessageType.None);
        }
    }
}
