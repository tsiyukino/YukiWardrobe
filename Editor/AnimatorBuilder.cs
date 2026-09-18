using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace TsiYuki.Wardrobe.Editor
{
    // Builds the FX controller for one wardrobe:
    //  - an exclusive-switch layer driven by the int parameter,
    //  - one Direct Blend Tree layer holding every piece toggle,
    //  - one layer per outfit with color variants,
    //  - a local-only layer that applies Looks through parameter drivers.
    // Everything is created in memory; the caller persists each object
    // through the callback so play mode and upload can serialize it.
    // Every state animates every property its layer touches, so
    // write-defaults off is safe and works with either WD convention.
    public static class AnimatorBuilder
    {
        // Constant 1 used as the weight of every Direct Blend Tree child.
        public const string OneParameter = "TsiYuki/Wardrobe/One";

        public static AnimatorController Build(WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var controller = new AnimatorController { name = $"{model.MenuName} FX" };
            persist(controller);

            controller.AddParameter(model.ParameterName, AnimatorControllerParameterType.Int);
            BuildOutfitLayer(controller, model, persist);

            var elements = model.AllElements.ToList();
            if (elements.Count > 0)
            {
                controller.AddParameter(new AnimatorControllerParameter { name = OneParameter, type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
                // Pieces are synced bools; VRChat feeds them to Float animator
                // parameters, which blend trees need.
                foreach (var element in elements)
                    controller.AddParameter(new AnimatorControllerParameter
                    {
                        name = element.ParameterName,
                        type = AnimatorControllerParameterType.Float,
                        defaultFloat = element.DefaultOn ? 1 : 0,
                    });
                BuildPieceLayer(controller, model, elements, persist);
            }

            foreach (var outfit in model.Outfits.Where(o => o.VariantParameter != null))
            {
                controller.AddParameter(outfit.VariantParameter, AnimatorControllerParameterType.Int);
                BuildVariantLayer(controller, outfit, persist);
            }

            if (model.Looks.Count > 0)
            {
                controller.AddParameter(model.LookParameter, AnimatorControllerParameterType.Int);
                BuildLookLayer(controller, model, persist);
            }

            return controller;
        }

        // ------------------------------------------------------------ outfits

        static void BuildOutfitLayer(AnimatorController controller, WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, model.ParameterName, persist);

            var states = new List<(int index, AnimatorState state)>();
            if (model.IncludeNone)
            {
                var noneClip = BuildOutfitClip(model, null);
                persist(noneClip);
                states.Add((model.NoneIndex, AddState(sm, "None", noneClip, new Vector3(400, 0), persist)));
            }

            int y = 80;
            foreach (var outfit in model.Outfits)
            {
                var clip = BuildOutfitClip(model, outfit);
                persist(clip);
                states.Add((outfit.Index, AddState(sm, outfit.Key, clip, new Vector3(400, y), persist)));
                y += 60;
            }

            // Index 0 is what the synced parameter defaults to, so the state
            // machine must start there too.
            sm.defaultState = states.Find(s => s.index == 0).state ?? states[0].state;

            foreach (var (index, state) in states)
            {
                var transition = sm.AddAnyStateTransition(state);
                transition.canTransitionToSelf = false;
                transition.hasExitTime = false;
                transition.duration = 0;
                transition.AddCondition(AnimatorConditionMode.Equals, index, model.ParameterName);
                persist(transition);
            }
        }

        /// <summary>The pose of one outfit (or None when <paramref name="worn"/> is null).</summary>
        public static AnimationClip BuildOutfitClip(WardrobeModel model, ResolvedOutfit worn)
        {
            var clip = new AnimationClip { name = worn != null ? $"Wardrobe {worn.DisplayName}" : "Wardrobe None" };
            foreach (var outfit in model.Outfits)
                SetActiveCurve(clip, outfit.Path, outfit == worn);
            foreach (var channel in model.ObjectChannels)
            {
                bool active = worn != null && worn.ObjectValues.TryGetValue(channel.Path, out bool forced) ? forced : channel.Baseline;
                SetActiveCurve(clip, channel.Path, active);
            }
            foreach (var channel in model.BlendshapeChannels)
            {
                float weight = worn != null && worn.BlendshapeValues.TryGetValue(channel.Key, out float overridden) ? overridden : channel.Baseline;
                SetBlendshapeCurve(clip, channel, weight);
            }
            if (model.ChangeEffectPath != null)
            {
                // On for a moment after every switch, then off. The state is
                // entered once per switch and holds its last frame.
                var curve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(model.ChangeEffectDuration, 0));
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                }
                clip.SetCurve(model.ChangeEffectPath, typeof(GameObject), "m_IsActive", curve);
            }
            return clip;
        }

        // ------------------------------------------------------------- pieces

        static void BuildPieceLayer(AnimatorController controller, WardrobeModel model, List<ResolvedElement> elements, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, model.ParameterName + "/Pieces", persist);

            var root = new BlendTree
            {
                name = "Pieces",
                blendType = BlendTreeType.Direct,
                hideFlags = HideFlags.HideInHierarchy,
            };
            persist(root);

            var children = new List<ChildMotion>();
            foreach (var element in elements)
            {
                var onClip = new AnimationClip { name = $"{element.DisplayName} On" };
                SetActiveCurve(onClip, element.Path, true);
                persist(onClip);

                var offClip = new AnimationClip { name = $"{element.DisplayName} Off" };
                SetActiveCurve(offClip, element.Path, false);
                persist(offClip);

                var toggle = new BlendTree
                {
                    name = element.DisplayName,
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = element.ParameterName,
                    useAutomaticThresholds = false,
                    hideFlags = HideFlags.HideInHierarchy,
                };
                toggle.children = new[]
                {
                    new ChildMotion { motion = offClip, threshold = 0, timeScale = 1 },
                    new ChildMotion { motion = onClip, threshold = 1, timeScale = 1 },
                };
                persist(toggle);

                children.Add(new ChildMotion { motion = toggle, directBlendParameter = OneParameter, timeScale = 1 });
            }
            root.children = children.ToArray();

            var state = AddState(sm, "Pieces", root, new Vector3(400, 0), persist);
            sm.defaultState = state;
        }

        // ----------------------------------------------------------- variants

        static void BuildVariantLayer(AnimatorController controller, ResolvedOutfit outfit, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, outfit.VariantParameter, persist);
            AnimatorState first = null;
            int y = 0;
            foreach (var variant in outfit.Variants)
            {
                var clip = new AnimationClip { name = $"{outfit.DisplayName} {variant.DisplayName}" };
                foreach (var (path, type, slot, material) in variant.Materials)
                {
                    var binding = EditorCurveBinding.PPtrCurve(path, type, $"m_Materials.Array.data[{slot}]");
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, new[] { new ObjectReferenceKeyframe { time = 0, value = material } });
                }
                persist(clip);

                var state = AddState(sm, $"Variant {variant.Index}", clip, new Vector3(400, y), persist);
                y += 60;
                if (first == null) first = state;

                var transition = sm.AddAnyStateTransition(state);
                transition.canTransitionToSelf = false;
                transition.hasExitTime = false;
                transition.duration = 0;
                transition.AddCondition(AnimatorConditionMode.Equals, variant.Index, outfit.VariantParameter);
                persist(transition);
            }
            sm.defaultState = first;
        }

        // -------------------------------------------------------------- looks

        static void BuildLookLayer(AnimatorController controller, WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, model.LookParameter, persist);
            var idle = AddState(sm, "Idle", null, new Vector3(400, 0), persist);
            sm.defaultState = idle;

            int y = 80;
            foreach (var look in model.Looks)
            {
                var state = AddState(sm, $"Look {look.Value}", null, new Vector3(400, y), persist);
                y += 60;

                var driver = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.localOnly = true;
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter { type = VRC_AvatarParameterDriver.ChangeType.Set, name = model.ParameterName, value = look.OutfitIndex },
                };
                foreach (var (parameter, on) in look.Pieces)
                    driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter { type = VRC_AvatarParameterDriver.ChangeType.Set, name = parameter, value = on ? 1 : 0 });
                persist(driver);

                var enter = sm.AddAnyStateTransition(state);
                enter.canTransitionToSelf = false;
                enter.hasExitTime = false;
                enter.duration = 0;
                enter.AddCondition(AnimatorConditionMode.Equals, look.Value, model.LookParameter);
                persist(enter);

                var back = state.AddTransition(idle);
                back.hasExitTime = false;
                back.duration = 0;
                back.AddCondition(AnimatorConditionMode.NotEqual, look.Value, model.LookParameter);
                persist(back);
            }
        }

        // ------------------------------------------------------------ helpers

        static AnimatorStateMachine AddLayer(AnimatorController controller, string name, Action<UnityEngine.Object> persist)
        {
            var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
            persist(sm);
            controller.AddLayer(new AnimatorControllerLayer
            {
                name = name,
                defaultWeight = 1,
                stateMachine = sm,
            });
            return sm;
        }

        static AnimatorState AddState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position, Action<UnityEngine.Object> persist)
        {
            var state = sm.AddState(name, position);
            state.motion = motion;
            state.writeDefaultValues = false;
            persist(state);
            return state;
        }

        public static void SetActiveCurve(AnimationClip clip, string path, bool active)
        {
            clip.SetCurve(path, typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, active ? 1 : 0)));
        }

        public static void SetBlendshapeCurve(AnimationClip clip, BlendshapeChannel channel, float weight)
        {
            clip.SetCurve(channel.Path, typeof(SkinnedMeshRenderer), channel.Property, new AnimationCurve(new Keyframe(0, weight)));
        }
    }
}
