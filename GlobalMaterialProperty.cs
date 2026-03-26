#if UNITY_EDITOR
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.ModularAvatar;
using peterfab;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

// This example uses NDMF. See https://github.com/bdunderscore/ndmf?tab=readme-ov-file#getting-started
[assembly: ExportsPlugin(typeof(GlobalMaterialPropertyPlugin))]
namespace peterfab
{
    [AddComponentMenu("peterfab/Global Material Property")]
    [DisallowMultipleComponent]
    public class GlobalMaterialProperty : MonoBehaviour, IEditorOnly
    {
        public string parameterName;
        public float defaultValue;
        public AnimationCurve curve;
        public string[] propertyNames;
    }

    public class GlobalMaterialPropertyPlugin : Plugin<GlobalMaterialPropertyPlugin>
    {
        public override string QualifiedName => "com.peterfab.globalmaterialproperty";
        public override string DisplayName => "Global Material Property";

        private const string SystemName = "GlobalMaterialProperty";
        private const bool UseWriteDefaults = true;

        protected override void Configure()
        {
            InPhase(BuildPhase.Generating).Run($"Generate {DisplayName}", Generate);
        }

        private void Generate(BuildContext ctx)
        {
            // Find all components of type GlobalMaterialProperty in this avatar.
            var components = ctx.AvatarRootTransform.GetComponentsInChildren<GlobalMaterialProperty>(true);
            if (components.Length == 0) return; // If there are none in the avatar, skip this entirely.

            var renderers = ctx.AvatarRootTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            // Initialize Animator As Code.
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = SystemName,
                AnimatorRoot = ctx.AvatarRootTransform,
                DefaultValueRoot = ctx.AvatarRootTransform,
                AssetKey = GUID.Generate().ToString(),
                AssetContainer = ctx.AssetContainer,
                ContainerMode = AacConfiguration.Container.OnlyWhenPersistenceRequired,
                // (For AAC 1.2.0 and above) The next line is recommended starting from NDMF 1.6.0.
                // If you use a lower version of NDMF or if you don't use it, remove that line.
                AssetContainerProvider = new NDMFContainerProvider(ctx),
                // States will be created with Write Defaults set to ON or OFF based on whether UseWriteDefaults is true or false.
                DefaultsProvider = new AacDefaultsProvider(UseWriteDefaults)
            });

            // Create a new object in the scene. We will add Modular Avatar components inside it.
            var modularAvatar = MaAc.Create(new GameObject(SystemName)
            {
                transform = { parent = ctx.AvatarRootTransform }
            });

            // Create a new animator controller.
            // This will be merged with the rest of the playable layer at the end of this function.
            var ctrl = aac.NewAnimatorController();

            // Create a new layer in that animator controller.
            var layer = ctrl.NewLayer();

            var blendTree = aac.NewBlendTree().Direct();

            foreach(var component in components)
            {
                var param = layer.FloatParameter(component.parameterName);
                layer.OverrideValue(param, component.defaultValue);

                blendTree.WithAnimation(aac.NewClip().Animating(clip =>
                {
                    foreach (var propertyName in component.propertyNames)
                    {
                        clip.Animates(renderers, $"material.{propertyName}")
                            .WithAnimationCurve(component.curve);
                    }
                }), param);

                modularAvatar.NewParameter(param);

                modularAvatar.EditMenuItem(component.gameObject).Radial(param).Name(component.parameterName);
            }

            // The first created state is the default one connected to the "Entry" node.
            var state = layer.NewState(SystemName)
                .WithAnimation(blendTree);

            // By creating a Modular Avatar Merge Animator component,
            // our animator controller will be added to the avatar's FX layer.
            modularAvatar.NewMergeAnimator(ctrl.AnimatorController, VRCAvatarDescriptor.AnimLayerType.FX);
        }
    }

    // (For AAC 1.2.0 and above) This is recommended starting from NDMF 1.6.0. You only need to define this class once.
    internal class NDMFContainerProvider : IAacAssetContainerProvider
    {
        private readonly BuildContext _ctx;
        public NDMFContainerProvider(BuildContext ctx) => _ctx = ctx;
        public void SaveAsPersistenceRequired(Object objectToAdd) => _ctx.AssetSaver.SaveAsset(objectToAdd);
        public void SaveAsRegular(Object objectToAdd) { } // Let NDMF crawl our assets when it finishes
        public void ClearPreviousAssets() { } // ClearPreviousAssets is never used in non-destructive contexts
    }
}
#endif
