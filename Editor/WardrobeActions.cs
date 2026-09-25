using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using nadena.dev.modular_avatar.core;
using TsiYuki.Core.Editor;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Wardrobe.Editor
{
    // Editing operations shared by the window and the Hierarchy menu. All of
    // them are undoable.
    public static class WardrobeActions
    {
        public static YukiWardrobe CreateWardrobe(VRCAvatarDescriptor avatar, string name)
        {
            var host = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(host, "Create Wardrobe");
            host.transform.SetParent(avatar.transform, false);
            var config = Undo.AddComponent<YukiWardrobe>(host);
            config.EnsureIds();
            return config;
        }

        /// <summary>
        /// Adds an outfit: a prefab is placed under the avatar and set up with
        /// Modular Avatar's "Setup Outfit" first; a scene object is added as is.
        /// </summary>
        public static WardrobeEntry AddOutfit(YukiWardrobe config, VRCAvatarDescriptor avatar, GameObject source, string category)
        {
            if (source == null) return null;
            var go = source;
            if (EditorUtility.IsPersistent(source))
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(source, avatar.transform);
                Undo.RegisterCreatedObjectUndo(go, "Add outfit");
                SetupOutfit(go);
            }
            else if (!go.transform.IsChildOf(avatar.transform) || go.transform == avatar.transform)
            {
                return null;
            }
            else if (go.GetComponentInChildren<ModularAvatarMergeArmature>(true) == null && LooksLikeOutfitPrefab(go))
            {
                SetupOutfit(go);
            }

            var existing = config.entries.FirstOrDefault(o => o != null && o.root == go);
            if (existing != null) return existing;
            UndoEdit.Begin(config, "Add outfit");
            var entry = new WardrobeEntry { root = go, category = (category ?? "").Trim() };
            config.entries.Add(entry);
            config.EnsureIds();
            if (string.IsNullOrEmpty(config.defaultEntry)) config.defaultEntry = entry.id;
            UndoEdit.End(config);
            return entry;
        }

        public static WardrobeEntry AddNone(YukiWardrobe config)
        {
            var existing = config.entries.FirstOrDefault(e => e != null && e.kind == EntryKind.None);
            if (existing != null) return existing;
            UndoEdit.Begin(config, "Add None");
            var entry = new WardrobeEntry { kind = EntryKind.None };
            config.entries.Add(entry);
            config.EnsureIds();
            UndoEdit.End(config);
            return entry;
        }

        static bool LooksLikeOutfitPrefab(GameObject go) =>
            go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any() &&
            go.GetComponentsInChildren<Transform>(true).Any(t => Regex.IsMatch(t.name, "^(Armature|armature)$"));

        // Runs MA's "Setup Outfit" through its menu command, which is what a
        // user would click; MA keeps that entry point stable.
        static void SetupOutfit(GameObject go)
        {
            var previous = Selection.objects;
            Selection.activeGameObject = go;
            foreach (var path in new[] { "GameObject/Modular Avatar/Setup Outfit", "GameObject/[Modular Avatar] Setup Outfit" })
                if (EditorApplication.ExecuteMenuItem(path)) break;
            Selection.objects = previous;
        }

        /// <summary>Body blendshapes that look like they hide skin under clothes.</summary>
        public static List<(SkinnedMeshRenderer renderer, string shape)> ShrinkCandidates(Transform avatarRoot, WardrobeModel model)
        {
            var outfitRoots = model.Outfits.Select(o => o.Root.transform).ToList();
            var result = new List<(SkinnedMeshRenderer, string)>();
            var pattern = new Regex(@"shrink|hide|_off$|^off_|縮|隠|消|收缩|隐藏", RegexOptions.IgnoreCase);
            foreach (var smr in avatarRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                if (outfitRoots.Any(r => smr.transform.IsChildOf(r))) continue;
                for (int i = 0; i < smr.sharedMesh.blendShapeCount; i++)
                {
                    var name = smr.sharedMesh.GetBlendShapeName(i);
                    if (pattern.IsMatch(name)) result.Add((smr, name));
                }
            }
            return result;
        }

        /// <summary>
        /// Items of an outfit's own menu that simply show or hide one direct
        /// child of the outfit, and so can become wardrobe pieces.
        /// </summary>
        public static List<(ModularAvatarMenuItem item, GameObject target, bool visibleByDefault)> ConvertibleToggles(WardrobeEntry group)
        {
            var result = new List<(ModularAvatarMenuItem, GameObject, bool)>();
            if (group.root == null || group.kind != EntryKind.Outfit) return result;
            foreach (var toggle in group.root.GetComponentsInChildren<ModularAvatarObjectToggle>(true))
            {
                var item = toggle.GetComponent<ModularAvatarMenuItem>();
                if (item == null || toggle.Objects == null || toggle.Objects.Count != 1) continue;
                if (item.Control == null || item.Control.type != VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.ControlType.Toggle) continue;
                var entry = toggle.Objects[0];
                var target = entry.Object?.Get(toggle);
                if (target == null || target.transform.parent != group.root.transform) continue;
                // While the item is on, the object takes entry.Active.
                var visible = item.isDefault ? entry.Active : !entry.Active;
                result.Add((item, target, visible));
            }
            return result;
        }

        public static void ConvertToPieces(YukiWardrobe config, WardrobeEntry group, IEnumerable<(ModularAvatarMenuItem item, GameObject target, bool visibleByDefault)> toggles)
        {
            Undo.SetCurrentGroupName("Convert outfit toggles to pieces");
            var group_ = Undo.GetCurrentGroup();
            UndoEdit.Begin(config, "Convert outfit toggles");
            var removals = new List<GameObject>();
            foreach (var (item, target, visible) in toggles)
            {
                var settings = group.FindPiece(target);
                if (settings == null) group.pieces.Add(settings = new WardrobePiece { target = target });
                settings.displayName = string.IsNullOrEmpty(item.label) ? item.gameObject.name : item.label;
                if (item.Control?.icon != null) settings.icon = item.Control.icon;
                if (target.activeSelf != visible)
                {
                    Undo.RecordObject(target, "Convert outfit toggles");
                    target.SetActive(visible);
                }
                removals.Add(item.gameObject);
            }
            config.EnsureIds();
            UndoEdit.End(config);
            // Remove the converted menu items; a menu object that only held
            // those items is removed with them.
            foreach (var go in removals)
            {
                if (go == null) continue;
                var parent = go.transform.parent;
                Undo.DestroyObjectImmediate(go);
                if (parent != null && parent.childCount == 0 && parent.GetComponents<Component>().All(c => c is Transform || c is ModularAvatarMenuItem || c is ModularAvatarMenuInstaller))
                    Undo.DestroyObjectImmediate(parent.gameObject);
            }
            Undo.CollapseUndoOperations(group_);
        }
    }

    // Synced parameter usage of the whole avatar, grouped by where it comes from.
    public static class ParameterBudget
    {
        public const int Limit = 256;

        public class Usage
        {
            public string Source;
            public int Bits;
            public bool IsWardrobe;
        }

        public static List<Usage> Measure(VRCAvatarDescriptor avatar)
        {
            var bySource = new Dictionary<string, Usage>();
            var names = new HashSet<string>();
            try
            {
                foreach (var p in nadena.dev.ndmf.ParameterInfo.ForUI.GetParametersForObject(avatar.gameObject))
                {
                    if (!p.WantSynced || p.IsHidden || !names.Add(p.EffectiveName)) continue;
                    var isWardrobe = p.Source is YukiWardrobe;
                    var source = isWardrobe ? ((YukiWardrobe)p.Source).gameObject.name
                        : p.Plugin != null ? p.Plugin.DisplayName
                        : p.Source != null ? p.Source.GetType().Name : "?";
                    var key = (isWardrobe ? "W:" : "O:") + source;
                    if (!bySource.TryGetValue(key, out var u)) bySource[key] = u = new Usage { Source = source, IsWardrobe = isWardrobe };
                    u.Bits += p.BitUsage;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Yuki Wardrobe] Could not read parameter usage: " + e.Message);
            }

            // Parameters already in the descriptor's own expression parameters.
            var own = avatar.expressionParameters;
            if (own != null && own.parameters != null)
            {
                var u = new Usage { Source = WardrobeText.L["budget.descriptor"] };
                foreach (var p in own.parameters)
                {
                    if (p == null || string.IsNullOrEmpty(p.name) || !p.networkSynced || !names.Add(p.name)) continue;
                    u.Bits += p.valueType == VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.ValueType.Bool ? 1 : 8;
                }
                if (u.Bits > 0) bySource["D"] = u;
            }
            return bySource.Values.OrderByDescending(u => u.IsWardrobe).ThenByDescending(u => u.Bits).ToList();
        }
    }
}
