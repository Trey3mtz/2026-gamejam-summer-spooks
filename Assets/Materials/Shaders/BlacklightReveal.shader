// Surfaces using this shader are invisible until they fall inside the blacklight's
// cone. Alpha = distance falloff * angular falloff * global on/off intensity.
// The globals (_Blacklight*) are pushed by BlacklightRevealDriver each frame.
//
// NOTE (Phase 3): when the PS1 surface shader lands, port its vertex snapping /
// affine UV tricks into the vert stage here so revealed geometry matches the
// rest of the world. The reveal math below is entirely fragment-side and won't
// conflict with it.
Shader "SpookyGame/BlacklightReveal"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _GlowColor ("UV Glow Tint", Color) = (0.55, 0.35, 1.0, 1)
        [Tooltip(Additive fluorescent pop at full reveal. 0 disables.)]
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "BlacklightReveal"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _GlowColor;
                half   _GlowStrength;
            CBUFFER_END

            // --- Globals written by BlacklightRevealDriver ---
            float3 _BlacklightPos;        // world-space light position
            float3 _BlacklightDir;        // normalized world-space forward
            float  _BlacklightRange;      // meters
            float  _BlacklightCosOuter;   // cos(outer half-angle)
            float  _BlacklightCosInner;   // cos(inner half-angle)
            float  _BlacklightIntensity;  // 0 = off, 1 = on

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 toFrag = IN.positionWS - _BlacklightPos;
                float  dist   = length(toFrag);
                float3 dirToFrag = toFrag / max(dist, 1e-4);

                // Quadratic distance falloff, clamped at range.
                float distAtten = saturate(1.0 - dist / max(_BlacklightRange, 1e-3));
                distAtten *= distAtten;

                // Angular falloff: full inside the inner cone, smooth fade to the outer edge.
                float cosTheta  = dot(dirToFrag, _BlacklightDir);
                float coneAtten = smoothstep(_BlacklightCosOuter, _BlacklightCosInner, cosTheta);

                float reveal = distAtten * coneAtten * _BlacklightIntensity;

                // Fully hidden fragments contribute nothing; skip the texture fetch.
                clip(reveal - 0.001);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // Fluorescent pop: additive glow that scales with reveal strength.
                half3 color = tex.rgb + _GlowColor.rgb * (_GlowStrength * reveal);

                return half4(color, tex.a * reveal);
            }
            ENDHLSL
        }
    }
}
