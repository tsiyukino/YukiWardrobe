using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TsiYuki.Wardrobe.Editor
{
    // Renders a menu icon for an outfit: a copy of the avatar wearing it,
    // framed on the outfit's renderers, on a transparent background.
    public static class WardrobeIcons
    {
        const int Size = 256;
        public const string Folder = "Assets/TsiYukiData/Wardrobe Icons";

        public static Texture2D Capture(Transform avatarRoot, WardrobeModel model, ResolvedOutfit outfit)
        {
            // Render a posed copy of the avatar alone in a preview scene, so
            // nothing else in the open scene shows up and nothing is changed.
            var scene = EditorSceneManager.NewPreviewScene();
            var clone = Object.Instantiate(avatarRoot.gameObject);
            SceneManager.MoveGameObjectToScene(clone, scene);
            clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            RenderTexture rt = null;
            try
            {
                var pose = WardrobePreview.BuildPose(model, outfit, 0);
                ApplyPose(clone, pose);
                Object.DestroyImmediate(pose);

                var renderers = clone.GetComponentsInChildren<Renderer>()
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && (r is SkinnedMeshRenderer || r is MeshRenderer)).ToList();
                if (renderers.Count == 0) return null;
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                // Frame the avatar's height (T-pose arms may be cut at the sides).
                var animator = clone.GetComponent<Animator>();
                var feet = 0f;
                var top = bounds.max.y;
                var head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                if (head != null) top = Mathf.Min(top, head.position.y + (head.position.y - feet) * 0.16f);
                var center = new Vector3(bounds.center.x, (top + feet) * 0.5f, bounds.center.z);
                var halfHeight = (top - feet) * 0.52f;

                var camGo = new GameObject("Icon Camera");
                SceneManager.MoveGameObjectToScene(camGo, scene);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.scene = scene;
                cam.fieldOfView = 20;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 100;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                var distance = halfHeight / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                cam.transform.position = center + Vector3.forward * distance;
                cam.transform.LookAt(center);

                var key = new GameObject("Icon Light");
                SceneManager.MoveGameObjectToScene(key, scene);
                var light = key.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                key.transform.rotation = Quaternion.LookRotation(new Vector3(-0.3f, -0.4f, -1f));

                rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
                cam.targetTexture = rt;
                var ambient = RenderSettings.ambientLight;
                var ambientMode = RenderSettings.ambientMode;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.48f);
                cam.Render();
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambient;
                cam.targetTexture = null;

                var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;
                return Save(avatarRoot, model, outfit, texture);
            }
            finally
            {
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(clone);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void ApplyPose(GameObject root, AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var t = root.transform.Find(binding.path);
                if (t == null) continue;
                var value = AnimationUtility.GetEditorCurve(clip, binding).Evaluate(0);
                if (binding.type == typeof(GameObject) && binding.propertyName == "m_IsActive")
                    t.gameObject.SetActive(value > 0.5f);
                else if (binding.type == typeof(SkinnedMeshRenderer) && binding.propertyName.StartsWith("blendShape."))
                {
                    var smr = t.GetComponent<SkinnedMeshRenderer>();
                    var index = smr != null && smr.sharedMesh != null ? smr.sharedMesh.GetBlendShapeIndex(binding.propertyName.Substring(11)) : -1;
                    if (index >= 0) smr.SetBlendShapeWeight(index, value);
                }
            }
        }

        static Texture2D Save(Transform avatarRoot, WardrobeModel model, ResolvedOutfit outfit, Texture2D texture)
        {
            var folder = $"{Folder}/{WardrobeModel.Sanitize(avatarRoot.name)}";
            Directory.CreateDirectory(folder);
            var path = $"{folder}/{WardrobeModel.Sanitize(model.MenuName)}_{outfit.Key}.png";

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.maxTextureSize = Size;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
