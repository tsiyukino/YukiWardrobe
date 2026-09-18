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
        public string ParameterName;
        public string Path;
        public bool DefaultOn;
    }

    public class ResolvedOutfit
    {
        public string DisplayName;
        public int Index;
        public string Category;
        public string Path;
        public GameObject Root;
        public List<ResolvedElement> Elements = new List<ResolvedElement>();

        // Channel key → weight this outfit sets; channels it doesn't mention
        // fall back to the channel baseline.
        public Dictionary<string, float> BlendshapeValues = new Dictionary<string, float>();

        // Object-channel path → active state this outfit forces; channels it
        // doesn't mention fall back to the channel baseline.
        public Dictionary<string, bool> ObjectValues = new Dictionary<string, bool>();
    }

    // One avatar object forced on/off by at least one outfit. Every outfit
    // state animates every channel so values never linger across switches.
    public class ObjectChannel
    {
        public string Path;
        public bool Baseline; // scene active state at build time
    }

    // One avatar blendshape touched by at least one outfit. Every outfit
    // state animates every channel so values never linger across switches.
    public class BlendshapeChannel
    {
        public string Path;
        public string Blendshape;
        public float Baseline; // editor value at build time
        public string Property => "blendShape." + Blendshape;
        public string Key => Path + "|" + Blendshape;
    }

    // Build model resolved from a YukiWardrobe component. Index assignment
    // lives here and nowhere else: outfits keep their list order, so the
    // first outfit owns index 0 — the value VRChat resets a toggled-off
    // parameter to — making it the spawn default and the toggle-off
    // fallback. None, when enabled, takes the index above every outfit.
    public class WardrobeModel
    {
        public string ParameterName;
        public string MenuName;
        public bool Saved;
        public bool IncludeNone;
        public int NoneIndex; // meaningful only when IncludeNone
        public List<ResolvedOutfit> Outfits = new List<ResolvedOutfit>();
        public List<BlendshapeChannel> BlendshapeChannels = new List<BlendshapeChannel>();

        public List<ObjectChannel> ObjectChannels = new List<ObjectChannel>();

        public List<string> Warnings = new List<string>();

        // One int (8 bits) plus one bit per toggleable piece.
        public int TotalBits => 8 + Outfits.Sum(o => o.Elements.Count);

        public static WardrobeModel Resolve(YukiWardrobe config, Transform avatarRoot)
        {
            var model = new WardrobeModel
            {
                ParameterName = Fallback(config.parameterName, "WardrobeState"),
                MenuName = Fallback(config.menuName, "Wardrobe"),
                Saved = config.saved,
                IncludeNone = config.includeNone,
            };

            var seen = new HashSet<GameObject>();
            var valid = new List<OutfitGroup>();
            foreach (var group in config.outfits)
            {
                if (group.root == null) continue;
                if (group.root.transform == avatarRoot || !group.root.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add($"'{group.root.name}' is not inside the avatar; skipped.");
                    continue;
                }
                if (!seen.Add(group.root))
                {
                    model.Warnings.Add($"'{group.root.name}' is listed more than once; duplicates skipped.");
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
                    DisplayName = group.root.name,
                    Index = i,
                    Category = Fallback(group.category, "Default"),
                    Path = AnimationUtility.CalculateTransformPath(group.root.transform, avatarRoot),
                    Root = group.root,
                };

                ResolveElements(group, outfit, key, model, avatarRoot);
                ResolveBlendshapes(group, outfit, model, avatarRoot, channels);
                ResolveObjectOverrides(group, outfit, model, avatarRoot, valid, objectChannels);
                model.Outfits.Add(outfit);
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
                    model.Warnings.Add($"Piece '{piece.name}' is not a direct child of '{group.root.name}'; skipped.");
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
                outfit.Elements.Add(new ResolvedElement
                {
                    DisplayName = child.name,
                    ParameterName = $"{model.ParameterName}/{outfitKey}/{elementKey}",
                    Path = AnimationUtility.CalculateTransformPath(child, avatarRoot),
                    DefaultOn = child.gameObject.activeSelf,
                });
            }
        }

        static void ResolveBlendshapes(OutfitGroup group, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot, Dictionary<string, BlendshapeChannel> channels)
        {
            if (group.blendshapes == null) return;

            foreach (var over in group.blendshapes)
            {
                if (over.renderer == null || string.IsNullOrEmpty(over.blendshape)) continue;
                if (!over.renderer.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add($"Blendshape target '{over.renderer.name}' is not inside the avatar; skipped.");
                    continue;
                }
                var mesh = over.renderer.sharedMesh;
                int shapeIndex = mesh == null ? -1 : mesh.GetBlendShapeIndex(over.blendshape);
                if (shapeIndex < 0)
                {
                    model.Warnings.Add($"'{over.renderer.name}' has no blendshape '{over.blendshape}'; skipped on '{group.root.name}'.");
                    continue;
                }

                string path = AnimationUtility.CalculateTransformPath(over.renderer.transform, avatarRoot);
                string channelKey = path + "|" + over.blendshape;
                if (!channels.TryGetValue(channelKey, out var channel))
                {
                    channel = new BlendshapeChannel
                    {
                        Path = path,
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
                if (over.target == null) continue;
                if (over.target.transform == avatarRoot || !over.target.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add($"Object override '{over.target.name}' is not inside the avatar; skipped.");
                    continue;
                }
                if (validGroups.Any(g => over.target.transform == g.root.transform || over.target.transform.IsChildOf(g.root.transform)))
                {
                    model.Warnings.Add($"Object override '{over.target.name}' is inside an outfit and already controlled by outfit switching; skipped on '{group.root.name}'.");
                    continue;
                }

                string path = AnimationUtility.CalculateTransformPath(over.target.transform, avatarRoot);
                if (!objectChannels.TryGetValue(path, out var channel))
                {
                    channel = new ObjectChannel { Path = path, Baseline = over.target.activeSelf };
                    objectChannels[path] = channel;
                    model.ObjectChannels.Add(channel);
                }
                outfit.ObjectValues[path] = over.enable;
            }
        }

        static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        // Parameter-name keys: '/' is our separator, so anything that is not
        // a word character (Unicode-aware, so CJK names survive) becomes '_'.
        static string Sanitize(string name)
        {
            string clean = Regex.Replace(name ?? "", @"[^\w]", "_");
            return clean.Length == 0 ? "Unnamed" : clean;
        }

        static string UniqueKey(string key, HashSet<string> used)
        {
            string candidate = key;
            for (int n = 2; !used.Add(candidate); n++)
                candidate = $"{key}_{n}";
            return candidate;
        }
    }
}
