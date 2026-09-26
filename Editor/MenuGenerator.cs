using System.Linq;
using TsiYuki.Core.Menus.Editor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace TsiYuki.Wardrobe.Editor
{
    // Builds the wardrobe menu as a tree of Modular Avatar Menu Items under
    // the wardrobe object. Using MA components (instead of menu assets) lets
    // MA handle installation and paging, and lets the menus that ship inside
    // outfits be pulled into their outfit's submenu with Menu Install Target.
    public static class MenuGenerator
    {
        public static GameObject Build(WardrobeModel model, Transform host)
        {
            var root = MenuItems.Root(host, model.MenuName, model.MenuIcon);

            // List order; a category becomes a submenu where its first entry is.
            var folders = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var entry in model.Entries)
            {
                var parent = root.transform;
                if (!string.IsNullOrEmpty(entry.Category) && !folders.TryGetValue(entry.Category, out parent))
                    folders[entry.Category] = parent = MenuItems.SubMenu(root.transform, entry.Category, null).transform;
                AddEntry(parent, entry, model);
            }

            if (model.Looks.Count > 0)
            {
                var looks = MenuItems.SubMenu(root.transform, MenuText.Looks, null);
                foreach (var look in model.Looks)
                    MenuItems.Control(looks.transform, look.DisplayName, look.Icon, VRCExpressionsMenu.Control.ControlType.Button, model.LookParameter, look.Value);
            }
            return root;
        }

        static void AddEntry(Transform parent, ResolvedOutfit outfit, WardrobeModel model)
        {
            if (!outfit.HasSubmenu)
            {
                Toggle(parent, outfit.DisplayName, outfit.Icon, model.ParameterName, outfit.Value);
                return;
            }

            var menu = MenuItems.SubMenu(parent, outfit.DisplayName, outfit.Icon);
            Toggle(menu.transform, MenuText.Wear, outfit.Icon, model.ParameterName, outfit.Value);

            foreach (var piece in outfit.Pieces)
                Toggle(menu.transform, piece.DisplayName, piece.Icon, piece.ParameterName, 1);

            if (outfit.ColorParameter != null)
            {
                var colors = MenuItems.SubMenu(menu.transform, MenuText.Colors, null);
                foreach (var color in outfit.Colors)
                    Toggle(colors.transform, color.DisplayName, color.Icon, outfit.ColorParameter, color.Index);
            }

            if (outfit.MenuMode == OutfitMenuMode.Absorb)
                foreach (var shipped in outfit.Menus.Where(m => m.CanAbsorb))
                    if (InstallTarget.Add(menu.transform, shipped.Root.Label, shipped.Installer) == null)
                        Debug.LogWarning("[Yuki Wardrobe] This Modular Avatar version has no Menu Install Target; the outfit menu stays where it is.");
        }

        static void Toggle(Transform parent, string label, Texture2D icon, string parameter, float value) =>
            MenuItems.Control(parent, label, icon, VRCExpressionsMenu.Control.ControlType.Toggle, parameter, value);
    }
}
