using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace TsiYuki.Wardrobe.Editor
{
    public class Conflict
    {
        public string Key;
        public object[] Args;
        public List<Object> Objects = new List<Object>();
        public string Message => WardrobeText.L.Tr(Key, Args);
    }

    // Finds objects, blendshapes and material slots that a wardrobe controls
    // and something else also controls. Two controllers of the same property
    // fight: whichever evaluates last wins, which usually looks random.
    // Wardrobe only reports these; it never changes other tools' setup.
    public static class ConflictChecker
    {
        class Owner
        {
            public WardrobeModel Model;
            public string What; // outfit / piece / override label
        }

        public static List<Conflict> Check(WardrobeSet set)
        {
            var conflicts = new List<Conflict>();
            var avatarRoot = set.AvatarRoot;

            var objects = new Dictionary<GameObject, List<Owner>>();
            var shapes = new Dictionary<(SkinnedMeshRenderer, string), List<Owner>>();

            void AddObject(GameObject go, WardrobeModel m, string what)
            {
                if (go == null) return;
                if (!objects.TryGetValue(go, out var list)) objects[go] = list = new List<Owner>();
                list.Add(new Owner { Model = m, What = what });
            }

            foreach (var model in set.Models)
            {
                foreach (var outfit in model.Outfits)
                {
                    AddObject(outfit.Root, model, outfit.DisplayName);
                    foreach (var e in outfit.Pieces) AddObject(e.Target, model, e.DisplayName);
                }
                foreach (var c in model.ObjectChannels) AddObject(c.Target, model, c.Target.name);
                if (model.Config.changeEffect != null) AddObject(model.Config.changeEffect, model, model.Config.changeEffect.name);
                foreach (var c in model.BlendshapeChannels)
                {
                    var key = (c.Renderer, c.Blendshape);
                    if (!shapes.TryGetValue(key, out var l)) shapes[key] = l = new List<Owner>();
                    l.Add(new Owner { Model = model, What = c.Blendshape });
                }
            }

            // Wardrobes controlling the same thing.
            foreach (var pair in objects)
            {
                var models = pair.Value.Select(o => o.Model).Distinct().ToList();
                if (models.Count > 1)
                    conflicts.Add(Make("conflict.two_wardrobes", new Object[] { pair.Key }, pair.Key.name, string.Join(", ", models.Select(m => m.MenuName))));
            }
            foreach (var pair in shapes)
            {
                var models = pair.Value.Select(o => o.Model).Distinct().ToList();
                if (models.Count > 1)
                    conflicts.Add(Make("conflict.two_wardrobes", new Object[] { pair.Key.Item1 }, pair.Key.Item1.name + " / " + pair.Key.Item2, string.Join(", ", models.Select(m => m.MenuName))));
            }

            // Modular Avatar Object Toggles.
            foreach (var toggle in avatarRoot.GetComponentsInChildren<ModularAvatarObjectToggle>(true))
            {
                if (toggle.Objects == null) continue;
                foreach (var entry in toggle.Objects)
                {
                    var target = entry.Object?.Get(toggle);
                    if (target != null && objects.ContainsKey(target))
                        conflicts.Add(Make("conflict.ma_toggle", new Object[] { target, toggle }, target.name, Label(toggle)));
                }
            }

            // Modular Avatar Shape Changers.
            foreach (var changer in avatarRoot.GetComponentsInChildren<ModularAvatarShapeChanger>(true))
            {
                if (changer.Shapes == null) continue;
                foreach (var shape in changer.Shapes)
                {
                    var go = shape.Object?.Get(changer);
                    var renderer = go != null ? go.GetComponent<SkinnedMeshRenderer>() : null;
                    if (renderer != null && shapes.ContainsKey((renderer, shape.ShapeName)))
                        conflicts.Add(Make("conflict.ma_shape", new Object[] { renderer, changer }, renderer.name + " / " + shape.ShapeName, Label(changer)));
                }
            }

            // Animator controllers merged by MA or set on the descriptor.
            var controllers = new List<(RuntimeAnimatorController controller, Transform pathRoot, Object source)>();
            foreach (var merge in avatarRoot.GetComponentsInChildren<ModularAvatarMergeAnimator>(true))
            {
                if (merge.animator == null) continue;
                Transform pathRoot = avatarRoot;
                if (merge.pathMode == MergeAnimatorPathMode.Relative)
                {
                    var rel = merge.relativePathRoot?.Get(merge);
                    pathRoot = rel != null ? rel.transform : merge.transform;
                }
                controllers.Add((merge.animator, pathRoot, merge));
            }
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null && descriptor.customizeAnimationLayers)
                foreach (var layer in descriptor.baseAnimationLayers.Concat(descriptor.specialAnimationLayers))
                    if (!layer.isDefault && layer.animatorController != null)
                        controllers.Add((layer.animatorController, avatarRoot, descriptor));

            var byPath = objects.Keys.ToDictionary(go => go.transform, go => go);
            var reported = new HashSet<(Object, Object)>();
            foreach (var (controller, pathRoot, source) in controllers)
            {
                foreach (var clip in controller.animationClips.Distinct())
                {
                    if (clip == null) continue;
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        var t = string.IsNullOrEmpty(binding.path) ? pathRoot : pathRoot.Find(binding.path);
                        if (t == null) continue;
                        if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive" && byPath.TryGetValue(t, out var go))
                        {
                            if (reported.Add((go, source)))
                                conflicts.Add(Make("conflict.animator", new Object[] { go, source }, go.name, controller.name));
                        }
                        else if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                        {
                            var renderer = t.GetComponent<SkinnedMeshRenderer>();
                            var shape = binding.propertyName.Substring("blendShape.".Length);
                            if (renderer != null && shapes.ContainsKey((renderer, shape)) && reported.Add((renderer, source)))
                                conflicts.Add(Make("conflict.animator", new Object[] { renderer, source }, renderer.name + " / " + shape, controller.name));
                        }
                    }
                }
            }
            return conflicts;
        }

        static string Label(Component c)
        {
            var item = c.GetComponent<ModularAvatarMenuItem>();
            var name = item != null && !string.IsNullOrEmpty(item.label) ? item.label : c.gameObject.name;
            return $"{name} ({c.GetType().Name.Replace("ModularAvatar", "MA ")})";
        }

        static Conflict Make(string key, Object[] objects, params object[] args) =>
            new Conflict { Key = key, Args = args, Objects = objects.Where(o => o != null).ToList() };
    }
}
