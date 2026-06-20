using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;

namespace NineGrid.Presentation.Visuals
{
    public sealed class TableNinePixelSnapRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Material passMaterial;
        [SerializeField] private Material maskMaterial;
        [SerializeField] private string noSnapLayerName = "NoSnapping";
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

        private TableNinePixelSnapPass m_Pass;

        public Material PassMaterial
        {
            get => passMaterial;
            set => passMaterial = value;
        }

        public Material MaskMaterial
        {
            get => maskMaterial;
            set => maskMaterial = value;
        }

        public override void Create()
        {
            m_Pass = new TableNinePixelSnapPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (passMaterial == null || maskMaterial == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview
                || renderingData.cameraData.cameraType == CameraType.Reflection
                || UniversalRenderer.IsOffscreenDepthTexture(ref renderingData.cameraData))
                return;

            int layerMask = LayerMask.GetMask(noSnapLayerName);
            m_Pass.Setup(passMaterial, maskMaterial, layerMask, injectionPoint, name);
            m_Pass.requiresIntermediateTexture = true;
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
        }

        private sealed class TableNinePixelSnapPass : ScriptableRenderPass
        {
            private static readonly List<ShaderTagId> ShaderTags = new()
            {
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("Universal2D"),
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
            };

            private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int NoSnapMaskId = Shader.PropertyToID("_NoSnapMask");
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();

            private Material m_PassMaterial;
            private Material m_MaskMaterial;
            private int m_LayerMask;
            private RTHandle m_MaskTexture;

            private class MaskPassData
            {
                internal RendererListHandle rendererListHandle;
            }

            private class FinalPassData
            {
                internal Material material;
                internal TextureHandle source;
            }

            public void Setup(
                Material passMaterial,
                Material maskMaterial,
                int layerMask,
                RenderPassEvent renderPassEvent,
                string passName)
            {
                m_PassMaterial = passMaterial;
                m_MaskMaterial = maskMaterial;
                m_LayerMask = layerMask;
                this.renderPassEvent = renderPassEvent;
                profilingSampler = new ProfilingSampler(passName);
            }

            public void Dispose()
            {
                m_MaskTexture?.Release();
            }

            private void ReAllocateMaskTexture(RenderTextureDescriptor descriptor)
            {
                descriptor.msaaSamples = 1;
                descriptor.depthStencilFormat = GraphicsFormat.None;
                descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref m_MaskTexture,
                    descriptor,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    name: "_TableNineNoSnapMask");
            }

            private static DrawingSettings CreateMaskDrawingSettings(UniversalCameraData cameraData, Material overrideMaterial)
            {
                SortingSettings sortingSettings = new SortingSettings(cameraData.camera)
                {
                    criteria = SortingCriteria.CommonTransparent
                };

                DrawingSettings drawingSettings = new DrawingSettings(ShaderTags[0], sortingSettings);
                for (int i = 1; i < ShaderTags.Count; i++)
                    drawingSettings.SetShaderPassName(i, ShaderTags[i]);

                drawingSettings.overrideMaterial = overrideMaterial;
                drawingSettings.overrideMaterialPassIndex = 0;
                drawingSettings.enableDynamicBatching = false;
                drawingSettings.enableInstancing = cameraData.camera.cameraType != CameraType.Preview;
                drawingSettings.perObjectData = PerObjectData.None;
                return drawingSettings;
            }

            private static RendererListHandle CreateMaskRendererList(
                RenderGraph renderGraph,
                ref CullingResults cullResults,
                DrawingSettings drawingSettings,
                FilteringSettings filteringSettings)
            {
                NativeArray<ShaderTagId> tagValues = new NativeArray<ShaderTagId>(1, Allocator.Temp);
                NativeArray<RenderStateBlock> stateBlocks = new NativeArray<RenderStateBlock>(1, Allocator.Temp);
                tagValues[0] = ShaderTagId.none;
                stateBlocks[0] = new RenderStateBlock(RenderStateMask.Nothing);

                RendererListParams param = new RendererListParams(cullResults, drawingSettings, filteringSettings)
                {
                    tagValues = tagValues,
                    stateBlocks = stateBlocks,
                    isPassTagName = false
                };

                RendererListHandle handle = renderGraph.CreateRendererList(param);
                tagValues.Dispose();
                stateBlocks.Dispose();
                return handle;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_PassMaterial == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();

                ReAllocateMaskTexture(cameraData.cameraTargetDescriptor);
                TextureHandle maskTexture = renderGraph.ImportTexture(m_MaskTexture);

                if (m_MaskMaterial != null && m_LayerMask != 0)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                               passName + " Mask",
                               out MaskPassData maskPassData,
                               profilingSampler))
                    {
                        builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                        builder.AllowPassCulling(false);
                        builder.AllowGlobalStateModification(true);

                        var filteringSettings = new FilteringSettings(RenderQueueRange.all, m_LayerMask);
                        var drawingSettings = CreateMaskDrawingSettings(cameraData, m_MaskMaterial);
                        maskPassData.rendererListHandle = CreateMaskRendererList(
                            renderGraph,
                            ref renderingData.cullResults,
                            drawingSettings,
                            filteringSettings);

                        builder.UseRendererList(maskPassData.rendererListHandle);

                        builder.SetRenderFunc((MaskPassData data, RasterGraphContext context) =>
                        {
                            context.cmd.ClearRenderTarget(true, false, Color.black);
                            context.cmd.DrawRendererList(data.rendererListHandle);
                        });
                    }
                }
                else
                {
                    using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                               passName + " Mask Clear",
                               out _,
                               profilingSampler))
                    {
                        builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                        builder.AllowPassCulling(false);

                        builder.SetRenderFunc((MaskPassData _, RasterGraphContext context) =>
                        {
                            context.cmd.ClearRenderTarget(true, false, Color.black);
                        });
                    }
                }

                var copyDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                copyDesc.name = "_CameraColorTableNinePixelSnapCopy";
                copyDesc.clearBuffer = false;

                TextureHandle source = resourceData.activeColorTexture;
                TextureHandle copy = renderGraph.CreateTexture(copyDesc);
                renderGraph.AddBlitPass(source, copy, Vector2.one, Vector2.zero, passName: passName + " Copy");

                using (var builder = renderGraph.AddRasterRenderPass<FinalPassData>(
                           passName,
                           out FinalPassData finalPassData,
                           profilingSampler))
                {
                    finalPassData.material = m_PassMaterial;
                    finalPassData.source = copy;

                    builder.UseTexture(copy, AccessFlags.Read);
                    builder.UseTexture(maskTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((FinalPassData data, RasterGraphContext context) =>
                    {
                        SharedPropertyBlock.Clear();
                        SharedPropertyBlock.SetTexture(BlitTextureId, data.source);
                        SharedPropertyBlock.SetTexture(NoSnapMaskId, m_MaskTexture);
                        SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));

                        context.cmd.DrawProcedural(
                            Matrix4x4.identity,
                            data.material,
                            0,
                            MeshTopology.Triangles,
                            3,
                            1,
                            SharedPropertyBlock);
                    });
                }
            }
        }
    }
}
