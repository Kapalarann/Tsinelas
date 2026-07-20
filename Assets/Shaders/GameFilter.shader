// ShinkaiFilter.shader
//
// Four-pass full-screen filter used by ShinkaiFilterFeature.cs:
//   Pass 0: Bright-pass extraction (downsampled)
//   Pass 1: Horizontal blur
//   Pass 2: Vertical blur
//   Pass 3: Composite - split-tone grade, pastel lift/soft-clip, saturation,
//           bloom blend, vignette, chromatic aberration
//
// Written against Core RP Library only (Core.hlsl) rather than URP's
// Blit.hlsl helper, to avoid depending on exact Blit include contents
// across URP versions. The texture named "_BlitTexture" is what
// Blitter.BlitTexture() writes the source into - keep that name as-is.

Shader "Custom/URP/ShinkaiFilter"
{
    Properties
    {
        _BloomThreshold ("Bloom Threshold", Float) = 0.7
        _BloomIntensity ("Bloom Intensity", Float) = 0.6
        _BloomTint ("Bloom Tint", Color) = (1, 0.9, 0.85, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.55, 0.85, 1)
        _HighlightTint ("Highlight Tint", Color) = (1.0, 0.85, 0.75, 1)
        _SplitToneBalance ("Split Tone Balance", Range(-1, 1)) = 0
        _Saturation ("Saturation", Float) = 1.15
        _Lift ("Shadow Lift", Range(0, 1)) = 0.08
        _SoftClip ("Highlight Soft Clip", Range(0, 1)) = 0.25
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.35
        _VignetteSmoothness ("Vignette Smoothness", Range(0.01, 1)) = 0.6
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.02)) = 0.0025
        _Intensity ("Overall Effect Intensity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BlitTexture);
        TEXTURE2D(_BloomTex);
        // sampler_LinearClamp is one of the built-in global samplers already
        // declared and bound by the Core RP Library - do NOT redeclare it
        // here, it will conflict and break compilation for every pass in
        // this file (since they all share this HLSLINCLUDE block).

        float4 _BlurTexelSize; // xy = 1/size, zw = size
        float _BloomThreshold;
        float _BloomIntensity;
        float4 _BloomTint;
        float4 _ShadowTint;
        float4 _HighlightTint;
        float _SplitToneBalance;
        float _Saturation;
        float _Lift;
        float _SoftClip;
        float _VignetteIntensity;
        float _VignetteSmoothness;
        float _ChromaticAberration;
        float _Intensity;

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            o.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return o;
        }

        half3 AdjustSaturation(half3 color, half saturation)
        {
            half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
            return lerp(luma.xxx, color, saturation);
        }

        // Multiplies shadows and highlights by different tints, blended by luma.
        half3 SplitTone(half3 color, half3 shadowColor, half3 highlightColor, half balance)
        {
            half luma = dot(color, half3(0.299, 0.587, 0.114));
            half t = smoothstep(0.0, 1.0, saturate(luma + balance));
            return color * lerp(shadowColor, highlightColor, t);
        }

        // Raises the black point (pastel wash) and soft-clips highlights
        // instead of hard-clamping, for that gentle, airy Shinkai look.
        half3 PastelGrade(half3 c, half lift, half softClip)
        {
            c = c + lift * (1.0 - c);
            c = c / (1.0 + softClip * c);
            return c;
        }

        half Vignette(float2 uv, half intensity, half smoothness)
        {
            float2 center = uv - 0.5;
            half dist = length(center) * 1.414;
            return 1.0 - smoothstep(1.0 - smoothness, 1.0, dist) * intensity;
        }
        ENDHLSL

        // ---------------------------------------------------------------
        // Pass 0: Bright-pass extraction
        // ---------------------------------------------------------------
        Pass
        {
            Name "BrightPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv).rgb;
                half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
                half contribution = saturate((luma - _BloomThreshold) / max(0.0001, 1.0 - _BloomThreshold));

                return half4(color * contribution, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 1: Horizontal blur (9-tap, weighted)
        // ---------------------------------------------------------------
        Pass
        {
            Name "BlurHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            static const float kWeights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texel = _BlurTexelSize.xy;
                half3 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv).rgb * kWeights[0];
                [unroll]
                for (int i = 1; i < 5; i++)
                {
                    float2 offset = float2(texel.x * i * 2.0, 0.0);
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv + offset).rgb * kWeights[i];
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv - offset).rgb * kWeights[i];
                }
                return half4(sum, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 2: Vertical blur (9-tap, weighted)
        // ---------------------------------------------------------------
        Pass
        {
            Name "BlurVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            static const float kWeights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texel = _BlurTexelSize.xy;
                half3 sum = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv).rgb * kWeights[0];
                [unroll]
                for (int i = 1; i < 5; i++)
                {
                    float2 offset = float2(0.0, texel.y * i * 2.0);
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv + offset).rgb * kWeights[i];
                    sum += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.uv - offset).rgb * kWeights[i];
                }
                return half4(sum, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------
        // Pass 3: Composite - grade + bloom + vignette + chromatic aberration
        // ---------------------------------------------------------------
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                half3 original = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                half3 sceneColor;
                if (_ChromaticAberration > 0.0001)
                {
                    float2 dir = uv - 0.5;
                    float2 offset = dir * _ChromaticAberration;
                    half r = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv - offset).r;
                    half g = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).g;
                    half b = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset).b;
                    sceneColor = half3(r, g, b);
                }
                else
                {
                    sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv).rgb;
                }

                half3 bloom = SAMPLE_TEXTURE2D(_BloomTex, sampler_LinearClamp, uv).rgb;
                half3 color = sceneColor + bloom * _BloomIntensity * _BloomTint.rgb;

                color = PastelGrade(color, _Lift, _SoftClip);
                color = SplitTone(color, _ShadowTint.rgb, _HighlightTint.rgb, _SplitToneBalance);
                color = AdjustSaturation(color, _Saturation);

                half vig = Vignette(uv, _VignetteIntensity, _VignetteSmoothness);
                color *= vig;

                color = lerp(sceneColor, color, _Intensity);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
