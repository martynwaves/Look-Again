// The ghost orb's glowing, swirling body. Only visible through the viewfinder.
Shader "Look Again/Spectral Orb"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (1, 0.97, 0.9, 1)
        _RimColor ("Rim Color", Color) = (0.35, 0.7, 1, 1)
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2.5
        _Brightness ("Brightness", Range(0, 4)) = 1.3
        _SwirlScale ("Swirl Scale", Float) = 5
        _SwirlSpeed ("Swirl Speed", Float) = 0.35
        _Pulse ("Pulse", Range(0, 1)) = 0.15
        _PulseSpeed ("Pulses per Second", Float) = 0.6
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
            half4 _CoreColor;
            half4 _RimColor;
            half _RimPower;
            half _Brightness;
            float _SwirlScale;
            float _SwirlSpeed;
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
                float3 positionOS : TEXCOORD2;
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
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 toEye = normalize(GetWorldSpaceViewDir(input.positionWS));
                half facing = saturate(dot(normal, toEye));

                float t = _Time.y * _SwirlSpeed;
                float3 p = input.positionOS * _SwirlScale;
                half swirl = ValueNoise(p + float3(0, t, t * 0.5)) * 0.65 + ValueNoise(p * 2.3 - float3(t, 0, t * 0.7)) * 0.35;

                half pulse = 1 + _Pulse * sin(_Time.y * _PulseSpeed * TWO_PI);
                half3 color = _CoreColor.rgb * pow(facing, 3) * (0.6 + 0.8 * swirl)
                            + _RimColor.rgb * pow(1 - facing, _RimPower) * (0.7 + 0.6 * swirl);
                return half4(color * _Brightness * pulse * _SpectralReveal, 0);
            }
            ENDHLSL
        }
    }
}
