#ifndef SGE_HDRP_COMMON_INCLUDED
#define SGE_HDRP_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

// Depth and shadow bias constants
#define ML_DEPTH_EYE_BIAS  0.005
#define ML_OFFSET_SHADOW_EYE_BIAS  0.001

// UV setup utilities
void SetupSurround8UVs(float2 uvCenter, inout float2 uvs[8], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 1);
    uvs[1] = uvCenter + uvStep * float2(0, 1);
    uvs[2] = uvCenter + uvStep * float2(1, 1);
    uvs[3] = uvCenter + uvStep * float2(-1, 0);
    uvs[4] = uvCenter + uvStep * float2(1, 0);
    uvs[5] = uvCenter + uvStep * float2(-1, -1);
    uvs[6] = uvCenter + uvStep * float2(0, -1);
    uvs[7] = uvCenter + uvStep * float2(1, -1);
}

void SetupSurroundCrossUVs(float2 uvCenter, inout float2 uvs[4], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 1);
    uvs[1] = uvCenter + uvStep * float2(-1, -1);
    uvs[2] = uvCenter + uvStep * float2(1, 1);
    uvs[3] = uvCenter + uvStep * float2(1, -1);
}

void SetupSurroundLRTDUVs(float2 uvCenter, inout float2 uvs[4], float2 uvStep)
{
    uvs[0] = uvCenter + uvStep * float2(-1, 0);
    uvs[1] = uvCenter + uvStep * float2(1, 0);
    uvs[2] = uvCenter + uvStep * float2(0, 1);
    uvs[3] = uvCenter + uvStep * float2(0, -1);
}

// Dithering function for HDRP
void Unity_Dither_HDRP(float In, float2 ScreenPosition, float2 ScreenSize, half ditherPixelSize, out float Out)
{
    float2 uv = ScreenPosition.xy * ScreenSize / ditherPixelSize;
    float DITHER_THRESHOLDS[16] =
    {
        1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
    };
    uint index = (uint(uv.x) % 4) * 4 + uint(uv.y) % 4;
    Out = In - DITHER_THRESHOLDS[index];
}

// Rotate UV in degrees
float2 RotateUVDeg(float2 UV, float2 Center, float Rotation)
{
    float2 uv = UV;
    Rotation = Rotation * (PI/180.0f);
    uv -= Center;
    float s = sin(Rotation);
    float c = cos(Rotation);
    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;
    uv.xy = mul(uv.xy, rMatrix);
    uv += Center;
    return uv;
}

// Hash functions for procedural generation
half3 hash31(float p)
{
    half3 p3 = frac(half3(p, p, p) * half3(.1031, .1030, .0973));
    p3 += dot(p3, p3.yzx+33.33);
    return frac((p3.xxy+p3.yzz)*p3.zyx); 
}

half hash11(float p)
{
    p = frac(p * .1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

// Material ID decoding functions
half3 DecodeMaterialIDToColor(half value)
{
    half materialID = value;
    materialID *= 255.0;
    float factor = step(1, materialID);
    return factor * hash31(materialID) + (1.0 - factor) * float3(0,0,0);
}

half DecodeMaterialIDToFloat(half value)
{
    half materialID = value;
    materialID *= 255.0;
    return step(1, materialID);
}

// Remap function
float Remapfloat(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

// HDRP-specific transform functions
float4 TransformHClipToViewPortPos_HDRP(float4 positionCS)
{
    float4 o = positionCS * 0.5f;
    o.xy = float2(o.x, o.y * _ProjectionParams.x) + o.w;
    o.zw = positionCS.zw;
    return o / o.w;
}

// FOV adjustment for HDRP
float3 GetFOVAdjustedPositionOS_HDRP(float3 positionOS, float3 objectCenterWS, float shift, float4x4 viewMatrix, float4x4 invViewMatrix)
{
    float3 objectCenterVS = mul(viewMatrix, float4(objectCenterWS, 1.0)).xyz;
    float3 fovAdjustedPositionVS = mul(viewMatrix, float4(TransformObjectToWorld(positionOS), 1.0)).xyz;
    fovAdjustedPositionVS.z = (fovAdjustedPositionVS.z - objectCenterVS.z)/(shift + 1) + objectCenterVS.z;
    float3 fovAdjustedPositionWS = mul(invViewMatrix, float4(fovAdjustedPositionVS, 1.0)).xyz;
    return mul(GetWorldToObjectMatrix(), float4(fovAdjustedPositionWS, 1.0)).xyz;
}

#endif // SGE_HDRP_COMMON_INCLUDED
