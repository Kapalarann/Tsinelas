// ShinkaiFilterFeature.cs
//
// A global post-process renderer feature that gives the whole frame a
// pastel, Makoto Shinkai-esque grade: soft bloom on bright areas, warm/cool
// split-toning, lifted pastel shadows with soft-clipped highlights, a gentle
// vignette, and a subtle chromatic aberration at the edges.
//
// SETUP:
// 1. Create a Material using the shader "Custom/URP/ShinkaiFilter"
//    (from ShinkaiFilter.shader).
// 2. On your URP Renderer Data asset, click "Add Renderer Feature" ->
//    "Shinkai Filter Feature".
// 3. Assign the material you made in step 1 to the "Material" slot.
// 4. Tweak the exposed settings to taste. All of them are also exposed as
//    plain public fields so you can drive them at runtime (e.g. lerp
//    intensity down for a "night" scene, or shift split-tone balance
//    per era/biome).
//
// NOTE: Render Graph API surface has shifted slightly between URP 16/17
// patch releases. This targets the Unity 6 / URP 17 API as documented at
// time of writing (RenderGraph, ContextContainer, UniversalResourceData,
// AddRasterRenderPass, RasterGraphContext). If you hit a compile error on
// a specific call (most likely around TextureDesc fields or the
// Blitter.BlitTexture overload), it's almost certainly just a minor
// signature difference for your exact package version — happy to patch it
// once you paste the error.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class ShinkaiFilterFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Core")]
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Material material;
        [Range(0f, 1f)] public float intensity = 1f;

        [Header("Bloom / Glow")]
        [Tooltip("Luminance above which pixels start contributing to the soft glow.")]
        [Range(0f, 1f)] public float bloomThreshold = 0.7f;
        [Tooltip("How strongly the glow is blended back into the image.")]
        [Range(0f, 3f)] public float bloomIntensity = 0.6f;
        [Tooltip("Tint applied to the glow itself (warm/pinkish reads as more 'anime sky').")]
        public Color bloomTint = new Color(1f, 0.9f, 0.85f);
        [Tooltip("Resolution divisor for the glow blur pass. Higher = softer & cheaper.")]
        [Range(1, 4)] public int downsample = 2;

        [Header("Color Grade")]
        [Tooltip("Tint multiplied into shadow tones.")]
        public Color shadowTint = new Color(0.55f, 0.55f, 0.85f);
        [Tooltip("Tint multiplied into highlight tones.")]
        public Color highlightTint = new Color(1f, 0.85f, 0.75f);
        [Tooltip("Shifts where 'shadow' ends and 'highlight' begins.")]
        [Range(-1f, 1f)] public float splitToneBalance = 0f;
        [Range(0f, 2f)] public float saturation = 1.15f;
        [Tooltip("Raises the black point for a soft, washed-out pastel feel.")]
        [Range(0f, 1f)] public float shadowLift = 0.08f;
        [Tooltip("Soft-clips bright highlights instead of hard-clamping them.")]
        [Range(0f, 1f)] public float highlightSoftClip = 0.25f;

        [Header("Vignette / Aberration")]
        [Range(0f, 1f)] public float vignetteIntensity = 0.35f;
        [Range(0.01f, 1f)] public float vignetteSmoothness = 0.6f;
        [Tooltip("Subtle RGB channel offset at the screen edges for a dreamy lens feel.")]
        [Range(0f, 0.02f)] public float chromaticAberration = 0.0025f;
    }

    public Settings settings = new Settings();
    private ShinkaiFilterPass pass;

    public override void Create()
    {
        pass = new ShinkaiFilterPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null)
        {
            Debug.LogWarning("ShinkaiFilterFeature: no material assigned, skipping pass.");
            return;
        }
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass = null;
    }
}

class ShinkaiFilterPass : ScriptableRenderPass
{
    private readonly ShinkaiFilterFeature.Settings settings;

    private static readonly int BloomThresholdId = Shader.PropertyToID("_BloomThreshold");
    private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
    private static readonly int BloomTintId = Shader.PropertyToID("_BloomTint");
    private static readonly int ShadowTintId = Shader.PropertyToID("_ShadowTint");
    private static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
    private static readonly int SplitToneBalanceId = Shader.PropertyToID("_SplitToneBalance");
    private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
    private static readonly int LiftId = Shader.PropertyToID("_Lift");
    private static readonly int SoftClipId = Shader.PropertyToID("_SoftClip");
    private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
    private static readonly int VignetteSmoothnessId = Shader.PropertyToID("_VignetteSmoothness");
    private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int BlurTexelSizeId = Shader.PropertyToID("_BlurTexelSize");
    private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");

