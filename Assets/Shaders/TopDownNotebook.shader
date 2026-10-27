Shader "Look Again/TopDownNotebook"
{
    Properties
    {
        [Header(Layout)]
        _Aspect ("Aspect Ratio (Width/Height)", Float) = 1.4
        _BookScale ("Book Scale", Float) = 0.85

        [Header(Pages)]
        _PaperColor ("Paper Color", Color) = (0.92, 0.88, 0.78, 1)
        _AgedStains ("Aged Stains Intensity", Range(0, 1)) = 0.35
        _FoldShadow ("Center Fold Shadow", Range(0, 1)) = 0.65
        _EdgeWear ("Page Edge Wear", Range(0, 1)) = 0.4

        [Header(Cover and Details)]
        _LeatherColor ("Leather Cover Color", Color) = (0.18, 0.11, 0.07, 1)
        _BorderWidth ("Cover Border Width", Range(0, 0.2)) = 0.09
        _CornerMetalColor ("Corner Bracket Color", Color) = (0.55, 0.48, 0.35, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _Aspect;
        float _BookScale;
        half4 _PaperColor;
        half _AgedStains;
        half _FoldShadow;
        half _EdgeWear;
        half4 _LeatherColor;
        float _BorderWidth;
        half4 _CornerMetalColor;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS   : POSITION;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionCS   : SV_POSITION;
            float2 uv           : TEXCOORD0;
            UNITY_VERTEX_OUTPUT_STEREO
        };

        Varyings Vertex(Attributes input)
        {
            Varyings output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        float Hash21Notebook(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoiseNotebook(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash21Notebook(i), Hash21Notebook(i + float2(1, 0)), f.x),
                        lerp(Hash21Notebook(i + float2(0, 1)), Hash21Notebook(i + float2(1, 1)), f.x), f.y);
        }
        ENDHLSL

        Pass
        {
            Name "TopDownNotebookPass"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            half4 Fragment(Varyings input) : SV_Target
            {
                // Center UV coordinates from -1 to 1
                float2 uv = (input.uv - 0.5) * 2.0;
                uv.x *= _Aspect;
                uv /= _BookScale;

                // Book Leather Cover Outer Bounds
                float2 bookSize = float2(1.35, 0.92);
                float dBook = max(abs(uv.x) - bookSize.x, abs(uv.y) - bookSize.y);
                
                if (dBook > 0.02)
                    return half4(0, 0, 0, 0); // Transparent outside

                // Base Leather Cover with pebbled noise texture
                float leatherNoise = ValueNoiseNotebook(uv * 60.0) * 0.12;
                half3 col = _LeatherColor.rgb + leatherNoise;

                // Inner Pages Area (Parchment)
                float2 pageSize = bookSize - float2(_BorderWidth, _BorderWidth);
                float dPage = max(abs(uv.x) - pageSize.x, abs(uv.y) - pageSize.y);

                if (dPage <= 0.0)
                {
                    // Procedural parchment texture & stains
                    float fineGrain = ValueNoiseNotebook(uv * 140.0);
                    float stains = ValueNoiseNotebook(uv * 12.0) * _AgedStains;
                    half3 paper = _PaperColor.rgb * (1.0 - stains - fineGrain * 0.07);

                    // Center spine fold shadow dividing left and right pages
                    float distToCenter = abs(uv.x);
                    float fold = smoothstep(0.0, 0.06, distToCenter);
                    paper *= lerp(1.0 - _FoldShadow, 1.0, fold);

                    // Darkening and wear near the paper borders
                    float edgeDist = min(pageSize.x - abs(uv.x), pageSize.y - abs(uv.y));
                    float wear = smoothstep(0.0, 0.06, edgeDist);
                    paper *= lerp(1.0 - _EdgeWear, 1.0, wear);

                    col = paper;
                }
                else
                {
                    // Add decorative metal corner brackets at the outer cover corners
                    float cornerX = abs(abs(uv.x) - bookSize.x * 0.92);
                    float cornerY = abs(abs(uv.y) - bookSize.y * 0.92);
                    if (cornerX < 0.08 && cornerY < 0.08)
                    {
                        half metalShen = ValueNoiseNotebook(uv * 200.0) * 0.2;
                        col = _CornerMetalColor.rgb + metalShen;
                    }
                }

                // Smooth edge anti-aliasing alpha blending
                float alpha = 1.0 - smoothstep(-0.005, 0.005, dBook);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}