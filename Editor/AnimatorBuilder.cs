using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace TsiYuki.Wardrobe.Editor
{
    // Builds the FX controller for a wardrobe: one exclusive-switch layer
    // driven by the int parameter, plus one two-state layer per element
    // toggle. Everything is created in memory; the caller persists each
    // object through the callback so play mode and upload can serialize it.
    public static class AnimatorBuilder
    {
        public static AnimatorController Build(WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var controller = new AnimatorController { name = $"{model.MenuName} FX" };
            persist(controller);

            controller.AddParameter(model.ParameterName, AnimatorControllerParameterType.Int);
            foreach (var outfit in model.Outfits)
                foreach (var element in outfit.Elements)
                    controller.AddParameter(new AnimatorControllerParameter
                    {
                        name = element.ParameterName,
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = element.DefaultOn,
                    });

            BuildOutfitLayer(controller, model, persist);
            foreach (var outfit in model.Outfits)
                foreach (var element in outfit.Elements)
                    BuildElementLayer(controller, element, persist);

            return controller;
        }

        static void BuildOutfitLayer(AnimatorController controller, WardrobeModel model, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, model.MenuName, persist);

            var states = new List<(int index, AnimatorState state)>();
            if (model.IncludeNone)
            {
                var noneClip = new AnimationClip { name = "Wardrobe None" };
                foreach (var outfit in model.Outfits)
                    SetActiveCurve(noneClip, outfit.Path, false);
                foreach (var channel in model.ObjectChannels)
                    SetActiveCurve(noneClip, channel.Path, channel.Baseline);
                foreach (var channel in model.BlendshapeChannels)
                    SetBlendshapeCurve(noneClip, channel, channel.Baseline);
                persist(noneClip);
                states.Add((model.NoneIndex, AddState(sm, "None", noneClip, new Vector3(400, 0), persist)));
            }

            int y = 80;
            foreach (var outfit in model.Outfits)
            {
                var clip = new AnimationClip { name = $"Wardrobe {outfit.DisplayName}" };
                foreach (var other in model.Outfits)
                    SetActiveCurve(clip, other.Path, other == outfit);
                foreach (var channel in model.ObjectChannels)
                {
                    bool active = outfit.ObjectValues.TryGetValue(channel.Path, out bool forced)
                        ? forced
                        : channel.Baseline;
                    SetActiveCurve(clip, channel.Path, active);
                }
                foreach (var channel in model.BlendshapeChannels)
                {
                    float weight = outfit.BlendshapeValues.TryGetValue(channel.Key, out float overridden)
                        ? overridden
                        : channel.Baseline;
                    SetBlendshapeCurve(clip, channel, weight);
                }
                persist(clip);

                states.Add((outfit.Index, AddState(sm, outfit.DisplayName, clip, new Vector3(400, y), persist)));
                y += 60;
            }

            // Index 0 is what the synced parameter defaults to, so the state
            // machine must start there too.
            sm.defaultState = states.Find(s => s.index == 0).state;

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

        static void BuildElementLayer(AnimatorController controller, ResolvedElement element, Action<UnityEngine.Object> persist)
        {
            var sm = AddLayer(controller, element.ParameterName, persist);

            var onClip = new AnimationClip { name = $"{element.DisplayName} On" };
            SetActiveCurve(onClip, element.Path, true);
            persist(onClip);

            var offClip = new AnimationClip { name = $"{element.DisplayName} Off" };
            SetActiveCurve(offClip, element.Path, false);
            persist(offClip);

            var onState = AddState(sm, "On", onClip, new Vector3(400, 0), persist);
            var offState = AddState(sm, "Off", offClip, new Vector3(400, 80), persist);
            sm.defaultState = element.DefaultOn ? onState : offState;

            var toOff = onState.AddTransition(offState);
            toOff.hasExitTime = false;
            toOff.duration = 0;
            toOff.AddCondition(AnimatorConditionMode.IfNot, 0, element.ParameterName);
            persist(toOff);

            var toOn = offState.AddTransition(onState);
            toOn.hasExitTime = false;
            toOn.duration = 0;
            toOn.AddCondition(AnimatorConditionMode.If, 0, element.ParameterName);
            persist(toOn);
        }

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
            // Every state animates the full property set its layer touches,
            // so write-defaults off is safe and plays nice with either WD
            // convention on the rest of the avatar.
            state.writeDefaultValues = false;
            persist(state);
            return state;
        }

        static void SetActiveCurve(AnimationClip clip, string path, bool active)
        {
            clip.SetCurve(path, typeof(GameObject), "m_IsActive", new AnimationCurve(new Keyframe(0, active ? 1 : 0)));
        }

        static void SetBlendshapeCurve(AnimationClip clip, BlendshapeChannel channel, float weight)
        {
            clip.SetCurve(channel.Path, typeof(SkinnedMeshRenderer), channel.Property, new AnimationCurve(new Keyframe(0, weight)));
        }
    }
}
