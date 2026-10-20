// A ghostly shell drawn over an object, brightest at its edges, with a slow shimmer rising through it. Only visible through the viewfinder.
Shader "Look Again/Spectral Aura"
{
    Properties
    {
        _Color ("Color", Color) = (0.9, 0.95, 1, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Fill ("Fill", Range(0, 1)) = 0.25
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2
        _ShimmerScale ("Shimmer Scale", Float) = 9
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.4
        _Pulse ("Pulse", Range(0, 1)) = 0.2
        _PulseSpeed ("Pulses per Second", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Stencil
            {
                Ref 1
                Comp [_SpectralStencilComp]
            }
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Brightness;
            half _Fill;
            half _RimPower;
            float _ShimmerScale;
            float _ShimmerSpeed;
            half _Pulse;
            float _PulseSpeed;
            CBUFFER_END

            float _SpectralReveal;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(
                    lerp(lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x), lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x), lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 toEye = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1 - saturate(dot(normal, toEye)), _RimPower);

                // Wisps drifting upward through the shell
                float3 p = input.positionWS * _ShimmerScale - float3(0, _Time.y * _ShimmerSpeed * _ShimmerScale * 0.1, 0);
                half shimmer = ValueNoise(p) * 0.6 + ValueNoise(p * 2.1 + 7.3) * 0.4;

                half pulse = 1 + _Pulse * sin(_Time.y * _PulseSpeed * TWO_PI);
                half glow = (_Fill + rim) * (0.45 + 1.1 * shimmer);
                return half4(_Color.rgb * glow * _Brightness * pulse * _SpectralReveal, 0);
            }
            ENDHLSL
        }
    }
}
