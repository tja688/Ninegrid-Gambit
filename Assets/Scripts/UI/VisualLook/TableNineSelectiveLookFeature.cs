using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// Base camera only: pixel-snap the frame, then apply scanlines.
    /// Pair with <see cref="TableNineLookRig"/> so NoPixelSnap UI rides an Overlay camera and skips this pass.
    /// </summary>
    public sealed class TableNineSelectiveLookFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public Material lookMaterial;
            [Tooltip("Reserved for same-camera mesh mask path; UGUI 主路径请用相机栈。")]
            public Material maskMaterial;
            public LayerMask noSnapLayers;
            [Tooltip("同相机 mask 路径（Canvas 无效）；相机栈方案请保持 false。")]
            public bool buildNoSnapMask;
        }

        public Settings settings = new Settings();

        SelectiveLookPass _pass;

        public override void Create()
        {
            _pass = new SelectiveLookPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.lookMaterial == null)
            {
                return;
            }

            if (renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
            {
                return;
            }

            _pass.Setup(settings);
            _pass.renderPassEvent = settings.passEvent;
            renderer.EnqueuePass(_pass);
        }

        sealed class SelectiveLookPass : ScriptableRenderPass
        {
            static readonly int UseNoSnapMaskId = Shader.PropertyToID("_UseNoSnapMask");

            Settings _settings;

            public void Setup(Settings settings)
            {
                _settings = settings;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _settings.lookMaterial == null)
                {
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                var source = resourceData.activeColorTexture;
                if (!source.IsValid())
                {
                    return;
                }

                // Camera-stack path: no mask. Always snap unprotected base color.
                _settings.lookMaterial.SetFloat(UseNoSnapMaskId, 0f);

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_TableNineLookTemp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                // Pass 1: selective / full snap into temp
                var snapParams = new RenderGraphUtils.BlitMaterialParameters(
                    source,
                    temp,
                    _settings.lookMaterial,
                    1);
                renderGraph.AddBlitPass(snapParams, "TableNine Look Snap");

                // Pass 2: scanlines temp -> camera color
                var scanParams = new RenderGraphUtils.BlitMaterialParameters(
                    temp,
                    source,
                    _settings.lookMaterial,
                    2);
                renderGraph.AddBlitPass(scanParams, "TableNine Look Scanlines");
            }
        }
    }
}
