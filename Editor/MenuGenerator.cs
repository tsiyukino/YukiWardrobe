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

            if (model.IncludeNone)
                Toggle(root.transform, model.NoneLabel, model.NoneIcon, model.ParameterName, model.NoneIndex);

            foreach (var outfit in model.Outfits.Where(o => IsDefaultCategory(o.Category)))
                AddOutfit(root.transform, outfit, model);

            foreach (var category in model.Outfits.Where(o => !IsDefaultCategory(o.Category)).GroupBy(o => o.Category))
            {
                var folder = Child(root.transform, category.Key);
                SubMenu(folder, category.Key, null);
                foreach (var outfit in category)
                    AddOutfit(folder.transform, outfit, model);
            }

            if (model.Looks.Count > 0)
            {
                var looks = Child(root.transform, WardrobeText.L["menu.looks"]);
                SubMenu(looks, WardrobeText.L["menu.looks"], null);
                foreach (var look in model.Looks)
                    Item(looks.transform, look.DisplayName, look.Icon, VRCExpressionsMenu.Control.ControlType.Button, model.LookParameter, look.Value);
            }
            return root;
        }

        static void AddOutfit(Transform parent, ResolvedOutfit outfit, WardrobeModel model)
        {
            if (!outfit.HasSubmenu)
            {
                Toggle(parent, outfit.DisplayName, outfit.Icon, model.ParameterName, outfit.Index);
                return;
            }

            var menu = Child(parent, outfit.DisplayName);
            SubMenu(menu, outfit.DisplayName, outfit.Icon);
            Toggle(menu.transform, WardrobeText.L["menu.wear"], outfit.Icon, model.ParameterName, outfit.Index);

            foreach (var element in outfit.Elements)
                Toggle(menu.transform, element.DisplayName, element.Icon, element.ParameterName, 1);

            if (outfit.VariantParameter != null)
            {
                var colors = Child(menu.transform, WardrobeText.L["menu.variants"]);
                SubMenu(colors, WardrobeText.L["menu.variants"], null);
                foreach (var variant in outfit.Variants)
                    Toggle(colors.transform, variant.DisplayName, variant.Icon, outfit.VariantParameter, variant.Index);
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

        public static bool IsDefaultCategory(string category) =>
            string.IsNullOrWhiteSpace(category) || string.Equals(category, "Default", System.StringComparison.OrdinalIgnoreCase);

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
