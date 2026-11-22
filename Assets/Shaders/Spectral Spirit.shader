// The evil spirit, only visible through the viewfinder: a glowing outline that shows through walls, and its full image where nothing is in the way.
Shader "Look Again/Spectral Spirit"
{
    Properties
    {
        _Alpha ("Alpha", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _BodyColor ("Body Color", Color) = (0.55, 0.75, 1, 1)
        _RimColor ("Outline Color", Color) = (0.3, 0.6, 1, 1)
        _RimPower ("Outline Sharpness", Range(0.5, 8)) = 2.5
        _Silhouette ("Outline Strength", Range(0, 2)) = 1
        _Image ("Image Strength", Range(0, 2)) = 1
        _Flicker ("Flicker", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _Alpha_ST;
        float4 _NormalMap_ST;
        half4 _BodyColor;
        half4 _RimColor;
        half _RimPower;
        half _Silhouette;
        half _Image;
        half _Flicker;
        CBUFFER_END

        float _SpectralReveal;
        TEXTURE2D(_Alpha);
        SAMPLER(sampler_Alpha);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
            output.uv = input.uv;
            return output;
        }

        // How squarely the surface faces the eye, from its folds in the normal map
        half Facing(Varyings input)
        {
            float3 normal = normalize(input.normalWS);
            float3 tangent = normalize(input.tangentWS.xyz);
            float3 bitangent = cross(normal, tangent) * input.tangentWS.w;
            half3 bumped = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, TRANSFORM_TEX(input.uv, _NormalMap)));
            normal = normalize(bumped.x * tangent + bumped.y * bitangent + bumped.z * normal);
            return saturate(dot(normal, normalize(GetWorldSpaceViewDir(input.positionWS))));
        }

        half Mask(Varyings input)
        {
            return SAMPLE_TEXTURE2D(_Alpha, sampler_Alpha, TRANSFORM_TEX(input.uv, _Alpha)).a;
        }

        half Flicker()
        {
            return 1 - _Flicker * (0.5 + 0.5 * sin(_Time.y * 7.3) * sin(_Time.y * 3.1));
        }
        ENDHLSL

        // The outline, drawn over everything so it shows through walls
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Back
            Stencil
            {
                Ref 1
                Comp [_SpectralStencilComp]
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            half4 Fragment(Varyings input) : SV_Target
            {
                half rim = pow(1 - Facing(input), _RimPower);
                return half4(_RimColor.rgb * rim * Mask(input) * _Silhouette * Flicker() * _SpectralReveal, 0);
            }
            ENDHLSL
        }

        // The full image, hidden behind anything in the way
        Pass
        {
            Name "Image"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Stencil
            {
                Ref 1
                Comp [_SpectralStencilComp]
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            half4 Fragment(Varyings input) : SV_Target
            {
                half facing = Facing(input);
                half3 color = _BodyColor.rgb * (0.25 + 0.55 * facing * facing) + _RimColor.rgb * pow(1 - facing, _RimPower) * 0.6;
                return half4(color * Mask(input) * _Image * Flicker() * _SpectralReveal, 0);
            }
            ENDHLSL
        }
    }
}
