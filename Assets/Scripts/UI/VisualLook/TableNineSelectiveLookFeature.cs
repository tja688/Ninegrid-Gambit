using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace NineGrid.VisualLook
{
    /// <summary>
    /// Selective look pipeline.
    /// Default (stable): Base camera runs Pass1 snap + Pass2 scanline; Overlay early-outs.
    /// Experimental: when <see cref="Settings.overlayOwnsScanline"/> is on, Base is snap-only and
    /// an Overlay camera with <see cref="TableNineFinalScanlineMarker"/> runs scanline-only.
    /// (Overlay AfterRendering blit is unreliable on the current URP stack — keep off unless verified.)
    /// </summary>
    public sealed class TableNineSelectiveLookFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;
            public RenderPassEvent overlayScanlineEvent = RenderPassEvent.AfterRendering;
            public Material lookMaterial;
            [Tooltip("Reserved for same-camera mesh mask path; UGUI 主路径请用相机栈。")]
            public Material maskMaterial;
            public LayerMask noSnapLayers;
            [Tooltip("同相机 mask 路径（Canvas 无效）；相机栈方案请保持 false。")]
            public bool buildNoSnapMask;
            [Tooltip("实验：由带 TableNineFinalScanlineMarker 的 Overlay 相机在栈末做 scanline-only blit。默认关闭（当前 URP 栈上不稳定）。")]
            public bool overlayOwnsScanline;
        }

        public Settings settings = new Settings();

        SelectiveLookPass _basePass;
        SelectiveLookPass _overlayPass;

        public override void Create()
        {
            _basePass = new SelectiveLookPass();
            _overlayPass = new SelectiveLookPass();
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

            var camera = renderingData.cameraData.camera;
            var isOverlay = renderingData.cameraData.renderType == CameraRenderType.Overlay;

            if (isOverlay)
            {
                if (!settings.overlayOwnsScanline)
                {
                    return;
                }

                if (camera == null || camera.GetComponent<TableNineFinalScanlineMarker>() == null)
                {
                    return;
                }

                _overlayPass.Setup(settings, PassMode.ScanlineOnly);
                _overlayPass.renderPassEvent = settings.overlayScanlineEvent;
                renderer.EnqueuePass(_overlayPass);
                return;
            }

            if (settings.overlayOwnsScanline)
            {
                _basePass.Setup(settings, PassMode.SnapOnly);
            }
            else
            {
                _basePass.Setup(settings, PassMode.SnapThenScanline);
            }

            _basePass.renderPassEvent = settings.passEvent;
            renderer.EnqueuePass(_basePass);
        }

        enum PassMode
        {
            SnapThenScanline,
            SnapOnly,
            ScanlineOnly
        }

        sealed class SelectiveLookPass : ScriptableRenderPass
        {
            static readonly int UseNoSnapMaskId = Shader.PropertyToID("_UseNoSnapMask");

            Settings _settings;
            PassMode _mode;

            public void Setup(Settings settings, PassMode mode)
            {
                _settings = settings;
                _mode = mode;
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

                _settings.lookMaterial.SetFloat(UseNoSnapMaskId, 0f);

                var desc = renderGraph.GetTextureDesc(source);
                desc.name = "_TableNineLookTemp";
                desc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(desc);

                if (_mode == PassMode.SnapThenScanline)
                {
                    var snapParams = new RenderGraphUtils.BlitMaterialParameters(
                        source, temp, _settings.lookMaterial, 1);
                    renderGraph.AddBlitPass(snapParams, "TableNine Look Snap");

                    var scanParams = new RenderGraphUtils.BlitMaterialParameters(
                        temp, source, _settings.lookMaterial, 2);
                    renderGraph.AddBlitPass(scanParams, "TableNine Look Scanlines");
                    return;
                }

                int effectPass = _mode == PassMode.SnapOnly ? 1 : 2;
                string effectName = _mode == PassMode.SnapOnly
                    ? "TableNine Look Snap"
                    : "TableNine Look Scanlines";

                var effectParams = new RenderGraphUtils.BlitMaterialParameters(
                    source, temp, _settings.lookMaterial, effectPass);
                renderGraph.AddBlitPass(effectParams, effectName);

                var copyParams = new RenderGraphUtils.BlitMaterialParameters(
                    temp, source, _settings.lookMaterial, 0);
                renderGraph.AddBlitPass(copyParams, "TableNine Look Copy");
            }
        }
    }
}
