using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace TsiYuki.Wardrobe.Editor
{
    // Builds the expressions menu tree: one wrapper entry pointing at the
    // wardrobe menu, category submenus when more than one category exists,
    // a per-outfit submenu (Wear + element toggles) when element toggles are
    // enabled, and pagination wherever a menu would exceed VRChat's control
    // cap. The returned wrapper is what a menu installer should append.
    public static class MenuBuilder
    {
        public static VRCExpressionsMenu Build(WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var controls = new List<VRCExpressionsMenu.Control>();
            if (model.IncludeNone)
                controls.Add(IntToggle("None", model.ParameterName, model.NoneIndex));

            // Outfits in the "Default" category sit at the wardrobe root;
            // every other category gets its own submenu.
            controls.AddRange(model.Outfits
                .Where(o => IsDefaultCategory(o.Category))
                .Select(o => OutfitControl(o, model, persist)));

            foreach (var category in model.Outfits
                         .Where(o => !IsDefaultCategory(o.Category))
                         .GroupBy(o => o.Category))
            {
                var items = category.Select(o => OutfitControl(o, model, persist)).ToList();
                controls.Add(SubMenu(category.Key, Paginate(category.Key, items, persist)));
            }

            var entry = SubMenu(model.MenuName, Paginate(model.MenuName, controls, persist));
            return Menu(model.MenuName, new List<VRCExpressionsMenu.Control> { entry }, persist);
        }

        static bool IsDefaultCategory(string category) =>
            string.Equals(category, "Default", System.StringComparison.OrdinalIgnoreCase);

        static VRCExpressionsMenu.Control OutfitControl(ResolvedOutfit outfit, WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            if (outfit.Elements.Count == 0)
                return IntToggle(outfit.DisplayName, model.ParameterName, outfit.Index);

            var items = new List<VRCExpressionsMenu.Control>
            {
                IntToggle("Wear", model.ParameterName, outfit.Index),
            };
            items.AddRange(outfit.Elements.Select(e => BoolToggle(e.DisplayName, e.ParameterName)));
            return SubMenu(outfit.DisplayName, Paginate(outfit.DisplayName, items, persist));
        }

        // Splits a control list into a chain of menus, each within the
        // per-menu control limit, linked by a trailing "Next" entry.
        static VRCExpressionsMenu Paginate(string name, List<VRCExpressionsMenu.Control> controls, Action<UnityEngine.Object> persist)
        {
            int max = VRCExpressionsMenu.MAX_CONTROLS;
            if (controls.Count <= max)
                return Menu(name, controls, persist);

            var page = controls.Take(max - 1).ToList();
            var rest = controls.Skip(max - 1).ToList();
            page.Add(SubMenu("Next", Paginate($"{name} (more)", rest, persist)));
            return Menu(name, page, persist);
        }

        static VRCExpressionsMenu Menu(string name, List<VRCExpressionsMenu.Control> controls, Action<UnityEngine.Object> persist)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = name;
            menu.controls = controls;
            persist(menu);
            return menu;
        }

        static VRCExpressionsMenu.Control IntToggle(string label, string parameter, int value) => new VRCExpressionsMenu.Control
        {
            name = label,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
            value = value,
        };

        static VRCExpressionsMenu.Control BoolToggle(string label, string parameter) => new VRCExpressionsMenu.Control
        {
            name = label,
            type = VRCExpressionsMenu.Control.ControlType.Toggle,
            parameter = new VRCExpressionsMenu.Control.Parameter { name = parameter },
            value = 1,
        };

        static VRCExpressionsMenu.Control SubMenu(string label, VRCExpressionsMenu menu) => new VRCExpressionsMenu.Control
        {
            name = label,
            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
            subMenu = menu,
        };
    }
}
