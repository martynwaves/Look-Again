// The viewfinder window's rounded-rectangle shape, shared by the mask and lens shaders.
// Include it after declaring _Margin and _Radius in the material buffer.

#ifndef LOOK_AGAIN_VIEWFINDER_INCLUDED
#define LOOK_AGAIN_VIEWFINDER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct ViewfinderAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ViewfinderVaryings
{
    float4 positionCS : SV_POSITION;
    float2 position : TEXCOORD0; // in window heights from the window's center
    float aspect : TEXCOORD1;    // window width over height
    UNITY_VERTEX_OUTPUT_STEREO
};

ViewfinderVaryings ViewfinderVertex(ViewfinderAttributes input)
{
    ViewfinderVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    float4x4 m = GetObjectToWorldMatrix();
    float2 scale = float2(length(m._m00_m10_m20), length(m._m01_m11_m21));
    float height = scale.y / (1 + 2 * _Margin);
    output.position = (input.uv - 0.5) * scale / height;
    output.aspect = scale.x / height - 2 * _Margin;
    return output;
}

float RoundedBoxDistance(float2 p, float2 halfSize, float radius)
{
    float2 q = abs(p) - halfSize + radius;
    return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
}

// Signed distance to the window's edge in window heights: negative inside the window, positive out on the frame
float ViewfinderDistance(float2 p, float aspect)
{
    return RoundedBoxDistance(p, float2(aspect * 0.5, 0.5), _Radius);
}

#endif
