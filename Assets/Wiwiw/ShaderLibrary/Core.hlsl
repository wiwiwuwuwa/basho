#ifndef WIWIW_CORE_INCLUDED
#define WIWIW_CORE_INCLUDED


#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"


// ----------------------------------------------------------------------------


CBUFFER_START(UnityLighting)
    half4 unity_SHAr;
    half4 unity_SHAg;
    half4 unity_SHAb;
    half4 unity_SHBr;
    half4 unity_SHBg;
    half4 unity_SHBb;
    half4 unity_SHC;
CBUFFER_END

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/AmbientProbe.hlsl"


// ----------------------------------------------------------------------------


TEXTURECUBE(unity_SpecCube0);
SAMPLER(sampler_unity_SpecCube0);
float4 unity_SpecCube0_HDR;


float3 Wiwiw_SampleSkySpecular(float3 directionWS)
{
    const float4 encodedIrradiance = SAMPLE_TEXTURECUBE(unity_SpecCube0, sampler_unity_SpecCube0, directionWS);
    const float3 decodedIrradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);

    return decodedIrradiance;
}


// ----------------------------------------------------------------------------


#define WIWIW_REMAP(v, inMin, inMax, outMin, outMax) \
    (((v) - (inMin)) * rcp((inMax) - (inMin)) * ((outMax) - (outMin)) + (outMin))


float1 Wiwiw_Remap(float1 value, float1 inMin, float1 inMax, float1 outMin, float1 outMax) { return WIWIW_REMAP(value, inMin, inMax, outMin, outMax); }
float2 Wiwiw_Remap(float2 value, float2 inMin, float2 inMax, float2 outMin, float2 outMax) { return WIWIW_REMAP(value, inMin, inMax, outMin, outMax); }
float3 Wiwiw_Remap(float3 value, float3 inMin, float3 inMax, float3 outMin, float3 outMax) { return WIWIW_REMAP(value, inMin, inMax, outMin, outMax); }
float4 Wiwiw_Remap(float4 value, float4 inMin, float4 inMax, float4 outMin, float4 outMax) { return WIWIW_REMAP(value, inMin, inMax, outMin, outMax); }


// ----------------------------------------------------------------------------


uint Wiwiw_Hash11(uint n)
{
    n = (n ^ 61u) ^ (n >> 16u);
    n = n + (n << 3u);
    n = n ^ (n >> 4u);
    n = n * 0x27D4EB2Du;
    n = n ^ (n >> 15u);

    return n;
}


float Wiwiw_HashToFloat01(uint n)
{
    return (float)(n & 0x00FFFFFFu) * (1.0 / 16777216.0);
}


float Wiwiw_Hash14(uint4 p)
{
    uint n = 0x5EEDu;

    n = Wiwiw_Hash11(p.w + n);
    n = Wiwiw_Hash11(p.z + n);
    n = Wiwiw_Hash11(p.y + n);
    n = Wiwiw_Hash11(p.x + n);

    return Wiwiw_HashToFloat01(n);
}


// ----------------------------------------------------------------------------


float3 Wiwiw_JitterVertex(float3 vertexNDC)
{
    const float2 ndcMin = -1.0;
    const float2 ndcMax = +1.0;
    const float2 gridMin = -128.0;
    const float2 gridMax = +128.0;

    vertexNDC.xy = Wiwiw_Remap(vertexNDC.xy, ndcMin, ndcMax, gridMin, gridMax);
    vertexNDC.xy = round(vertexNDC.xy);
    vertexNDC.xy = Wiwiw_Remap(vertexNDC.xy, gridMin, gridMax, ndcMin, ndcMax);

    return vertexNDC;
}


// ----------------------------------------------------------------------------


#define WIWIW_DITHER_TO_5BIT(value, coord) \
    (floor(saturate(value) * 31.0 + Wiwiw_Bayer4x4(coord)) * (1.0 / 31.0))


float Wiwiw_Bayer4x4(uint2 coord)
{
    static const float4x4 kDitherMatrix =
    {
         0.5 / 16.0,     8.5 / 16.0,     2.5 / 16.0,    10.5 / 16.0,
        12.5 / 16.0,     4.5 / 16.0,    14.5 / 16.0,     6.5 / 16.0,
         3.5 / 16.0,    11.5 / 16.0,     1.5 / 16.0,     9.5 / 16.0,
        15.5 / 16.0,     7.5 / 16.0,    13.5 / 16.0,     5.5 / 16.0
    };

    coord = coord & 3u;
    return kDitherMatrix[coord.y][coord.x];
}


float1 Wiwiw_DitherTo5Bit(float1 value, uint2 coord) { return WIWIW_DITHER_TO_5BIT(value, coord); }
float2 Wiwiw_DitherTo5Bit(float2 value, uint2 coord) { return WIWIW_DITHER_TO_5BIT(value, coord); }
float3 Wiwiw_DitherTo5Bit(float3 value, uint2 coord) { return WIWIW_DITHER_TO_5BIT(value, coord); }
float4 Wiwiw_DitherTo5Bit(float4 value, uint2 coord) { return WIWIW_DITHER_TO_5BIT(value, coord); }


// ----------------------------------------------------------------------------


float2 Wiwiw_CubeFaceUv(float3 dir)
{
    const float3 absDir = abs(dir);

    float mainAxis;
    float2 faceUv;

    // +X / -X face
    if (absDir.x >= absDir.y && absDir.x >= absDir.z)
    {
        mainAxis = absDir.x;
        faceUv = dir.zy;
    }
    // +Y / -Y face
    else if (absDir.y >= absDir.x && absDir.y >= absDir.z)
    {
        mainAxis = absDir.y;
        faceUv = dir.xz;
    }
    // +Z / -Z face
    else
    {
        mainAxis = absDir.z;
        faceUv = dir.xy;
    }

    return Wiwiw_Remap(faceUv, -mainAxis, +mainAxis, 0.0, 1.0);
}


// ----------------------------------------------------------------------------


#endif // WIWIW_CORE_INCLUDED
