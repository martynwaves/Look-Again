// Marks the viewfinder window in the stencil buffer, so Spectral objects only show there.
Shader "Look Again/TopDownNotebook Mask"
{
    Properties
    {
        _Margin ("Margin", Float) = 0.12
        _Radius ("Corner Radius", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" "RenderType" = "Opaque" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex ViewfinderVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Margin;
            float _Radius;
            CBUFFER_END

            #include "Viewfinder.hlsl"

            half4 Fragment(ViewfinderVaryings input) : SV_Target
            {
                clip(-ViewfinderDistance(input.position, input.aspect));
                return 0;
            }
            ENDHLSL
        }
    }
}
