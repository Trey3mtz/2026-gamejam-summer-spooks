// Phase 4 of the retro pipeline: color crush + ordered dithering.
// Lives on the presentation RawImage, sampling the point-filtered internal
// target. All pattern math is computed in INTERNAL pixel space
// (floor(uv * _InternalRes)), so every native-res screen pixel inside one
// internal pixel gets an identical result — visually indistinguishable from
// running the pass at 320x240 before upscale, without a second RT or a
// renderer-feature ordering problem across the camera stack.
//
// Phase 5 extends this same shader: barrel distortion remaps i.uv at the top
// of frag, and scanlines/mask/vignette apply after the quantization below.
//
// Canvas rendering is not SRP-managed, so legacy CG syntax is correct here.
Shader "SpookyGame/UI/RetroComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Internal Target", 2D) = "white" {}

        [Header(Color Crush)]
        [Tooltip(5 bits per channel is authentic PS1 15 bit output.)]
        _ColorBits ("Bits Per Channel", Range(2, 8)) = 5

        [Header(Dither)]
        _DitherStrength ("Dither Strength", Range(0, 1)) = 1

        // Driven from RetroScreenController; keep in sync with the RT size.
        _InternalRes ("Internal Resolution", Vector) = (320, 240, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _MainTex;
            float  _ColorBits;
            float  _DitherStrength;
            float4 _InternalRes;

            // Classic 4x4 Bayer matrix, row-major. Values 0..15.
            static const float BAYER_4x4[16] =
            {
                 0,  8,  2, 10,
                12,  4, 14,  6,
                 3, 11,  1,  9,
                15,  7, 13,  5
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // Internal-pixel coordinates: constant across the whole block
                // of screen pixels that one 320x240 texel upscales into.
                float2 internalPixel = floor(i.uv * _InternalRes.xy);
                int idx = (int)fmod(internalPixel.x, 4.0)
                        + (int)fmod(internalPixel.y, 4.0) * 4;

                // Centered threshold in [-0.5, 0.5).
                float threshold = (BAYER_4x4[idx] + 0.5) / 16.0 - 0.5;

                // The PS1 quantized its gamma-encoded framebuffer, so in a
                // Linear-color-space project we hop to gamma, crush, hop back.
                // Skipping this quantizes linear values and visibly shifts
                // the banding toward the shadows.
                #ifndef UNITY_COLORSPACE_GAMMA
                col.rgb = LinearToGammaSpace(col.rgb);
                #endif

                float levels = exp2(floor(_ColorBits)) - 1.0;
                col.rgb = saturate(
                    floor(col.rgb * levels + 0.5 + threshold * _DitherStrength) / levels);

                #ifndef UNITY_COLORSPACE_GAMMA
                col.rgb = GammaToLinearSpace(col.rgb);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
