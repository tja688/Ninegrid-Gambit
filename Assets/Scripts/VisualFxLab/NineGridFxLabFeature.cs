using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室全屏 Look Feature（试验性）。
    /// 与 TableNineSelectiveLookFeature 同构：RenderGraph blit，挂在 Renderer2D 上、
    /// 事件 600（AfterRenderingPostProcessing，位于 Selective Look 之后，因此扫描线会跟随
    /// CRT 曲面等畸变，风味正确）。<see cref="FxLabState.Installed"/> 为 false 或
    /// Look 为 None 时不入队任何 Pass，零 GPU 开销。
    /// </summary>
    public sealed class NineGridFxLabFeature : ScriptableRendererFeature
    {
        [Tooltip("Hidden/NineGrid/FxLabLooks；留空时 Create 尝试 Shader.Find。")]
        public Shader lookShader;

        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingPostProcessing;

        internal const int PassCopy = 0;
        internal const int PassCrt = 1;
        internal const int PassCandle = 2;
        internal const int PassBloomPrefilter = 3;
        internal const int PassBlurH = 4;
        internal const int PassBlurV = 5;
        internal const int PassBloomComposite = 6;
        internal const int PassNoir = 7;
        internal const int PassFilm = 8;

        static class Ids
        {
            public static readonly int FxLabTime = Shader.PropertyToID("_FxLabTime");
            public static readonly int PixelResolution = Shader.PropertyToID("_PixelResolution");
            public static readonly int BloomTex = Shader.PropertyToID("_FxLabBloomTex");
            public static readonly int BlurTexel = Shader.PropertyToID("_FxBlurTexel");

            public static readonly int CrtCurvature = Shader.PropertyToID("_CrtCurvature");
            public static readonly int CrtAberration = Shader.PropertyToID("_CrtAberration");
            public static readonly int CrtGrille = Shader.PropertyToID("_CrtGrille");
            public static readonly int CrtVignette = Shader.PropertyToID("_CrtVignette");
            public static readonly int CrtFlicker = Shader.PropertyToID("_CrtFlicker");
            public static readonly int CrtCorner = Shader.PropertyToID("_CrtCorner");

            public static readonly int CandleWarmTint = Shader.PropertyToID("_CandleWarmTint");
            public static readonly int CandleShadowTint = Shader.PropertyToID("_CandleShadowTint");
            public static readonly int CandleVigColor = Shader.PropertyToID("_CandleVigColor");
            public static readonly int CandleShadowLift = Shader.PropertyToID("_CandleShadowLift");
            public static readonly int CandleCurve = Shader.PropertyToID("_CandleCurve");
            public static readonly int CandleVignette = Shader.PropertyToID("_CandleVignette");
            public static readonly int CandleFlicker = Shader.PropertyToID("_CandleFlicker");

            public static readonly int BloomThreshold = Shader.PropertyToID("_BloomThreshold");
            public static readonly int BloomKnee = Shader.PropertyToID("_BloomKnee");
            public static readonly int BloomIntensity = Shader.PropertyToID("_BloomIntensity");
            public static readonly int BloomTint = Shader.PropertyToID("_BloomTint");

            public static readonly int NoirDesat = Shader.PropertyToID("_NoirDesat");
            public static readonly int NoirContrast = Shader.PropertyToID("_NoirContrast");
            public static readonly int NoirGrain = Shader.PropertyToID("_NoirGrain");
            public static readonly int NoirVignette = Shader.PropertyToID("_NoirVignette");
            public static readonly int NoirShadowTint = Shader.PropertyToID("_NoirShadowTint");

            public static readonly int FilmSepia = Shader.PropertyToID("_FilmSepia");
            public static readonly int FilmGrain = Shader.PropertyToID("_FilmGrain");
            public static readonly int FilmScratch = Shader.PropertyToID("_FilmScratch");
            public static readonly int FilmVignette = Shader.PropertyToID("_FilmVignette");
            public static readonly int FilmFlicker = Shader.PropertyToID("_FilmFlicker");
            public static readonly int FilmJitter = Shader.PropertyToID("_FilmJitter");
        }

        Material _matMain;
        Material _matPrefilter;
        Material _matBlurH;
        Material _matBlurV;
        Material _matComposite;
        FxLabPass _pass;

        public override void Create()
        {
            if (lookShader == null)
            {
                lookShader = Shader.Find("Hidden/NineGrid/FxLabLooks");
            }

            if (lookShader != null)
            {
                _matMain = CoreUtils.CreateEngineMaterial(lookShader);
                _matPrefilter = CoreUtils.CreateEngineMaterial(lookShader);
                _matBlurH = CoreUtils.CreateEngineMaterial(lookShader);
                _matBlurV = CoreUtils.CreateEngineMaterial(lookShader);
                _matComposite = CoreUtils.CreateEngineMaterial(lookShader);
            }

            _pass = new FxLabPass();
            FxLabState.FeatureLoaded = _matMain != null;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_matMain);
            CoreUtils.Destroy(_matPrefilter);
            CoreUtils.Destroy(_matBlurH);
            CoreUtils.Destroy(_matBlurV);
            CoreUtils.Destroy(_matComposite);
            _matMain = null;
            FxLabState.FeatureLoaded = false;
            base.Dispose(disposing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_matMain == null)
            {
                return;
            }

            if (!FxLabState.Installed || FxLabState.Look == FxLabLook.None)
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

            Shader.SetGlobalFloat(Ids.FxLabTime, Time.unscaledTime);
            ApplyLookParams();

            _pass.Setup(this);
            _pass.renderPassEvent = passEvent;
            renderer.EnqueuePass(_pass);
        }

        void ApplyLookParams()
        {
            float s = Mathf.Clamp01(FxLabState.LookStrength);
            var m = _matMain;

            switch (FxLabState.Look)
            {
                case FxLabLook.CrtDeluxe:
                    m.SetFloat(Ids.CrtCurvature, 0.055f * s);
                    m.SetFloat(Ids.CrtAberration, 1.7f * s);
                    m.SetFloat(Ids.CrtGrille, 0.16f * s);
                    m.SetFloat(Ids.CrtVignette, 0.34f * s);
                    m.SetFloat(Ids.CrtFlicker, 0.028f * s);
                    m.SetFloat(Ids.CrtCorner, Mathf.Lerp(0.02f, 0.085f, s));
                    break;

                case FxLabLook.Candlelight:
                    m.SetColor(Ids.CandleWarmTint,
                        Color.Lerp(Color.white, new Color(1.09f, 0.97f, 0.83f), s));
                    m.SetColor(Ids.CandleShadowTint, new Color(0.38f, 0.2f, 0.07f));
                    m.SetColor(Ids.CandleVigColor, new Color(0.52f, 0.32f, 0.17f));
                    m.SetFloat(Ids.CandleShadowLift, 0.16f * s);
                    m.SetFloat(Ids.CandleCurve, 0.22f * s);
                    m.SetFloat(Ids.CandleVignette, 0.5f * s);
                    m.SetFloat(Ids.CandleFlicker, 0.06f * s);
                    break;

                case FxLabLook.BloomGlow:
                    _matPrefilter.SetFloat(Ids.BloomThreshold, 0.6f);
                    _matPrefilter.SetFloat(Ids.BloomKnee, 0.4f);
                    _matComposite.SetFloat(Ids.BloomIntensity, 1.5f * s);
                    _matComposite.SetColor(Ids.BloomTint, new Color(1f, 0.96f, 0.88f));
                    break;

                case FxLabLook.DungeonNoir:
                    m.SetFloat(Ids.NoirDesat, 0.5f * s);
                    m.SetFloat(Ids.NoirContrast, 1f + 0.22f * s);
                    m.SetFloat(Ids.NoirGrain, 0.055f * s);
                    m.SetFloat(Ids.NoirVignette, 0.48f * s);
                    m.SetColor(Ids.NoirShadowTint,
                        new Color(0.035f * s, 0.08f * s, 0.14f * s));
                    break;

                case FxLabLook.OldFilm:
                    m.SetFloat(Ids.FilmSepia, 0.52f * s);
                    m.SetFloat(Ids.FilmGrain, 0.07f * s);
                    m.SetFloat(Ids.FilmScratch, 0.85f * s);
                    m.SetFloat(Ids.FilmVignette, 0.38f * s);
                    m.SetFloat(Ids.FilmFlicker, 0.06f * s);
                    m.SetFloat(Ids.FilmJitter, 1.1f * s);
                    break;
            }
        }

        sealed class FxLabPass : ScriptableRenderPass
        {
            NineGridFxLabFeature _owner;

            public void Setup(NineGridFxLabFeature owner)
            {
                _owner = owner;
            }

            class BlurPassData
            {
                public TextureHandle Source;
                public Material Material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_owner == null || _owner._matMain == null)
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

                var srcDesc = renderGraph.GetTextureDesc(source);
                _owner._matMain.SetVector(Ids.PixelResolution,
                    new Vector4(Mathf.Max(1, srcDesc.width), Mathf.Max(1, srcDesc.height), 0f, 0f));

                var tempDesc = srcDesc;
                tempDesc.name = "_FxLabTemp";
                tempDesc.clearBuffer = false;
                var temp = renderGraph.CreateTexture(tempDesc);

                var look = FxLabState.Look;
                if (look == FxLabLook.BloomGlow)
                {
                    RecordBloom(renderGraph, source, temp, srcDesc);
                }
                else
                {
                    int passIndex = look switch
                    {
                        FxLabLook.CrtDeluxe => PassCrt,
                        FxLabLook.Candlelight => PassCandle,
                        FxLabLook.DungeonNoir => PassNoir,
                        FxLabLook.OldFilm => PassFilm,
                        _ => PassCopy,
                    };

                    var effectParams = new RenderGraphUtils.BlitMaterialParameters(
                        source, temp, _owner._matMain, passIndex);
                    renderGraph.AddBlitPass(effectParams, "FxLab Look");
                }

                var copyBack = new RenderGraphUtils.BlitMaterialParameters(
                    temp, source, _owner._matMain, PassCopy);
                renderGraph.AddBlitPass(copyBack, "FxLab Copy Back");
            }

            void RecordBloom(
                RenderGraph renderGraph,
                TextureHandle source,
                TextureHandle temp,
                in TextureDesc srcDesc)
            {
                var halfDesc = srcDesc;
                halfDesc.width = Mathf.Max(1, srcDesc.width >> 1);
                halfDesc.height = Mathf.Max(1, srcDesc.height >> 1);
                halfDesc.depthBufferBits = 0;
                halfDesc.msaaSamples = MSAASamples.None;
                halfDesc.clearBuffer = false;
                halfDesc.filterMode = FilterMode.Bilinear;

                halfDesc.name = "_FxLabBloomA";
                var bloomA = renderGraph.CreateTexture(halfDesc);
                halfDesc.name = "_FxLabBloomB";
                var bloomB = renderGraph.CreateTexture(halfDesc);
                halfDesc.name = "_FxLabBloomC";
                var bloomC = renderGraph.CreateTexture(halfDesc);

                var texel = new Vector4(1f / halfDesc.width, 1f / halfDesc.height, 0f, 0f);
                _owner._matBlurH.SetVector(Ids.BlurTexel, new Vector4(texel.x, texel.y, 1f, 0f));
                _owner._matBlurV.SetVector(Ids.BlurTexel, new Vector4(texel.x, texel.y, 0f, 1f));

                var prefilter = new RenderGraphUtils.BlitMaterialParameters(
                    source, bloomA, _owner._matPrefilter, PassBloomPrefilter);
                renderGraph.AddBlitPass(prefilter, "FxLab Bloom Prefilter");

                var blurH = new RenderGraphUtils.BlitMaterialParameters(
                    bloomA, bloomB, _owner._matBlurH, PassBlurH);
                renderGraph.AddBlitPass(blurH, "FxLab Bloom BlurH");

                // 纵向模糊走自定义 Raster Pass，结束后把结果发布为全局 _FxLabBloomTex 供合成采样。
                using (var builder = renderGraph.AddRasterRenderPass<BlurPassData>(
                    "FxLab Bloom BlurV", out var passData))
                {
                    passData.Source = bloomB;
                    passData.Material = _owner._matBlurV;
                    builder.UseTexture(bloomB);
                    builder.SetRenderAttachment(bloomC, 0);
                    builder.AllowPassCulling(false);
                    builder.SetGlobalTextureAfterPass(bloomC, Ids.BloomTex);
                    builder.SetRenderFunc(
                        (BlurPassData data, RasterGraphContext ctx) =>
                            Blitter.BlitTexture(
                                ctx.cmd, data.Source, new Vector4(1f, 1f, 0f, 0f),
                                data.Material, PassBlurV));
                }

                var composite = new RenderGraphUtils.BlitMaterialParameters(
                    source, temp, _owner._matComposite, PassBloomComposite);
                renderGraph.AddBlitPass(composite, "FxLab Bloom Composite");
            }
        }
    }
}
