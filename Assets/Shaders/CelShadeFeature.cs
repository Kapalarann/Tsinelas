using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CelShadeFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;

        [Header("Posterization")]
        [Tooltip("Number of discrete color bands. 3-5 = BotW feel. Lower = more aggressive.")]
        [Range(2f, 16f)]  public float posterizeSteps     = 5f;
        [Tooltip("How strongly posterization is applied. 0 = off, 1 = full.")]
        [Range(0f, 1f)]   public float posterizeStrength  = 0.75f;

        [Header("Saturation")]
        [Tooltip("Boosts color vibrancy to compensate for flat banding.")]
        [Range(0f, 1f)]   public float saturationBoost    = 0.25f;

        [Header("Outlines")]
        [Tooltip("Outline width in pixels.")]
        [Range(0f, 4f)]   public float outlineThickness   = 1.0f;
        [Tooltip("How sensitive depth edges are. Lower = more outlines.")]
        [Range(0f, 1f)]   public float depthThreshold     = 0.20f;
        [Tooltip("Multiplier on depth gradient strength.")]
        [Range(0f, 20f)]  public float depthScale         = 8.0f;
        [Tooltip("How sensitive normal edges are. Lower = more outlines on curved surfaces.")]
        [Range(0f, 1f)]   public float normalThreshold    = 0.25f;
        [Tooltip("Outline color and opacity (alpha controls blend strength).")]
        [ColorUsage(false, false)]
        public Color outlineColor = new Color(0.15f, 0.15f, 0.18f);
        [Range(0f, 1f)]   public float outlineOpacity       = 0.70f;

        [Header("Depth Limits (stops halo around island edges)")]
        [Tooltip("Pixels with depth below this are skipped for outlines (water, sky, far terrain). 0=near, 1=far in NDC.")]
        [Range(0f, 1f)]   public float outlineMaxDepth      = 0.001f;
        [Tooltip("Max depth difference between a pixel and its neighbours before the edge is ignored as a silhouette.")]
        [Range(0f, 0.1f)] public float outlineMaxDepthDelta = 0.002f;

        [Header("Outline Exclusion")]
        [Tooltip("Layers that must NOT be crossed by the outline/posterize — your UI layer and any " +
                 "world-space sprite layer (badges, glows, ping icons, etc.). These still get cel-shaded " +
                 "with the rest of the frame, then are RE-DRAWN cleanly on top, so the outline never sits " +
                 "over them. Leave transparent VFX OUT of this mask so they keep getting cel-shaded.")]
        public LayerMask excludeLayers = 0;
    }

    public Settings settings = new Settings();
    Material _mat;
    CelShadePass _pass;

    static readonly int ID_PosterizeSteps         = Shader.PropertyToID("_PosterizeSteps");
    static readonly int ID_PosterizeStrength      = Shader.PropertyToID("_PosterizeStrength");
    static readonly int ID_SaturationBoost        = Shader.PropertyToID("_SaturationBoost");
    static readonly int ID_OutlineMaxDepth        = Shader.PropertyToID("_OutlineMaxDepth");
    static readonly int ID_OutlineMaxDepthDelta   = Shader.PropertyToID("_OutlineMaxDepthDelta");
    static readonly int ID_OutlineThickness       = Shader.PropertyToID("_OutlineThickness");
    static readonly int ID_OutlineDepthThreshold  = Shader.PropertyToID("_OutlineDepthThreshold");
    static readonly int ID_OutlineDepthScale      = Shader.PropertyToID("_OutlineDepthScale");
    static readonly int ID_OutlineNormalThreshold = Shader.PropertyToID("_OutlineNormalThreshold");
    static readonly int ID_OutlineColor           = Shader.PropertyToID("_OutlineColor");

    public override void Create()
    {
        _mat = settings.material;

        if (_mat == null)
        {
            Debug.LogWarning("[CelShadeFeature] No material assigned. " +
                             "Create a material using Custom/URP/CelShadeFilter and assign it.");
            return;
        }

        _pass = new CelShadePass(_mat);
        // Full-frame: runs after ALL transparents, so opaque world AND transparent VFX get cel-shaded.
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
    {
        if (_mat == null) return;

        Color oc = settings.outlineColor;
        _mat.SetFloat(ID_PosterizeSteps,         settings.posterizeSteps);
        _mat.SetFloat(ID_PosterizeStrength,      settings.posterizeStrength);
        _mat.SetFloat(ID_SaturationBoost,        settings.saturationBoost);
        _mat.SetFloat(ID_OutlineThickness,       settings.outlineThickness);
        _mat.SetFloat(ID_OutlineDepthThreshold,  settings.depthThreshold);
        _mat.SetFloat(ID_OutlineDepthScale,      settings.depthScale);
        _mat.SetFloat(ID_OutlineNormalThreshold, settings.normalThreshold);
        _mat.SetVector(ID_OutlineColor,          new Vector4(oc.r, oc.g, oc.b, settings.outlineOpacity));
        _mat.SetFloat(ID_OutlineMaxDepth,        settings.outlineMaxDepth);
        _mat.SetFloat(ID_OutlineMaxDepthDelta,   settings.outlineMaxDepthDelta);

        _pass.SetExcludeLayers(settings.excludeLayers);
        renderer.EnqueuePass(_pass);
    }

    class CelShadePass : ScriptableRenderPass
    {
        Material _mat;
        int _excludeLayers;

        // UGUI uses SRPDefaultUnlit; SpriteRenderers use these forward tags / Universal2D.
        static readonly List<ShaderTagId> s_Tags = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("Universal2D"),
        };

        public CelShadePass(Material m)
        {
            _mat = m;
            // Make sure depth + normals prepasses run and their textures are bound.
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        public void SetExcludeLayers(int mask) => _excludeLayers = mask;

        class PassData
        {
            public TextureHandle src;
            public Material      mat;
        }

        class RedrawData
        {
            public RendererListHandle list;
        }

        public override void RecordRenderGraph(RenderGraph rg, ContextContainer ctx)
        {
            var resourceData = ctx.Get<UniversalResourceData>();

            // ── 1) Cel-shade the whole frame into a temp target ──────────────────
            var desc = rg.GetTextureDesc(resourceData.activeColorTexture);
            desc.name        = "_CelShadeTmp";
            desc.clearBuffer = false;
            var temp = rg.CreateTexture(desc);

            using (var builder = rg.AddRasterRenderPass<PassData>("CelShadeFilter", out var data))
            {
                data.src = resourceData.activeColorTexture;
                data.mat = _mat;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(temp, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), d.mat, 0));
            }

            // ── 2) Copy the cel-shaded result back to the camera color ───────────
            using (var builder = rg.AddRasterRenderPass<PassData>("CelShadeFilter CopyBack", out var data))
            {
                data.src = temp;

                builder.UseTexture(data.src);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.SetRenderFunc((PassData d, RasterGraphContext c) =>
                    Blitter.BlitTexture(c.cmd, d.src, new Vector4(1, 1, 0, 0), 0, false));
            }

            // ── 3) Re-draw the excluded layers (UI + sprites) cleanly on top, so
            //       the outline that was painted over them is covered. They keep
            //       their own materials/colors; nothing of the cel filter remains. ─
            if (_excludeLayers != 0)
            {
                var renderingData = ctx.Get<UniversalRenderingData>();
                var cameraData    = ctx.Get<UniversalCameraData>();
                var lightData     = ctx.Get<UniversalLightData>();

                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    s_Tags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var filter = new FilteringSettings(RenderQueueRange.transparent, _excludeLayers);

                var listParams = new RendererListParams(renderingData.cullResults, drawSettings, filter);
                RendererListHandle list = rg.CreateRendererList(listParams);

                using (var builder = rg.AddRasterRenderPass<RedrawData>("CelShade Exclude Redraw", out var data))
                {
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((RedrawData d, RasterGraphContext c) =>
                        c.cmd.DrawRendererList(d.list));
                }
            }
        }
    }
}