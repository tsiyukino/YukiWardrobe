using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace TsiYuki.Wardrobe.Editor
{
    // One entry of a menu shipped with an outfit, for display and checks.
    public class MenuNode
    {
        public string Label;
        public VRCExpressionsMenu.Control.ControlType Type;
        public string Parameter;
        public bool? IsDefault;
        public Object Context;
        public List<MenuNode> Children = new List<MenuNode>();

        // Objects switched by a Modular Avatar Object Toggle on this item,
        // with the state they take while the item is on.
        public List<(GameObject target, bool active)> Toggles = new List<(GameObject, bool)>();

        public IEnumerable<MenuNode> Flatten()
        {
            yield return this;
            foreach (var child in Children)
                foreach (var n in child.Flatten())
                    yield return n;
        }
    }

    public enum OutfitMenuSource
    {
        ModularAvatar,
        VRCFury,
    }

    // A menu that ships inside an outfit and would normally be installed on its own.
    public class OutfitMenu
    {
        public OutfitMenuSource Source;
        public ModularAvatarMenuInstaller Installer; // null for VRCFury
        public Component Component;
        public MenuNode Root;

        public bool CanAbsorb => Installer != null;
    }

    public static class OutfitMenuScanner
    {
        const int MaxDepth = 8;

        public static List<OutfitMenu> Scan(GameObject outfitRoot)
        {
            var result = new List<OutfitMenu>();
            if (outfitRoot == null) return result;

            foreach (var installer in outfitRoot.GetComponentsInChildren<ModularAvatarMenuInstaller>(true))
            {
                if (!installer.enabled || installer.CompareTag("EditorOnly")) continue;
                // An installer nested under another installer's menu tree is
                // installed as part of that tree, not on its own.
                if (installer.transform.parent != null &&
                    installer.transform.parent.GetComponentsInParent<ModularAvatarMenuInstaller>(true)
                        .Any(p => p.transform.IsChildOf(outfitRoot.transform) && p.GetComponent<ModularAvatarMenuItem>() != null))
                    continue;

                var menu = new OutfitMenu
                {
                    Source = OutfitMenuSource.ModularAvatar,
                    Installer = installer,
                    Component = installer,
                };
                var item = installer.GetComponent<ModularAvatarMenuItem>();
                if (item != null)
                {
                    menu.Root = FromMenuItem(item, 0);
                }
                else
                {
                    menu.Root = new MenuNode
                    {
                        Label = installer.menuToAppend != null ? installer.menuToAppend.name : installer.gameObject.name,
                        Type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        Context = installer,
                    };
                    if (installer.menuToAppend != null)
                        menu.Root.Children.AddRange(FromAsset(installer.menuToAppend, 0));
                }
                result.Add(menu);
            }

            foreach (var component in outfitRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.GetType().Name != "VRCFury") continue;
                result.Add(new OutfitMenu
                {
                    Source = OutfitMenuSource.VRCFury,
                    Component = component,
                    Root = new MenuNode
                    {
                        Label = "VRCFury (" + component.gameObject.name + ")",
                        Type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        Context = component,
                    },
                });
            }
            return result;
        }

        static MenuNode FromMenuItem(ModularAvatarMenuItem item, int depth)
        {
            var control = item.Control ?? new VRCExpressionsMenu.Control();
            var node = new MenuNode
            {
                Label = string.IsNullOrEmpty(item.label) ? item.gameObject.name : item.label,
                Type = control.type,
                Parameter = control.parameter?.name,
                IsDefault = item.isDefault,
                Context = item,
            };

            var toggle = item.GetComponent<ModularAvatarObjectToggle>();
            if (toggle != null && toggle.Objects != null)
                foreach (var entry in toggle.Objects)
                {
                    var target = entry.Object?.Get(toggle);
                    if (target != null) node.Toggles.Add((target, entry.Active));
                }

            if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu || depth >= MaxDepth) return node;

            if (item.MenuSource == SubmenuSource.Children)
            {
                var source = item.menuSource_otherObjectChildren != null ? item.menuSource_otherObjectChildren.transform : item.transform;
                foreach (Transform child in source)
                {
                    if (!child.gameObject.activeSelf && child.CompareTag("EditorOnly")) continue;
                    var childItem = child.GetComponent<ModularAvatarMenuItem>();
                    if (childItem != null && childItem.enabled) node.Children.Add(FromMenuItem(childItem, depth + 1));
                }
            }
            else if (control.subMenu != null)
            {
                node.Children.AddRange(FromAsset(control.subMenu, depth + 1));
            }
            return node;
        }

        static IEnumerable<MenuNode> FromAsset(VRCExpressionsMenu menu, int depth)
        {
            if (menu == null || menu.controls == null) yield break;
            foreach (var control in menu.controls)
            {
                var node = new MenuNode
                {
                    Label = control.name,
                    Type = control.type,
                    Parameter = control.parameter?.name,
                    Context = menu,
                };
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null && depth < MaxDepth)
                    node.Children.AddRange(FromAsset(control.subMenu, depth + 1));
                yield return node;
            }
        }
    }
}
