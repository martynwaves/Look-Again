// Two dark halves closing across the view from above and below, like eyelids.
// Drawn in each eye's own screen space, so the seam lines up in both eyes.
Shader "Look Again/Eye Shutter"
{
    Properties
    {
        _Shut ("Shut", Range(0, 1)) = 0
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Bow ("Bow", Range(0, 1)) = 0.35
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay+500" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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
            half _Shut;
            half4 _Color;
            half _Bow;
            half _Softness;
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
                // Starts past the corners and ends past the middle, so the edge's softness is gone at both ends
                float gap = lerp(1.6, -_Softness, _Shut);
                float lid = abs(input.screen.y) - gap * (1 - _Bow * input.screen.x * input.screen.x);
                return half4(_Color.rgb, _Color.a * smoothstep(0, _Softness, lid));
            }
            ENDHLSL
        }
    }
}
