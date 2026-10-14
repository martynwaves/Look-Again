// The viewfinder's lens (tint, vignette, grain, scratches, corner brackets) and its camera frame.
Shader "Look Again/Viewfinder"
{
    Properties
    {
        [Header(Window)]
        _Margin ("Margin (window heights)", Float) = 0.12
        _Radius ("Corner Radius (window heights)", Float) = 0.06
        _Open ("Open", Range(0, 1)) = 1

        [Header(Lens)]
        _Tint ("Tint", Color) = (1, 0.975, 0.92, 1)
        _Vignette ("Vignette", Range(0, 1)) = 0.45
        _EdgeShadow ("Edge Shadow", Range(0, 1)) = 0.5
        _Grain ("Grain", Range(0, 0.3)) = 0.08
        _GrainSize ("Grain Cells per Window Height", Float) = 380
        _Flicker ("Flicker", Range(0, 0.1)) = 0.025
        _LineColor ("Frame Line Color", Color) = (1, 0.94, 0.82, 1)
        _Lines ("Frame Lines", Range(0, 1)) = 0.2
        _Scratches ("Scratches", 2D) = "black" {}
        _ScratchColor ("Scratch Color", Color) = (1, 0.97, 0.9, 1)
        _ScratchOpacity ("Scratch Opacity", Range(0, 1)) = 0.1
        _ScratchBlack ("Scratch Black Point", Range(0, 1)) = 0.3

        [Header(Frame)]
        _BodyColor ("Body Color", Color) = (0.1, 0.09, 0.08, 1)
        _RimColor ("Rim Color", Color) = (0.38, 0.35, 0.3, 1)
        _RimWidth ("Rim Width (window heights)", Float) = 0.014
        _Band ("Solid Band (window heights)", Float) = 0.035
        _Feather ("Fade Out (window heights)", Float) = 0.07
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _Margin;
        float _Radius;
        half _Open;
        half4 _Tint;
        half _Vignette;
        half _EdgeShadow;
        half _Grain;
        float _GrainSize;
        half _Flicker;
        half4 _LineColor;
        half _Lines;
        float4 _Scratches_TexelSize;
        half4 _ScratchColor;
        half _ScratchOpacity;
        half _ScratchBlack;
        half4 _BodyColor;
        half4 _RimColor;
        float _RimWidth;
        float _Band;
        float _Feather;
        CBUFFER_END

        #include "Viewfinder.hlsl"

        TEXTURE2D(_Scratches);
        SAMPLER(sampler_Scratches);

        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                        lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
        }

        struct Window
        {
            float2 p;        // in window heights from the window's center
            float2 halfSize;
            float d;         // distance out from the window's edge
            float aa;        // one pixel, in window heights
            float frame;     // 1 on the frame, 0 in the window, blended across the edge
        };

        Window GetWindow(ViewfinderVaryings input)
        {
            Window w;
            w.p = input.position;
            w.halfSize = float2(input.aspect * 0.5, 0.5);
            w.d = ViewfinderDistance(w.p, input.aspect);
            w.aa = max(fwidth(w.d), 1e-5);
            w.frame = smoothstep(-w.aa, w.aa, w.d);
            return w;
        }
        ENDHLSL

        // Multiplies the view in the window: tint, vignette, grain and flicker
        Pass
        {
            Name "Lens Tint"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend DstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex ViewfinderVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            half4 Fragment(ViewfinderVaryings input) : SV_Target
            {
                Window w = GetWindow(input);
                float filmFrame = floor(_Time.y * 24);
                half vignette = smoothstep(0.55, 1.45, length(w.p / w.halfSize * float2(1, 0.9))) * _Vignette;
                half edge = smoothstep(-0.04, 0, w.d) * _EdgeShadow;
                half flicker = Hash21(float2(filmFrame, 7.1)) * _Flicker;
                half grain = Hash21(floor(w.p * _GrainSize) + filmFrame * float2(31.7, 17.3)) * _Grain;
                half3 lens = _Tint.rgb * saturate(1 - vignette - edge - flicker - grain);
                return half4(lerp(1, lens, (1 - w.frame) * _Open), 1);
            }
            ENDHLSL
        }

        // Blends the scratches, brackets and focus patch into the window, and draws the frame around it
        Pass
        {
            Name "Lens Marks and Frame"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex ViewfinderVertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            // Scratches on the lens, scaled to cover the window without stretching
            half Scratches(Window w)
            {
                float textureAspect = _Scratches_TexelSize.z / _Scratches_TexelSize.w;
                float scale = max(w.halfSize.x * 2 / textureAspect, 1);
                float2 uv = w.p / (scale * float2(textureAspect, 1)) + 0.5;
                half brightness = SAMPLE_TEXTURE2D(_Scratches, sampler_Scratches, uv).r;
                return saturate((brightness - _ScratchBlack) / max(1 - _ScratchBlack, 1e-3)) * _ScratchOpacity;
            }

            // Scratches, with the corner brackets and focus patch on top
            half4 Marks(Window w)
            {
                half scratches = Scratches(w);
                half4 result = half4(_ScratchColor.rgb * scratches, scratches);

                float2 inner = w.halfSize * 0.74;
                float bracketLength = 0.07;
                float brackets = 1 - smoothstep(0.0035 - w.aa, 0.0035 + w.aa, abs(RoundedBoxDistance(w.p, inner, 0.01)));
                brackets *= step(inner.x - bracketLength, abs(w.p.x)) * step(inner.y - bracketLength, abs(w.p.y));
                float patch = 1 - smoothstep(0.0025 - w.aa, 0.0025 + w.aa, abs(RoundedBoxDistance(w.p, float2(0.05, 0.034), 0.006)));
                half amount = saturate(brackets + patch * 0.6) * _Lines;
                return result * (1 - amount) + half4(_LineColor.rgb * amount, amount);
            }

            half4 Frame(Window w)
            {
                // Fine pebbled grain in the body, like leatherette
                half3 body = _BodyColor.rgb * (0.7 + 0.6 * ValueNoise(w.p * 140) * ValueNoise(w.p * 37 + 5.3));

                // The rim slopes down into the window, so its lower right catches a light from the upper left
                float e = 0.002;
                float aspect = w.halfSize.x * 2;
                float2 outward = normalize(float2(
                    ViewfinderDistance(w.p + float2(e, 0), aspect) - ViewfinderDistance(w.p - float2(e, 0), aspect),
                    ViewfinderDistance(w.p + float2(0, e), aspect) - ViewfinderDistance(w.p - float2(0, e), aspect)) + 1e-6);
                half light = saturate(dot(-outward, normalize(float2(-0.6, 0.8))));
                half3 rim = _RimColor.rgb * (0.35 + 1.1 * light * light);
                half rimAmount = 1 - smoothstep(_RimWidth * 0.8, _RimWidth, w.d);

                half alpha = 1 - smoothstep(_Band, _Band + _Feather, w.d);
                return half4(lerp(body, rim, rimAmount) * alpha, alpha);
            }

            half4 Fragment(ViewfinderVaryings input) : SV_Target
            {
                Window w = GetWindow(input);
                return lerp(Marks(w), Frame(w), w.frame) * _Open;
            }
            ENDHLSL
        }
    }
}
