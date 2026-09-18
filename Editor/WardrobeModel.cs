using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TsiYuki.Wardrobe.Editor
{
    public class ResolvedElement
    {
        public string DisplayName;
        public Texture2D Icon;
        public string ParameterName;
        public string Path;
        public GameObject Target;
        public bool DefaultOn;
    }

    public class ResolvedVariant
    {
        public string DisplayName;
        public Texture2D Icon;
        public int Index;
        // (renderer path, renderer type, slot) → material
        public List<(string path, System.Type type, int slot, Material material)> Materials = new List<(string, System.Type, int, Material)>();
    }

    public class ResolvedOutfit
    {
        public string DisplayName;
        public Texture2D Icon;
        public int Index;
        public string Key;
        public string Category;
        public string Path;
        public GameObject Root;
        public OutfitGroup Source;
        public List<ResolvedElement> Elements = new List<ResolvedElement>();

        // Channel key → weight this outfit sets; channels it doesn't mention
        // fall back to the channel baseline.
        public Dictionary<string, float> BlendshapeValues = new Dictionary<string, float>();

        // Object-channel path → active state this outfit forces; channels it
        // doesn't mention fall back to the channel baseline.
        public Dictionary<string, bool> ObjectValues = new Dictionary<string, bool>();

        public OutfitMenuMode MenuMode;
        public List<OutfitMenu> Menus = new List<OutfitMenu>();

        public string VariantParameter; // null when the outfit has no variants
        public List<ResolvedVariant> Variants = new List<ResolvedVariant>();

        // True when the outfit has anything besides the "wear" toggle, so it
        // gets a submenu instead of a single toggle.
        public bool HasSubmenu =>
            Elements.Count > 0 || Variants.Count > 1 || (MenuMode == OutfitMenuMode.Absorb && Menus.Count > 0);
    }

    public class ResolvedLook
    {
        public string DisplayName;
        public Texture2D Icon;
        public int Value; // 1-based; 0 is idle
        public int OutfitIndex;
        public List<(string parameter, bool on)> Pieces = new List<(string, bool)>();
    }

    // One avatar object forced on/off by at least one outfit. Every outfit
    // state animates every channel so values never linger across switches.
    public class ObjectChannel
    {
        public string Path;
        public GameObject Target;
        public bool Baseline; // scene active state at build time
    }

    // One avatar blendshape touched by at least one outfit. Every outfit
    // state animates every channel so values never linger across switches.
    public class BlendshapeChannel
    {
        public string Path;
        public SkinnedMeshRenderer Renderer;
        public string Blendshape;
        public float Baseline; // editor value at build time
        public string Property => "blendShape." + Blendshape;
        public string Key => Path + "|" + Blendshape;
    }

    public class ModelWarning
    {
        public string Key;
        public object[] Args;
        public Object Context;

        public ModelWarning(string key, Object context, params object[] args)
        {
            Key = key;
            Context = context;
            Args = args;
        }

        public string Message => WardrobeText.L.Tr(Key, Args);
    }

    // Build model resolved from one YukiWardrobe component. Index assignment
    // lives here and nowhere else: outfits keep their list order, so the
    // first outfit owns index 0 — the value VRChat resets a toggled-off
    // parameter to — making it the spawn default and the toggle-off
    // fallback. None, when enabled, takes the index above every outfit.
    public class WardrobeModel
    {
        public YukiWardrobe Config;
        public string ParameterName;
        public string LookParameter;
        public string MenuName;
        public Texture2D MenuIcon;
        public bool Saved;
        public bool IncludeNone;
        public string NoneLabel;
        public Texture2D NoneIcon;
        public int NoneIndex; // meaningful only when IncludeNone
        public List<ResolvedOutfit> Outfits = new List<ResolvedOutfit>();
        public List<BlendshapeChannel> BlendshapeChannels = new List<BlendshapeChannel>();
        public List<ObjectChannel> ObjectChannels = new List<ObjectChannel>();
        public List<ResolvedLook> Looks = new List<ResolvedLook>();
        public string ChangeEffectPath;
        public float ChangeEffectDuration;

        // Outfits removed for the current build platform.
        public List<GameObject> ExcludedRoots = new List<GameObject>();

        public List<ModelWarning> Warnings = new List<ModelWarning>();

        public IEnumerable<ResolvedElement> AllElements => Outfits.SelectMany(o => o.Elements);

        // Synced bits: one int for the outfit, one bool per piece, one int per
        // outfit with variants. The look selector is local only.
        public int TotalBits => 8 + AllElements.Count() + Outfits.Count(o => o.VariantParameter != null) * 8;

        public static WardrobeModel Resolve(YukiWardrobe config, Transform avatarRoot, string parameterName, bool mobile)
        {
            var model = new WardrobeModel
            {
                Config = config,
                ParameterName = parameterName,
                LookParameter = parameterName + "/Look",
                MenuName = Fallback(config.menuName, config.gameObject.name),
                MenuIcon = config.menuIcon,
                Saved = config.saved,
                IncludeNone = config.includeNone,
                NoneLabel = Fallback(config.noneLabel, WardrobeText.L["menu.none"]),
                NoneIcon = config.noneIcon,
                ChangeEffectDuration = Mathf.Max(0.1f, config.changeEffectDuration),
            };

            var seen = new HashSet<GameObject>();
            var valid = new List<OutfitGroup>();
            foreach (var group in config.outfits)
            {
                if (group == null || group.root == null) continue;
                if (group.root.transform == avatarRoot || !group.root.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", group.root, group.root.name));
                    continue;
                }
                if (!seen.Add(group.root))
                {
                    model.Warnings.Add(new ModelWarning("warn.duplicate_outfit", group.root, group.root.name));
                    continue;
                }
                if ((group.platform == OutfitPlatform.PCOnly && mobile) || (group.platform == OutfitPlatform.MobileOnly && !mobile))
                {
                    model.ExcludedRoots.Add(group.root);
                    continue;
                }
                valid.Add(group);
            }

            model.NoneIndex = valid.Count;

            var outfitKeys = new HashSet<string>();
            var channels = new Dictionary<string, BlendshapeChannel>();
            var objectChannels = new Dictionary<string, ObjectChannel>();
            for (int i = 0; i < valid.Count; i++)
            {
                var group = valid[i];
                string key = UniqueKey(Sanitize(group.root.name), outfitKeys);
                var outfit = new ResolvedOutfit
                {
                    DisplayName = Fallback(group.displayName, group.root.name),
                    Icon = group.icon,
                    Index = i,
                    Key = key,
                    Category = Fallback(group.category, "Default"),
                    Path = AnimationUtility.CalculateTransformPath(group.root.transform, avatarRoot),
                    Root = group.root,
                    Source = group,
                    MenuMode = group.menuMode,
                    Menus = OutfitMenuScanner.Scan(group.root),
                };

                ResolveElements(group, outfit, key, model, avatarRoot);
                ResolveBlendshapes(group, outfit, model, avatarRoot, channels);
                ResolveObjectOverrides(group, outfit, model, avatarRoot, valid, objectChannels);
                ResolveVariants(group, outfit, key, model, avatarRoot);
                model.Outfits.Add(outfit);
            }

            ResolveLooks(config, model);

            if (config.changeEffect != null)
            {
                if (config.changeEffect.transform.IsChildOf(avatarRoot) && config.changeEffect.transform != avatarRoot)
                    model.ChangeEffectPath = AnimationUtility.CalculateTransformPath(config.changeEffect.transform, avatarRoot);
                else
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", config.changeEffect, config.changeEffect.name));
            }

            return model;
        }

        static void ResolveElements(OutfitGroup group, ResolvedOutfit outfit, string outfitKey, WardrobeModel model, Transform avatarRoot)
        {
            if (group.toggleablePieces == null || group.toggleablePieces.Count == 0) return;

            var chosen = new HashSet<GameObject>();
            foreach (var piece in group.toggleablePieces)
            {
                if (piece == null) continue;
                if (piece.transform.parent != group.root.transform)
                {
                    model.Warnings.Add(new ModelWarning("warn.piece_not_child", piece, piece.name, group.root.name));
                    continue;
                }
                chosen.Add(piece);
            }

            // Iterate hierarchy order so the menu matches what the creator
            // sees in the editor, regardless of selection order.
            var elementKeys = new HashSet<string>();
            foreach (Transform child in group.root.transform)
            {
                if (!chosen.Contains(child.gameObject)) continue;
                string elementKey = UniqueKey(Sanitize(child.name), elementKeys);
                var settings = group.FindPiece(child.gameObject);
                outfit.Elements.Add(new ResolvedElement
                {
                    DisplayName = Fallback(settings?.displayName, child.name),
                    Icon = settings?.icon,
                    ParameterName = $"{model.ParameterName}/{outfitKey}/{elementKey}",
                    Path = AnimationUtility.CalculateTransformPath(child, avatarRoot),
                    Target = child.gameObject,
                    DefaultOn = child.gameObject.activeSelf,
                });
            }
        }

        static void ResolveBlendshapes(OutfitGroup group, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot, Dictionary<string, BlendshapeChannel> channels)
        {
            if (group.blendshapes == null) return;

            foreach (var over in group.blendshapes)
            {
                if (over == null || over.renderer == null || string.IsNullOrEmpty(over.blendshape)) continue;
                if (!over.renderer.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", over.renderer, over.renderer.name));
                    continue;
                }
                var mesh = over.renderer.sharedMesh;
                int shapeIndex = mesh == null ? -1 : mesh.GetBlendShapeIndex(over.blendshape);
                if (shapeIndex < 0)
                {
                    model.Warnings.Add(new ModelWarning("warn.missing_blendshape", over.renderer, over.renderer.name, over.blendshape, group.root.name));
                    continue;
                }

                string path = AnimationUtility.CalculateTransformPath(over.renderer.transform, avatarRoot);
                string channelKey = path + "|" + over.blendshape;
                if (!channels.TryGetValue(channelKey, out var channel))
                {
                    channel = new BlendshapeChannel
                    {
                        Path = path,
                        Renderer = over.renderer,
                        Blendshape = over.blendshape,
                        Baseline = over.renderer.GetBlendShapeWeight(shapeIndex),
                    };
                    channels[channelKey] = channel;
                    model.BlendshapeChannels.Add(channel);
                }
                outfit.BlendshapeValues[channelKey] = over.value;
            }
        }

        static void ResolveObjectOverrides(OutfitGroup group, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot, List<OutfitGroup> validGroups, Dictionary<string, ObjectChannel> objectChannels)
        {
            if (group.objectOverrides == null) return;

            foreach (var over in group.objectOverrides)
            {
                if (over == null || over.target == null) continue;
                if (over.target.transform == avatarRoot || !over.target.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", over.target, over.target.name));
                    continue;
                }
                if (validGroups.Any(g => over.target.transform == g.root.transform || over.target.transform.IsChildOf(g.root.transform)))
                {
                    model.Warnings.Add(new ModelWarning("warn.override_inside_outfit", over.target, over.target.name, group.root.name));
                    continue;
                }

                string path = AnimationUtility.CalculateTransformPath(over.target.transform, avatarRoot);
                if (!objectChannels.TryGetValue(path, out var channel))
                {
                    channel = new ObjectChannel { Path = path, Target = over.target, Baseline = over.target.activeSelf };
                    objectChannels[path] = channel;
                    model.ObjectChannels.Add(channel);
                }
                outfit.ObjectValues[path] = over.enable;
            }
        }

        static void ResolveVariants(OutfitGroup group, ResolvedOutfit outfit, string outfitKey, WardrobeModel model, Transform avatarRoot)
        {
            if (group.variants == null || group.variants.Count < 2) return;

            // Every variant animates every slot any variant touches; slots a
            // variant doesn't list keep the material they have in the scene.
            var slots = new Dictionary<(Renderer, int), Material>();
            foreach (var variant in group.variants)
                foreach (var m in variant.materials)
                {
                    if (m == null || m.renderer == null || m.material == null) continue;
                    if (!m.renderer.transform.IsChildOf(avatarRoot))
                    {
                        model.Warnings.Add(new ModelWarning("warn.outside_avatar", m.renderer, m.renderer.name));
                        continue;
                    }
                    var shared = m.renderer.sharedMaterials;
                    if (m.slot < 0 || m.slot >= shared.Length) continue;
                    slots[(m.renderer, m.slot)] = shared[m.slot];
                }
            if (slots.Count == 0) return;

            outfit.VariantParameter = $"{model.ParameterName}/{outfitKey}/Variant";
            for (int v = 0; v < group.variants.Count; v++)
            {
                var variant = group.variants[v];
                var resolved = new ResolvedVariant
                {
                    DisplayName = Fallback(variant.displayName, WardrobeText.L.Tr("menu.variant_n", v + 1)),
                    Icon = variant.icon,
                    Index = v,
                };
                foreach (var slot in slots)
                {
                    var material = variant.materials.FirstOrDefault(m => m != null && m.renderer == slot.Key.Item1 && m.slot == slot.Key.Item2 && m.material != null)?.material ?? slot.Value;
                    resolved.Materials.Add((AnimationUtility.CalculateTransformPath(slot.Key.Item1.transform, avatarRoot), slot.Key.Item1.GetType(), slot.Key.Item2, material));
                }
                outfit.Variants.Add(resolved);
            }
        }

        static void ResolveLooks(YukiWardrobe config, WardrobeModel model)
        {
            if (config.looks == null) return;
            int value = 1;
            foreach (var look in config.looks)
            {
                if (look == null) continue;
                var outfit = model.Outfits.FirstOrDefault(o => o.Root == look.outfit);
                if (outfit == null)
                {
                    model.Warnings.Add(new ModelWarning("warn.look_no_outfit", config, Fallback(look.displayName, "?")));
                    continue;
                }
                var resolved = new ResolvedLook
                {
                    DisplayName = Fallback(look.displayName, outfit.DisplayName),
                    Icon = look.icon,
                    Value = value++,
                    OutfitIndex = outfit.Index,
                };
                // Pieces the look doesn't mention go back to their defaults.
                foreach (var element in outfit.Elements)
                {
                    var state = look.pieces?.FirstOrDefault(p => p != null && p.piece == element.Target);
                    resolved.Pieces.Add((element.ParameterName, state != null ? state.on : element.DefaultOn));
                }
                model.Looks.Add(resolved);
            }
        }

        internal static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        // Parameter-name keys: '/' is our separator, so anything that is not
        // a word character (Unicode-aware, so CJK names survive) becomes '_'.
        internal static string Sanitize(string name)
        {
            string clean = Regex.Replace(name ?? "", @"[^\w]", "_");
            return clean.Length == 0 ? "Unnamed" : clean;
        }

        internal static string UniqueKey(string key, HashSet<string> used)
        {
            string candidate = key;
            for (int n = 2; !used.Add(candidate); n++)
                candidate = $"{key}_{n}";
            return candidate;
        }
    }

    // All wardrobes of one avatar, resolved together so generated parameter
    // names never collide.
    public class WardrobeSet
    {
        public Transform AvatarRoot;
        public List<WardrobeModel> Models = new List<WardrobeModel>();

        public int TotalBits => Models.Sum(m => m.TotalBits);

        public static bool IsMobileBuild =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        public static WardrobeSet Resolve(Transform avatarRoot) => Resolve(avatarRoot, IsMobileBuild);

        public static WardrobeSet Resolve(Transform avatarRoot, bool mobile)
        {
            var set = new WardrobeSet { AvatarRoot = avatarRoot };
            var configs = avatarRoot.GetComponentsInChildren<YukiWardrobe>(true);

            var used = new HashSet<string>();
            foreach (var c in configs)
                if (!string.IsNullOrWhiteSpace(c.parameterName)) used.Add(c.parameterName.Trim());

            var explicitSeen = new HashSet<string>();
            foreach (var config in configs)
            {
                string parameter;
                if (!string.IsNullOrWhiteSpace(config.parameterName))
                {
                    parameter = config.parameterName.Trim();
                    if (!explicitSeen.Add(parameter))
                    {
                        // Two wardrobes asked for the same name: keep the first,
                        // rename the second so they don't fight.
                        parameter = WardrobeModel.UniqueKey(parameter, used);
                    }
                }
                else
                {
                    parameter = WardrobeModel.UniqueKey("Wardrobe/" + WardrobeModel.Sanitize(config.gameObject.name), used);
                }
                set.Models.Add(WardrobeModel.Resolve(config, avatarRoot, parameter, mobile));
            }
            return set;
        }

        public WardrobeModel For(YukiWardrobe config) => Models.FirstOrDefault(m => m.Config == config);
    }
}
