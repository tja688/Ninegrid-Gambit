using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

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
            [Tooltip("权威路径：同相机重绘 NoPixelSnap（世界 TMP）到 _NoSnapMask，Snap 时保护这些像素。需 TableNine TMP 带 LightMode=Universal2D。相机栈逃 Snap 时保持 false。")]
            public bool buildNoSnapMask;
            [Tooltip("实验：由带 TableNineFinalScanlineMarker 的 Overlay 相机在栈末做 scanline-only blit。默认关闭（当前 URP 栈上不稳定）。")]
            public bool overlayOwnsScanline;
        }

        public Settings settings = new Settings();

        SelectiveLookPass _basePass;
        SelectiveLookPass _overlayPass;
        NoSnapMaskPass _maskPass;

        public override void Create()
        {
            _basePass = new SelectiveLookPass();
            _overlayPass = new SelectiveLookPass();
            _maskPass = new NoSnapMaskPass();
        }

        protected override void Dispose(bool disposing)
        {
            _maskPass?.Dispose();
            base.Dispose(disposing);
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

            if (settings.buildNoSnapMask && settings.maskMaterial != null)
            {
                _maskPass.Setup(settings);
                _maskPass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
                renderer.EnqueuePass(_maskPass);
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

                _settings.lookMaterial.SetFloat(UseNoSnapMaskId,
                    _settings.buildNoSnapMask && _settings.maskMaterial != null ? 1f : 0f);

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

        /// <summary>
        /// Redraws NoPixelSnap-layer meshes (world TMP text) into a screen-space mask via
        /// <see cref="Settings.maskMaterial"/>, then binds it as the global _NoSnapMask so the
        /// snap pass can leave those pixels un-snapped. Text stays on the base camera, so world
        /// sorting layers still occlude it naturally (cards over text, book over text, etc.).
        /// TableNine TMP shaders declare LightMode=Universal2D so this list can find them;
        /// SRPDefaultUnlit / UniversalForward remain for other unlit/legacy meshes on NoPixelSnap.
        /// </summary>
        sealed class NoSnapMaskPass : ScriptableRenderPass
        {
            static readonly int NoSnapMaskId = Shader.PropertyToID("_NoSnapMask");
            static readonly ShaderTagId[] MaskTags =
            {
                new ShaderTagId("Universal2D"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("UniversalForward"),
            };

            Settings _settings;

            class PassData
            {
                public RendererListHandle rendererList;
            }

            public void Setup(Settings settings)
            {
                _settings = settings;
            }

            public void Dispose()
            {
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_settings == null || _settings.maskMaterial == null)
                {
                    return;
                }

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();

                var maskDesc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
                maskDesc.name = "_TableNineNoSnapMask";
                maskDesc.depthBufferBits = 0;
                maskDesc.clearBuffer = true;
                maskDesc.clearColor = Color.black;
                maskDesc.colorFormat = GraphicsFormat.R8_UNorm;
                maskDesc.msaaSamples = MSAASamples.None;
                var maskTex = renderGraph.CreateTexture(maskDesc);

                var sorting = new SortingSettings(cameraData.camera)
                {
                    criteria = SortingCriteria.CommonTransparent,
                };
                var drawSettings = new DrawingSettings(MaskTags[0], sorting)
                {
                    overrideMaterial = _settings.maskMaterial,
                    overrideMaterialPassIndex = 0,
                };
                for (int i = 1; i < MaskTags.Length; i++)
                {
                    drawSettings.SetShaderPassName(i, MaskTags[i]);
                }

                var filter = new FilteringSettings(RenderQueueRange.all, _settings.noSnapLayers);
                var listParams = new RendererListParams(renderingData.cullResults, drawSettings, filter);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                    "TableNine NoSnap Mask", out var passData))
                {
                    passData.rendererList = renderGraph.CreateRendererList(listParams);
                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(maskTex, 0);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(maskTex, NoSnapMaskId);
                    builder.SetRenderFunc(
                        (PassData data, RasterGraphContext ctx) =>
                            ctx.cmd.DrawRendererList(data.rendererList));
                }
            }
        }
    }
}
