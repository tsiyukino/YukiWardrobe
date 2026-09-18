using System.Collections.Generic;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(TsiYuki.Wardrobe.Editor.WardrobePlugin))]

namespace TsiYuki.Wardrobe.Editor
{
    // Converts each YukiWardrobe component into Modular Avatar primitives at
    // build time, then removes it. Runs in the Generating phase so MA's own
    // passes (Transforming phase) pick up the generated components.
    public class WardrobePlugin : Plugin<WardrobePlugin>
    {
        public override string QualifiedName => "moe.tsiyuki.wardrobe";
        public override string DisplayName => "Yuki Wardrobe";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).Run("Generate wardrobe system", Execute);
        }

        static void Execute(BuildContext ctx)
        {
            var configs = ctx.AvatarRootObject.GetComponentsInChildren<YukiWardrobe>(true);
            if (configs.Length > 1)
                Debug.LogWarning("[Yuki Wardrobe] Multiple wardrobe components found on one avatar; " +
                                 "make sure their parameter names differ or they will fight over the same state.");

            foreach (var config in configs)
            {
                var model = WardrobeModel.Resolve(config, ctx.AvatarRootTransform);
                foreach (var warning in model.Warnings)
                    Debug.LogWarning($"[Yuki Wardrobe] {warning}", config.gameObject);

                if (model.Outfits.Count > 0)
                    Generate(ctx, config.gameObject, model);

                Object.DestroyImmediate(config);
            }
        }

        static void Generate(BuildContext ctx, GameObject host, WardrobeModel model)
        {
            void Persist(Object asset) => ctx.AssetSaver.SaveAsset(asset);

            var merge = host.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = AnimatorBuilder.Build(model, Persist);
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.deleteAttachedAnimator = false;
            merge.matchAvatarWriteDefaults = false;

            var parameters = host.AddComponent<ModularAvatarParameters>();
            parameters.parameters = BuildParameters(model);

            var installer = host.AddComponent<ModularAvatarMenuInstaller>();
            installer.menuToAppend = MenuBuilder.Build(model, Persist);
        }

        static List<ParameterConfig> BuildParameters(WardrobeModel model)
        {
            var list = new List<ParameterConfig>
            {
                new ParameterConfig
                {
                    nameOrPrefix = model.ParameterName,
                    syncType = ParameterSyncType.Int,
                    defaultValue = 0,
                    hasExplicitDefaultValue = true,
                    saved = model.Saved,
                },
            };

            foreach (var outfit in model.Outfits)
                foreach (var element in outfit.Elements)
                    list.Add(new ParameterConfig
                    {
                        nameOrPrefix = element.ParameterName,
                        syncType = ParameterSyncType.Bool,
                        defaultValue = element.DefaultOn ? 1 : 0,
                        hasExplicitDefaultValue = true,
                        saved = model.Saved,
                    });

            return list;
        }
    }
}
