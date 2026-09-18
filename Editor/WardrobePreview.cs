using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TsiYuki.Wardrobe.Editor
{
    // "Try on" in the editor: shows one outfit (with its pieces, blendshapes,
    // object overrides and a variant) in the scene through Unity's
    // AnimationMode, the same mechanism as the Animation window preview.
    // Nothing is written to the scene; stopping restores everything.
    [InitializeOnLoad]
    public static class WardrobePreview
    {
        static AnimationClip _clip;
        static GameObject _avatar;

        public static YukiWardrobe Wardrobe { get; private set; }
        public static WardrobeEntry Entry { get; private set; }
        public static int Color { get; private set; }
        public static bool Active => _avatar != null && AnimationMode.InAnimationMode();

        static WardrobePreview()
        {
            EditorApplication.playModeStateChanged += _ => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += (_, __) => Stop();
        }

        public static void Show(Transform avatarRoot, YukiWardrobe config, WardrobeEntry entry, int color = 0)
        {
            var model = WardrobeSet.Resolve(avatarRoot, WardrobeSet.IsMobileBuild).For(config);
            var outfit = model?.For(entry);
            if (outfit == null) return;

            Stop();
            var clip = BuildPose(model, outfit.IsNone ? null : outfit, color);
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            Apply(avatarRoot, clip);
            AnimationMode.EndSampling();

            _clip = clip;
            _avatar = avatarRoot.gameObject;
            Wardrobe = config;
            Entry = entry;
            Color = color;
            SceneView.RepaintAll();
        }

        public static void Stop()
        {
            if (_avatar != null && AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            if (_clip != null) Object.DestroyImmediate(_clip);
            _clip = null;
            _avatar = null;
            Wardrobe = null;
            Entry = null;
            SceneView.RepaintAll();
        }

        public static bool IsShowing(YukiWardrobe config, WardrobeEntry entry) =>
            Active && Wardrobe == config && Entry == entry;

        // Registers each property with AnimationMode (so it is restored on
        // Stop) and then sets it. Sampling the clip on the avatar root would
        // also drive the humanoid rig into a muscle pose, so it is not used.
        static void Apply(Transform root, AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var t = string.IsNullOrEmpty(binding.path) ? root : root.Find(binding.path);
                if (t == null) continue;
                var value = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(0);
                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                {
                    Register(binding, t.gameObject, "m_IsActive", t.gameObject.activeSelf ? "1" : "0");
                    t.gameObject.SetActive(value > 0.5f);
                }
                else if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    var index = smr != null && smr.sharedMesh != null ? smr.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11)) : -1;
                    if (index < 0) continue;
                    Register(binding, smr, $"m_BlendShapeWeights.Array.data[{index}]", smr.GetBlendShapeWeight(index).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    smr.SetBlendShapeWeight(index, value);
                }
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                var t = string.IsNullOrEmpty(binding.path) ? root : root.Find(binding.path);
                var renderer = t != null ? t.GetComponent(binding.type) as Renderer : null;
                if (renderer == null) continue;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (keys.Length == 0) continue;
                var match = System.Text.RegularExpressions.Regex.Match(binding.propertyName, @"\[(\d+)\]");
                if (!match.Success) continue;
                var slot = int.Parse(match.Groups[1].Value);
                var materials = renderer.sharedMaterials;
                if (slot >= materials.Length) continue;
                AnimationMode.AddPropertyModification(binding, new PropertyModification { target = renderer, propertyPath = binding.propertyName, objectReference = materials[slot] }, true);
                materials[slot] = keys[0].value as Material;
                renderer.sharedMaterials = materials;
            }
        }

        static void Register(EditorCurveBinding binding, Object target, string path, string value) =>
            AnimationMode.AddPropertyModification(binding, new PropertyModification { target = target, propertyPath = path, value = value }, true);

        /// <summary>Outfit pose plus default piece states and the chosen variant.</summary>
        internal static AnimationClip BuildPose(WardrobeModel model, ResolvedOutfit outfit, int color)
        {
            var clip = AnimatorBuilder.BuildOutfitClip(model, outfit);
            clip.hideFlags = HideFlags.HideAndDontSave;
            foreach (var piece in model.AllPieces)
                AnimatorBuilder.SetActiveCurve(clip, piece.Path, piece.DefaultOn);
            if (outfit != null && outfit.Colors.Count > 0)
            {
                var chosen = outfit.Colors[Mathf.Clamp(color, 0, outfit.Colors.Count - 1)];
                foreach (var (path, type, slot, material) in chosen.Materials)
                    AnimationUtility.SetObjectReferenceCurve(clip,
                        EditorCurveBinding.PPtrCurve(path, type, $"m_Materials.Array.data[{slot}]"),
                        new[] { new ObjectReferenceKeyframe { time = 0, value = material } });
            }
            return clip;
        }
    }
}
