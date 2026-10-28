// See-through ghostly hands: dark and faint face-on, glowing at the edges.
// A depth pass first keeps the hand's own overlapping fingers from showing through each other.
Shader "Look Again/Ghost Hand"
{
    Properties
    {
        _Inner ("Inner Colour", Color) = (0, 0.058, 0.18, 0.25)
        _Rim ("Rim Colour", Color) = (0.118, 0.686, 1, 0.9)
        _Power ("Rim Sharpness", Range(0.5, 8)) = 2.5
        [HDR] _Glow ("Rim Glow", Color) = (0.1, 0.5, 0.9, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Inner;
            half4 _Rim;
            half _Power;
            half4 _Glow;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Depth"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            half4 Frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "Ghost"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim = pow(1 - saturate(abs(dot(normalize(input.normalWS), view))), _Power);
                half4 colour = lerp(_Inner, _Rim, rim);
                colour.rgb += _Glow.rgb * rim;
                return colour;
            }
            ENDHLSL
        }
    }
}
