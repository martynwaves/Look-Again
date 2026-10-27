Shader "Custom/TopDownNotebook"
{
    Properties
    {
        _PaperColor ("Paper Color", Color) = (0.95, 0.93, 0.88, 1.0)
        _CoverColor ("Cover / Border Color", Color) = (0.22, 0.15, 0.10, 1.0)
        _Open ("Open Progress", Range(0, 1)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _PaperColor;
            fixed4 _CoverColor;
            float _Open;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i : SV_Target) : SV_Target
            {
                // Create clean borders for the leather cover frame (outer 6%)
                float2 edge = abs(i.uv - 0.5) * 2.0;
                float isCover = (edge.x > 0.88 || edge.y > 0.88) ? 1.0 : 0.0;

                // Realistic center spine shadow (gutter)
                float spineDist = abs(i.uv.x - 0.5);
                float spineShadow = smoothstep(0.0, 0.06, spineDist);
                spineShadow = lerp(0.4, 1.0, spineShadow); // Darkens towards the middle crease

                // Subtle page texture noise or gradient variation
                float grain = (frac(sin(dot(i.uv, float2(12.9898, 78.233))) * 43758.5453) - 0.5) * 0.04;

                fixed4 finalColor = isCover > 0.5 ? _CoverColor : (_PaperColor + grain) * spineShadow;

                // Modulate with animation property
                finalColor.rgb *= saturate(_Open);

                return finalColor;
            }
            ENDCG
        }
    }
}