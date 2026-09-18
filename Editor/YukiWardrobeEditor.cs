using System.Linq;
using TsiYuki.Core.Editor;
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
        static YukiLocalizer L => WardrobeText.L;

        void OnEnable() => YukiLanguage.Changed += Repaint;
        void OnDisable() => YukiLanguage.Changed -= Repaint;

        public override void OnInspectorGUI()
        {
            var config = (YukiWardrobe)target;
            YukiGUI.Header("Yuki Wardrobe", null);
            var avatar = config.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar == null)
            {
                EditorGUILayout.HelpBox(L["ui.not_in_avatar"], MessageType.Error);
                return;
            }

            var model = WardrobeSet.Resolve(avatar.transform).For(config);
            if (model != null)
            {
                EditorGUILayout.LabelField(model.MenuName, YukiGUI.SectionHeaderStyle);
                EditorGUILayout.LabelField(L.Tr("ui.summary_text", model.Outfits.Count(), model.AllPieces.Count(), model.Looks.Count, model.TotalBits), YukiGUI.WrapMini);
                foreach (var outfit in model.Entries)
                    EditorGUILayout.LabelField("• " + outfit.DisplayName + (outfit.IsDefault ? "  (" + L["ui.badge.default"] + ")" : ""), EditorStyles.miniLabel);
                foreach (var warning in model.Warnings)
                    EditorGUILayout.HelpBox(warning.Message, MessageType.Warning);
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button(L["ui.open_editor"], GUILayout.Height(28)))
                WardrobeWindow.Open(config);
        }
    }
}
