using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TsiYuki.Wardrobe.Editor
{
    public class ResolvedPiece
    {
        public string DisplayName;
        public Texture2D Icon;
        public string Id;
        public string ParameterName;
        public string Path;
        public GameObject Target;
        public bool DefaultOn;
    }

    public class ResolvedColor
    {
        public string DisplayName;
        public Texture2D Icon;
        public int Index;
        public List<(string path, System.Type type, int slot, Material material)> Materials = new List<(string, System.Type, int, Material)>();
    }

    public class ResolvedOutfit
    {
        public WardrobeEntry Source;
        public string Id;
        public int Value;
        public bool IsNone;
        public bool IsDefault;
        public string DisplayName;
        public Texture2D Icon;
        public string Category; // "" = top level
        public string Path;     // null for None
        public GameObject Root; // null for None
        public List<ResolvedPiece> Pieces = new List<ResolvedPiece>();

        // Channel key → weight / path → active state this entry sets;
        // channels it doesn't mention fall back to the channel baseline.
        public Dictionary<string, float> BlendshapeValues = new Dictionary<string, float>();
        public Dictionary<string, bool> ObjectValues = new Dictionary<string, bool>();

        public OutfitMenuMode MenuMode;
        public List<OutfitMenu> Menus = new List<OutfitMenu>();

        public string ColorParameter; // null when the outfit has fewer than two colors
        public List<ResolvedColor> Colors = new List<ResolvedColor>();

        // Anything besides the "wear" toggle makes it a submenu.
        public bool HasSubmenu =>
            Pieces.Count > 0 || ColorParameter != null || (MenuMode == OutfitMenuMode.Absorb && Menus.Any(m => m.CanAbsorb));
    }

    public class ResolvedLook
    {
        public string DisplayName;
        public Texture2D Icon;
        public int Value; // 1-based button value; 0 is idle
        public int EntryValue;
        public List<(string parameter, bool on)> Pieces = new List<(string, bool)>();
    }

    public class ObjectChannel
    {
        public string Path;
        public GameObject Target;
        public bool Baseline; // scene active state at build time
    }

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

    // One YukiWardrobe resolved for a build or for display. Entry values
    // come from the component and never from list order; parameter value 0
    // means "the default entry", which is what VRChat resets a toggled-off
    // parameter to.
    public class WardrobeModel
    {
        public YukiWardrobe Config;
        public string ParameterName;
        public string LookParameter;
        public string MenuName;
        public Texture2D MenuIcon;
        public bool Saved;
        public List<ResolvedOutfit> Entries = new List<ResolvedOutfit>(); // menu order
        public ResolvedOutfit Default;
        public List<BlendshapeChannel> BlendshapeChannels = new List<BlendshapeChannel>();
        public List<ObjectChannel> ObjectChannels = new List<ObjectChannel>();
        public List<ResolvedLook> Looks = new List<ResolvedLook>();
        public string ChangeEffectPath;
        public float ChangeEffectDuration;
        public List<GameObject> ExcludedRoots = new List<GameObject>();
        public List<ModelWarning> Warnings = new List<ModelWarning>();

        public IEnumerable<ResolvedOutfit> Outfits => Entries.Where(e => !e.IsNone);
        public IEnumerable<ResolvedPiece> AllPieces => Entries.SelectMany(o => o.Pieces);

        public ResolvedOutfit For(WardrobeEntry entry) => Entries.FirstOrDefault(e => e.Source == entry);

        // One int for the outfit, one bool per piece, one int per outfit with colors.
        public int TotalBits => Entries.Count == 0 ? 0 : 8 + AllPieces.Count() + Entries.Count(o => o.ColorParameter != null) * 8;

        public static WardrobeModel Resolve(YukiWardrobe config, Transform avatarRoot, string parameterName, bool mobile)
        {
            var model = new WardrobeModel
            {
                Config = config,
                ParameterName = parameterName,
                LookParameter = parameterName + "/Look",
                MenuName = Fallback(config.displayName, config.gameObject.name),
                MenuIcon = config.icon,
                Saved = config.saved,
                ChangeEffectDuration = Mathf.Max(0.1f, config.changeEffectDuration),
            };

            var roots = new List<Transform>();
            var seenRoots = new HashSet<GameObject>();
            bool hasNone = false;
            foreach (var entry in config.entries)
            {
                if (entry == null) continue;
                if (entry.kind == EntryKind.None)
                {
                    if (hasNone) { model.Warnings.Add(new ModelWarning("warn.duplicate_none", config)); continue; }
                    hasNone = true;
                    continue;
                }
                if (entry.root == null) { model.Warnings.Add(new ModelWarning("warn.missing_root", config, Fallback(entry.displayName, "?"))); continue; }
                if (entry.root.transform == avatarRoot || !entry.root.transform.IsChildOf(avatarRoot))
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", entry.root, entry.root.name));
                else if (!seenRoots.Add(entry.root))
                    model.Warnings.Add(new ModelWarning("warn.duplicate_outfit", entry.root, entry.root.name));
                else if ((entry.platform == OutfitPlatform.PCOnly && mobile) || (entry.platform == OutfitPlatform.MobileOnly && !mobile))
                    model.ExcludedRoots.Add(entry.root);
                else
                    roots.Add(entry.root.transform);
            }

            var channels = new Dictionary<string, BlendshapeChannel>();
            var objectChannels = new Dictionary<string, ObjectChannel>();
            hasNone = false;
            foreach (var entry in config.entries)
            {
                if (entry == null) continue;
                if (entry.kind == EntryKind.None)
                {
                    if (hasNone) continue;
                    hasNone = true;
                    model.Entries.Add(new ResolvedOutfit
                    {
                        Source = entry,
                        Id = entry.id,
                        Value = entry.value,
                        IsNone = true,
                        DisplayName = Fallback(entry.displayName, WardrobeText.L["menu.none"]),
                        Icon = entry.icon,
                        Category = (entry.category ?? "").Trim(),
                    });
                    continue;
                }
                if (entry.root == null || !roots.Contains(entry.root.transform)) continue;

                var outfit = new ResolvedOutfit
                {
                    Source = entry,
                    Id = entry.id,
                    Value = entry.value,
                    DisplayName = Fallback(entry.displayName, entry.root.name),
                    Icon = entry.icon,
                    Category = (entry.category ?? "").Trim(),
                    Path = AnimationUtility.CalculateTransformPath(entry.root.transform, avatarRoot),
                    Root = entry.root,
                    MenuMode = entry.menuMode,
                    Menus = OutfitMenuScanner.Scan(entry.root),
                };
                ResolvePieces(entry, outfit, model, avatarRoot);
                ResolveBlendshapes(entry, outfit, model, avatarRoot, channels);
                ResolveObjectOverrides(entry, outfit, model, avatarRoot, roots, objectChannels);
                ResolveColors(entry, outfit, model, avatarRoot);
                model.Entries.Add(outfit);
            }

            var defaultSource = config.DefaultEntry;
            model.Default = model.Entries.FirstOrDefault(e => e.Source == defaultSource) ?? model.Entries.FirstOrDefault();
            if (model.Default != null) model.Default.IsDefault = true;

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

        static void ResolvePieces(WardrobeEntry entry, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot)
        {
            // Hierarchy order, so the menu matches what the creator sees.
            foreach (Transform child in entry.root.transform)
            {
                var piece = entry.FindPiece(child.gameObject);
                if (piece == null) continue;
                outfit.Pieces.Add(new ResolvedPiece
                {
                    DisplayName = Fallback(piece.displayName, child.name),
                    Icon = piece.icon,
                    Id = piece.id,
                    ParameterName = $"{model.ParameterName}/{piece.id}",
                    Path = AnimationUtility.CalculateTransformPath(child, avatarRoot),
                    Target = child.gameObject,
                    DefaultOn = child.gameObject.activeSelf,
                });
            }
            foreach (var piece in entry.pieces)
                if (piece != null && piece.target != null && piece.target.transform.parent != entry.root.transform)
                    model.Warnings.Add(new ModelWarning("warn.piece_not_child", piece.target, piece.target.name, entry.root.name));
        }

        static void ResolveBlendshapes(WardrobeEntry entry, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot, Dictionary<string, BlendshapeChannel> channels)
        {
            foreach (var over in entry.blendshapes)
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
                    model.Warnings.Add(new ModelWarning("warn.missing_blendshape", over.renderer, over.renderer.name, over.blendshape, outfit.DisplayName));
                    continue;
                }
                string path = AnimationUtility.CalculateTransformPath(over.renderer.transform, avatarRoot);
                string key = path + "|" + over.blendshape;
                if (!channels.TryGetValue(key, out var channel))
                {
                    channel = new BlendshapeChannel { Path = path, Renderer = over.renderer, Blendshape = over.blendshape, Baseline = over.renderer.GetBlendShapeWeight(shapeIndex) };
                    channels[key] = channel;
                    model.BlendshapeChannels.Add(channel);
                }
                outfit.BlendshapeValues[key] = over.value;
            }
        }

        static void ResolveObjectOverrides(WardrobeEntry entry, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot, List<Transform> roots, Dictionary<string, ObjectChannel> objectChannels)
        {
            foreach (var over in entry.objectOverrides)
            {
                if (over == null || over.target == null) continue;
                if (over.target.transform == avatarRoot || !over.target.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", over.target, over.target.name));
                    continue;
                }
                if (roots.Any(r => over.target.transform == r || over.target.transform.IsChildOf(r)))
                {
                    model.Warnings.Add(new ModelWarning("warn.override_inside_outfit", over.target, over.target.name, outfit.DisplayName));
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

        static void ResolveColors(WardrobeEntry entry, ResolvedOutfit outfit, WardrobeModel model, Transform avatarRoot)
        {
            if (entry.colors.Count < 2 || entry.colorSlots.Count == 0) return;
            var slots = new List<(int index, ColorSlot slot, Material scene)>();
            for (int i = 0; i < entry.colorSlots.Count; i++)
            {
                var s = entry.colorSlots[i];
                if (s == null || s.renderer == null) continue;
                if (!s.renderer.transform.IsChildOf(avatarRoot))
                {
                    model.Warnings.Add(new ModelWarning("warn.outside_avatar", s.renderer, s.renderer.name));
                    continue;
                }
                var shared = s.renderer.sharedMaterials;
                if (s.slot < 0 || s.slot >= shared.Length) continue;
                slots.Add((i, s, shared[s.slot]));
            }
            if (slots.Count == 0) return;

            outfit.ColorParameter = $"{model.ParameterName}/{entry.id}/Color";
            for (int c = 0; c < entry.colors.Count; c++)
            {
                var color = entry.colors[c];
                var resolved = new ResolvedColor
                {
                    DisplayName = Fallback(color.displayName, WardrobeText.L.Tr("menu.variant_n", c + 1)),
                    Icon = color.icon,
                    Index = c,
                };
                foreach (var (index, slot, scene) in slots)
                {
                    var material = index < color.materials.Count && color.materials[index] != null ? color.materials[index] : scene;
                    resolved.Materials.Add((AnimationUtility.CalculateTransformPath(slot.renderer.transform, avatarRoot), slot.renderer.GetType(), slot.slot, material));
                }
                outfit.Colors.Add(resolved);
            }
        }

        static void ResolveLooks(YukiWardrobe config, WardrobeModel model)
        {
            int value = 1;
            foreach (var look in config.looks)
            {
                if (look == null) continue;
                var entry = model.Entries.FirstOrDefault(e => e.Id == look.entryId);
                if (entry == null)
                {
                    model.Warnings.Add(new ModelWarning("warn.look_no_outfit", config, Fallback(look.displayName, "?")));
                    continue;
                }
                var resolved = new ResolvedLook
                {
                    DisplayName = Fallback(look.displayName, entry.DisplayName),
                    Icon = look.icon,
                    Value = value++,
                    EntryValue = entry.Value,
                };
                foreach (var piece in entry.Pieces)
                {
                    var state = look.pieces.FirstOrDefault(p => p != null && p.pieceId == piece.Id);
                    resolved.Pieces.Add((piece.ParameterName, state != null ? state.on : piece.DefaultOn));
                }
                model.Looks.Add(resolved);
            }
        }

        internal static string Fallback(string value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    // All wardrobes of one avatar, resolved together so parameter names
    // never collide.
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
            var used = new HashSet<string>();
            foreach (var config in avatarRoot.GetComponentsInChildren<YukiWardrobe>(true))
            {
                var name = !string.IsNullOrWhiteSpace(config.parameterName)
                    ? config.parameterName.Trim()
                    : "Wardrobe/" + (string.IsNullOrEmpty(config.id) ? config.gameObject.name : config.id);
                var model = WardrobeModel.Resolve(config, avatarRoot, name, mobile);
                if (!used.Add(name))
                    model.Warnings.Add(new ModelWarning("warn.duplicate_parameter", config, name));
                set.Models.Add(model);
            }
            return set;
        }

        public WardrobeModel For(YukiWardrobe config) => Models.FirstOrDefault(m => m.Config == config);
    }
}
