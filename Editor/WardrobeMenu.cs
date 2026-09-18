using System.Linq;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Wardrobe.Editor
{
    // Hierarchy right-click: GameObject > TsiYuki > Add to Wardrobe.
    static class WardrobeMenu
    {
        const string Path = "GameObject/TsiYuki/Add to Wardrobe";

        [MenuItem(Path, false, 20)]
        static void AddToWardrobe(MenuCommand command)
        {
            // Called once per selected object; handle the whole selection once.
            if (command.context != null && command.context != Selection.activeGameObject) return;
            var objects = Selection.gameObjects;
            if (objects.Length == 0) return;
            var avatar = objects[0].GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar == null)
            {
                EditorUtility.DisplayDialog("Yuki Wardrobe", WardrobeText.L["ui.not_in_avatar"], WardrobeText.L["ui.ok"]);
                return;
            }
            var config = avatar.GetComponentInChildren<YukiWardrobe>(true) ?? WardrobeActions.CreateWardrobe(avatar, "Wardrobe");
            foreach (var go in objects.Where(o => o.GetComponentInParent<VRCAvatarDescriptor>() == avatar))
                WardrobeActions.AddOutfit(config, avatar, go, "");
            WardrobeWindow.Open(config);
        }

        [MenuItem(Path, true)]
        static bool Validate() =>
            Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>() != null &&
            Selection.activeGameObject.GetComponent<VRCAvatarDescriptor>() == null;
    }
}
