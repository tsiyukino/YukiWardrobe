using System.Collections.Generic;
using System.Linq;
using nadena.dev.modular_avatar.core;
using nadena.dev.ndmf;
using TsiYuki.Core.Menus.Editor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(TsiYuki.Wardrobe.Editor.WardrobePlugin))]

namespace TsiYuki.Wardrobe.Editor
{
    // Converts every YukiWardrobe component into Modular Avatar primitives
    // (Merge Animator, Parameters, Menu Items) at build time, then removes
    // it. Runs in the Generating phase, before Modular Avatar, so MA's own
    // passes pick up the generated components and the absorbed menus.
    public class WardrobePlugin : Plugin<WardrobePlugin>
    {
        public override string QualifiedName => "moe.tsiyuki.wardrobe";
        public override string DisplayName => "Yuki Wardrobe";

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating)
                // Core settles menu placement once every TsiYuki tool has run,
                // so this has to be done by then.
                .BeforePlugin("moe.tsiyuki.core.menus")
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Generate wardrobe", Execute);
        }

        static void Execute(BuildContext ctx)
        {
            var configs = ctx.AvatarRootObject.GetComponentsInChildren<YukiWardrobe>(true);
            if (configs.Length == 0) return;

            // Build copies may never have been validated in the editor.
            foreach (var config in configs) config.EnsureIds();
            var set = WardrobeSet.Resolve(ctx.AvatarRootTransform);

            foreach (var model in set.Models)
                foreach (var warning in model.Warnings)
                    WardrobeText.Errors.Report(ErrorSeverity.NonFatal, warning.Key, warning.Context, warning.Args);
            foreach (var conflict in ConflictChecker.Check(set))
                WardrobeText.Errors.Report(ErrorSeverity.Information, conflict.Key, conflict.Objects.FirstOrDefault(), conflict.Args);

            foreach (var model in set.Models)
            {
                foreach (var outfit in model.Entries.Where(o => o.MenuMode == OutfitMenuMode.Hide))
                    foreach (var menu in outfit.Menus.Where(m => m.Installer != null))
                        Object.DestroyImmediate(menu.Installer);

                if (model.Entries.Count > 0)
                    Generate(ctx, model);

                // Outfits for the other platform are removed entirely.
                foreach (var root in model.ExcludedRoots)
                    if (root != null) Object.DestroyImmediate(root);
            }

            foreach (var config in configs)
                if (config != null) Object.DestroyImmediate(config);
        }

        static void Generate(BuildContext ctx, WardrobeModel model)
        {
            void Persist(Object asset) => ctx.AssetSaver.SaveAsset(asset);

            // Generated components go on a fresh object so removing the
            // config component (and its DisallowMultiple rule) never matters.
            var host = new GameObject($"{model.MenuName} (Yuki Wardrobe)");
            host.transform.SetParent(ctx.AvatarRootTransform, false);

            var merge = host.AddComponent<ModularAvatarMergeAnimator>();
            merge.animator = AnimatorBuilder.Build(model, Persist);
            merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
            merge.pathMode = MergeAnimatorPathMode.Absolute;
            merge.deleteAttachedAnimator = false;
            merge.matchAvatarWriteDefaults = false;

            var parameters = host.AddComponent<ModularAvatarParameters>();
            parameters.parameters = BuildParameters(model);

            var menuRoot = MenuGenerator.Build(model, host.transform);
            MenuPlacement.Place(model.Config, model.Config.menuParent, menuRoot, model.MenuName);
        }

        internal static List<ParameterConfig> BuildParameters(WardrobeModel model)
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

            foreach (var piece in model.AllPieces)
                list.Add(new ParameterConfig
                {
                    nameOrPrefix = piece.ParameterName,
                    syncType = ParameterSyncType.Bool,
                    defaultValue = piece.DefaultOn ? 1 : 0,
                    hasExplicitDefaultValue = true,
                    saved = model.Saved,
                });

            foreach (var outfit in model.Entries.Where(o => o.ColorParameter != null))
                list.Add(new ParameterConfig
                {
                    nameOrPrefix = outfit.ColorParameter,
                    syncType = ParameterSyncType.Int,
                    defaultValue = 0,
                    hasExplicitDefaultValue = true,
                    saved = model.Saved,
                });

            if (model.Looks.Count > 0)
                list.Add(new ParameterConfig
                {
                    nameOrPrefix = model.LookParameter,
                    syncType = ParameterSyncType.Int,
                    localOnly = true,
                    defaultValue = 0,
                    hasExplicitDefaultValue = true,
                    saved = false,
                });

            if (model.AllPieces.Any())
                list.Add(new ParameterConfig
                {
                    nameOrPrefix = AnimatorBuilder.OneParameter,
                    syncType = ParameterSyncType.NotSynced,
                    internalParameter = false,
                    defaultValue = 1,
                    hasExplicitDefaultValue = true,
                });

            return list;
        }
    }

    // Lets NDMF / Modular Avatar tools (parameter usage, budget displays)
    // see the parameters a wardrobe will create before it is built.
    [ParameterProviderFor(typeof(YukiWardrobe))]
    internal class WardrobeParameterProvider : IParameterProvider
    {
        readonly YukiWardrobe _component;

        public WardrobeParameterProvider(YukiWardrobe component) { _component = component; }

        public IEnumerable<ProvidedParameter> GetSuppliedParameters(BuildContext context = null)
        {
            var avatar = _component.GetComponentInParent<VRCAvatarDescriptor>();
            if (avatar == null) yield break;
            var model = WardrobeSet.Resolve(avatar.transform).For(_component);
            if (model == null || model.Entries.Count == 0) yield break;

            yield return Make(model.ParameterName, AnimatorControllerParameterType.Int, true, 0);
            foreach (var e in model.AllPieces)
                yield return Make(e.ParameterName, AnimatorControllerParameterType.Bool, true, e.DefaultOn ? 1 : 0);
            foreach (var o in model.Entries.Where(o => o.ColorParameter != null))
                yield return Make(o.ColorParameter, AnimatorControllerParameterType.Int, true, 0);
            if (model.Looks.Count > 0)
                yield return Make(model.LookParameter, AnimatorControllerParameterType.Int, false, 0);
        }

        ProvidedParameter Make(string name, AnimatorControllerParameterType type, bool synced, float defaultValue) =>
            new ProvidedParameter(name, ParameterNamespace.Animator, _component, WardrobePlugin.Instance, type)
            {
                WantSynced = synced,
                DefaultValue = defaultValue,
            };

        public void RemapParameters(ref System.Collections.Immutable.ImmutableDictionary<(ParameterNamespace, string), ParameterMapping> nameMap, BuildContext context)
        {
        }
    }
}
