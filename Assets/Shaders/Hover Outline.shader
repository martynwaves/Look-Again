// An unlit outline around an item: one step marks the item in the stencil, the next draws it nudged each way in view where it isn't marked, and the last clears the mark.
// Its width is an angle, so it looks the same near or far; lighting doesn't touch it, so it shows in the dark.
Shader "Look Again/Hover Outline"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.7, 1, 1)
        _Shift ("Nudge (x, y)", Vector) = (0, 0, 0, 0)
        _Nearer ("Drawn Nearer (fraction of distance)", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
        _ColorMask ("Color Mask", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comparison", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil Pass", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+11" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest [_ZTest]
            ColorMask [_ColorMask]
            // Bit 64 only, clear of the viewfinder's mask
            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Shift;
                float _Nearer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                // Nearer along the line of sight, so the surface an item rests on doesn't hide the outline
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz)) * (1 - _Nearer);
                output.positionCS = TransformWViewToHClip(positionVS);
                output.positionCS.xy += _Shift.xy * float2(UNITY_MATRIX_P._m00, UNITY_MATRIX_P._m11) * output.positionCS.w;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
