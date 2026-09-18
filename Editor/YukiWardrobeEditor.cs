using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Wardrobe.Editor
{
    // Inspector for the component is a summary only; real editing happens in
    // the window, which survives Hierarchy selection changes.
    [CustomEditor(typeof(YukiWardrobe))]
    public class YukiWardrobeEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var config = (YukiWardrobe)target;
            var avatar = config.GetComponentInParent<VRCAvatarDescriptor>();

            if (avatar == null)
            {
                EditorGUILayout.HelpBox("This component must live inside an avatar (no VRC Avatar Descriptor found on any parent).", MessageType.Error);
                return;
            }

            var model = WardrobeModel.Resolve(config, avatar.transform);
            EditorGUILayout.LabelField($"{model.Outfits.Count} outfits, {model.TotalBits} parameter bits.");
            foreach (var warning in model.Warnings)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            if (GUILayout.Button("Open Wardrobe Editor"))
                WardrobeWindow.Open(config);
        }
    }
}
