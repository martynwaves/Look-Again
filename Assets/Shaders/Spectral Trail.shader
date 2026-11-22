// The ghostly trail from the evil spirit back to its source, drawn on a line in drifting broken wisps. Only visible through the viewfinder; faint through walls, clearer in the open.
Shader "Look Again/Spectral Trail"
{
    Properties
    {
        _Color ("Color", Color) = (0.3, 0.6, 1, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1.2
        _Through ("Through Walls", Range(0, 1)) = 0.3
        _FlowScale ("Wisps per Meter", Float) = 1.5
        _FlowSpeed ("Flow Speed", Float) = 0.8
        _Gaps ("Gaps", Range(0, 1)) = 0.55
        _PieceScale ("Pieces per Meter", Float) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        half4 _Color;
        half _Brightness;
        half _Through;
        float _FlowScale;
        float _FlowSpeed;
        half _Gaps;
        float _PieceScale;
        CBUFFER_END

        float _SpectralReveal;

        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
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

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.color = input.color;
            return output;
        }

        // A soft band across the line, with wisps flowing along it back toward the source; the line's texture mode is Tile, so u counts meters
        half3 Trail(Varyings input)
        {
            half band = pow(saturate(1 - abs(input.uv.y * 2 - 1)), 2);
            float u = input.uv.x * _FlowScale + _Time.y * _FlowSpeed;
            half wisps = 0.45 + 0.55 * Noise(u) * Noise(u * 2.7 + 13.1);
            // Broken into pieces with gaps between, drifting slowly along
            half pieces = smoothstep(_Gaps - 0.12, _Gaps + 0.12, Noise(input.uv.x * _PieceScale + _Time.y * 0.15 + 7.3));
            return _Color.rgb * band * wisps * pieces * _Brightness * input.color.rgb * input.color.a * _SpectralReveal;
        }
        ENDHLSL

        // Faintly, over everything
        Pass
        {
            Name "Through Walls"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
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
                return half4(Trail(input) * _Through, 0);
            }
            ENDHLSL
        }

        // Clearly, where nothing is in the way
        Pass
        {
            Name "In The Open"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
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
                return half4(Trail(input) * (1 - _Through), 0);
            }
            ENDHLSL
        }
    }
}