    private const string PassName = "Shinkai Filter";

    public ShinkaiFilterPass(ShinkaiFilterFeature.Settings settings)
    {
        this.settings = settings;
        renderPassEvent = settings.renderPassEvent;
    }

    private class PassData
    {
        public Material material;
        public TextureHandle source;
        public TextureHandle bloom;
        public int passIndex;
    }

    private void UpdateStaticMaterialProperties(Material material)
    {
        material.SetFloat(BloomThresholdId, settings.bloomThreshold);
        material.SetFloat(BloomIntensityId, settings.bloomIntensity);
        material.SetColor(BloomTintId, settings.bloomTint);
        material.SetColor(ShadowTintId, settings.shadowTint);
        material.SetColor(HighlightTintId, settings.highlightTint);
        material.SetFloat(SplitToneBalanceId, settings.splitToneBalance);
        material.SetFloat(SaturationId, settings.saturation);
        material.SetFloat(LiftId, settings.shadowLift);
        material.SetFloat(SoftClipId, settings.highlightSoftClip);
        material.SetFloat(VignetteIntensityId, settings.vignetteIntensity);
        material.SetFloat(VignetteSmoothnessId, settings.vignetteSmoothness);
        material.SetFloat(ChromaticAberrationId, settings.chromaticAberration);
        material.SetFloat(IntensityId, settings.intensity);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        var material = settings.material;
        if (material == null) return;

        UpdateStaticMaterialProperties(material);

        var resourceData = frameData.Get<UniversalResourceData>();
        var cameraData = frameData.Get<UniversalCameraData>();

        // Skip if the active target is already the back buffer (nothing left to grade).
        if (resourceData.isActiveTargetBackBuffer) return;

        TextureHandle source = resourceData.activeColorTexture;

        int downsample = Mathf.Max(1, settings.downsample);
        int width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width / downsample);
        int height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height / downsample);

        material.SetVector(BlurTexelSizeId, new Vector4(1f / width, 1f / height, width, height));

        var bloomDesc = new TextureDesc(width, height)
        {
            colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
            name = "_ShinkaiBloomA",
            clearBuffer = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            msaaSamples = MSAASamples.None
        };
        TextureHandle bloomA = renderGraph.CreateTexture(bloomDesc);

        bloomDesc.name = "_ShinkaiBloomB";
        TextureHandle bloomB = renderGraph.CreateTexture(bloomDesc);

        var destDesc = renderGraph.GetTextureDesc(source);
        destDesc.name = "_ShinkaiFilterOutput";
        destDesc.clearBuffer = false;
        TextureHandle destination = renderGraph.CreateTexture(destDesc);

        // Pass 0: bright-pass extraction, downsampled.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{PassName} - Bright Pass", out var passData))
        {
            passData.material = material;
            passData.source = source;
            passData.passIndex = 0;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(bloomA, 0, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
            });
        }

        // Pass 1: horizontal blur.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{PassName} - Blur H", out var passData))
        {
            passData.material = material;
            passData.source = bloomA;
            passData.passIndex = 1;
            builder.UseTexture(bloomA, AccessFlags.Read);
            builder.SetRenderAttachment(bloomB, 0, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
            });
        }

        // Pass 2: vertical blur, written back into bloomA.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{PassName} - Blur V", out var passData))
        {
            passData.material = material;
            passData.source = bloomB;
            passData.passIndex = 2;
            builder.UseTexture(bloomB, AccessFlags.Read);
            builder.SetRenderAttachment(bloomA, 0, AccessFlags.Write);
            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
            });
        }

        // Pass 3: composite - color grade + bloom + vignette + chromatic aberration.
        using (var builder = renderGraph.AddRasterRenderPass<PassData>($"{PassName} - Composite", out var passData))
        {
            passData.material = material;
            passData.source = source;
            passData.bloom = bloomA;
            passData.passIndex = 3;
            builder.UseTexture(source, AccessFlags.Read);
            builder.UseTexture(bloomA, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.AllowGlobalStateModification(true); // required: this pass calls SetGlobalTexture below
            builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
            {
                ctx.cmd.SetGlobalTexture(BloomTexId, data.bloom);
                Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
            });
        }

        resourceData.cameraColor = destination;
    }
}