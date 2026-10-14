// Soft glow for the orb's halo, floor light and motes. Only visible through the viewfinder.
Shader "Look Again/Spectral Glow"
{
    Properties
    {
        _Color ("Color", Color) = (0.25, 0.5, 0.85, 1)
        _Brightness ("Brightness", Range(0, 4)) = 1
        _Falloff ("Falloff", Range(0.5, 8)) = 2.5
        _Rings ("Rings", Range(0, 1)) = 0
        _RingSpeed ("Ring Speed", Float) = 0.25
        _Pulse ("Pulse", Range(0, 1)) = 0.15
        _PulseSpeed ("Pulses per Second", Float) = 0.6
        [Toggle] _FaceEye ("Face the Eye", Float) = 0
        [Toggle] _UseVertexColor ("Use Vertex Color (particles)", Float) = 0
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
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half _Brightness;
            half _Falloff;
            half _Rings;
            float _RingSpeed;
            half _Pulse;
            float _PulseSpeed;
            half _FaceEye;
            half _UseVertexColor;
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

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                if (_FaceEye > 0.5)
                {
                    // Spread the quad out around its center in view space, so it always faces the eye
                    float4x4 m = GetObjectToWorldMatrix();
                    float2 scale = float2(length(m._m00_m10_m20), length(m._m01_m11_m21));
                    float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                    output.positionCS = TransformWViewToHClip(centerVS + float3(input.positionOS.xy * scale, 0));
                }
                else
                {
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                }
                output.uv = input.uv;
                output.color = lerp(half4(1, 1, 1, 1), input.color, _UseVertexColor);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float r = length(input.uv - 0.5) * 2;
                half glow = pow(saturate(1 - r), _Falloff);
                half rings = pow(saturate(sin((r - _Time.y * _RingSpeed) * 18)), 6) * saturate(1 - r) * _Rings;
                half pulse = 1 + _Pulse * sin(_Time.y * _PulseSpeed * TWO_PI);
                half3 color = _Color.rgb * (glow + rings) * _Brightness * pulse * input.color.rgb * input.color.a;
                return half4(color * _SpectralReveal, 0);
            }
            ENDHLSL
        }
    }
}
