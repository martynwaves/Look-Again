// A cold blue creeping in from the edges of the view as the evil spirit nears, its edge rippling and shaking harder the closer it is.
// Drawn in each eye's own screen space, so both eyes match.
Shader "Look Again/Spirit Dread"
{
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0
        _Color ("Color", Color) = (0.05, 0.2, 0.55, 0.85)
        _EdgeColor ("Edge Glow", Color) = (0.35, 0.65, 1, 1)
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.35
        _Shake ("Shake", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half _Strength;
            half4 _Color;
            half4 _EdgeColor;
            half _Softness;
            half _Shake;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 screen : TEXCOORD0; // -1 to 1 across the eye's view
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }

            float Noise(float p)
            {
                float i = floor(p);
                float f = frac(p);
                return lerp(Hash11(i), Hash11(i + 1), f * f * (3 - 2 * f));
            }

            // Noise that repeats every period, so a pattern laid around a circle meets itself without a seam
            float LoopNoise(float p, float period)
            {
                float i = floor(p);
                float f = frac(p);
                return lerp(Hash11(fmod(i, period)), Hash11(fmod(i + 1, period)), f * f * (3 - 2 * f));
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.screen = input.uv * 2 - 1;
                output.positionCS = float4(output.screen, UNITY_NEAR_CLIP_VALUE, 1);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 p = input.screen * float2(1, 1.15);
                float radius = length(p);
                // Around the ring, wrapped so the ripples join up
                float around = (atan2(p.y, p.x) / TWO_PI + 0.5) * 24;
                float t = _Time.y;
                float ripple = (LoopNoise(around * 0.5 + t * 1.7, 12) - 0.5) * 0.3 + (LoopNoise(around + 24 - frac(t * 0.17) * 24, 24) - 0.5) * 0.14;
                // The whole edge twitches back and forth, only building up properly close in
                float close = _Strength * _Strength;
                float shake = (Noise(t * lerp(4, 20, close)) - 0.5) * 0.2 * _Shake;
                float edge = lerp(1.75, 0.6, _Strength) + ripple * _Strength + shake * close;
                half cover = smoothstep(edge - _Softness, edge + _Softness * 0.6, radius);
                half glow = exp(-pow((radius - edge) * 9, 2));
                half3 color = lerp(_Color.rgb, _EdgeColor.rgb, glow * 0.6);
                return half4(color, saturate(cover * _Color.a + glow * 0.35) * saturate(_Strength * 1.6));
            }
            ENDHLSL
        }
    }
}
