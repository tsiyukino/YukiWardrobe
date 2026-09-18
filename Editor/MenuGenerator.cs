using System.Linq;
using nadena.dev.modular_avatar.core;
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
            var root = new GameObject(model.MenuName);
            root.transform.SetParent(host, false);
            root.AddComponent<ModularAvatarMenuInstaller>();
            SubMenu(root, model.MenuName, model.MenuIcon);

            // List order; a category becomes a submenu where its first entry is.
            var folders = new System.Collections.Generic.Dictionary<string, Transform>();
            foreach (var entry in model.Entries)
            {
                var parent = root.transform;
                if (!string.IsNullOrEmpty(entry.Category))
                {
                    if (!folders.TryGetValue(entry.Category, out parent))
                    {
                        var folder = Child(root.transform, entry.Category);
                        SubMenu(folder, entry.Category, null);
                        folders[entry.Category] = parent = folder.transform;
                    }
                }
                AddEntry(parent, entry, model);
            }

            if (model.Looks.Count > 0)
            {
                var looks = Child(root.transform, MenuText.Looks);
                SubMenu(looks, MenuText.Looks, null);
                foreach (var look in model.Looks)
                    Item(looks.transform, look.DisplayName, look.Icon, VRCExpressionsMenu.Control.ControlType.Button, model.LookParameter, look.Value);
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

            var menu = Child(parent, outfit.DisplayName);
            SubMenu(menu, outfit.DisplayName, outfit.Icon);
            Toggle(menu.transform, MenuText.Wear, outfit.Icon, model.ParameterName, outfit.Value);

            foreach (var piece in outfit.Pieces)
                Toggle(menu.transform, piece.DisplayName, piece.Icon, piece.ParameterName, 1);

            if (outfit.ColorParameter != null)
            {
                var colors = Child(menu.transform, MenuText.Colors);
                SubMenu(colors, MenuText.Colors, null);
                foreach (var color in outfit.Colors)
                    Toggle(colors.transform, color.DisplayName, color.Icon, outfit.ColorParameter, color.Index);
            }

            if (outfit.MenuMode == OutfitMenuMode.Absorb)
                foreach (var shipped in outfit.Menus.Where(m => m.CanAbsorb))
                {
                    var target = Child(menu.transform, shipped.Root.Label);
                    AddInstallTarget(target, shipped.Installer);
                }
        }

        // MA's Menu Install Target is what its own "Select Menu" button creates;
        // the component is internal, so it is added by reflection.
        static readonly System.Type InstallTargetType =
            typeof(ModularAvatarMenuInstaller).Assembly.GetType("nadena.dev.modular_avatar.core.ModularAvatarMenuInstallTarget");

        static void AddInstallTarget(GameObject go, ModularAvatarMenuInstaller installer)
        {
            if (InstallTargetType == null)
            {
                Debug.LogWarning("[Yuki Wardrobe] This Modular Avatar version has no Menu Install Target; the outfit menu stays where it is.");
                Object.DestroyImmediate(go);
                return;
            }
            var component = go.AddComponent(InstallTargetType);
            InstallTargetType.GetField("installer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(component, installer);
        }

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static void SubMenu(GameObject go, string label, Texture2D icon)
        {
            var item = go.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                name = label,
                icon = icon,
                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = "" },
            };
            item.MenuSource = SubmenuSource.Children;
            item.label = label;
            item.automaticValue = false;
        }

        static void Toggle(Transform parent, string label, Texture2D icon, string parameter, float value) =>
            Item(parent, label, icon, VRCExpressionsMenu.Control.ControlType.Toggle, parameter, value);

        static void Item(Transform parent, string label, Texture2D icon, VRCExpressionsMenu.Control.ControlType type, string parameter, float value)
        {
            var go = Child(parent, label);
            var item = go.AddComponent<ModularAvatarMenuItem>();
            item.Control = new VRCExpressionsMenu.Control
            {
                name = label,
                icon = icon,
                type = type,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
                value = value,
            };
            item.label = label;
            item.automaticValue = false;
        }
    }
}
