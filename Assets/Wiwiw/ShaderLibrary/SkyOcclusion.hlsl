#ifndef WIWIW_SKY_OCCLUSION_INCLUDED
#define WIWIW_SKY_OCCLUSION_INCLUDED


#define WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT 49
#define WIWIW_SKY_OCCLUSION_SAMPLE_COUNT 8


struct Wiwiw_SkyOcclusionIn
{
    TEXTURE2D_ARRAY(shadowMapArray);
    SAMPLER_CMP(sampler_ShadowMapArray);
    float4x4 worldToShadowMatrixArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
    float4 shadowDirectionArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
    uint sliceCount;
    uint frameIndex;
    uint2 pixelCoord;

    #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
        float3 worldSpaceCameraPos;
        float4x4 objectToWorldMatrixArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
        float3 positionOS;
        float3 normalOS;
    #else // _IS_DYNAMIC_OCCLUSION_OBJECT
        float3 positionWS;
        float3 normalWS;
        #if defined(_ENABLE_SPECULAR)
            float3 reflectionWS;
        #endif // _ENABLE_SPECULAR
    #endif // _IS_DYNAMIC_OCCLUSION_OBJECT
};


struct Wiwiw_SkyOcclusionOut
{
    #if ('uwu' == 'uwu')
        float3 diffuseDirection;
        float diffuseOcclusion;
    #endif // ('uwu' == 'uwu')

    #if defined(_ENABLE_SPECULAR)
        float3 specularDirection;
        float specularOcclusion;
    #endif // _ENABLE_SPECULAR
};


Wiwiw_SkyOcclusionOut Wiwiw_ComputeSkyOcclusion(in Wiwiw_SkyOcclusionIn IN)
{
    #if ('uwu' == 'uwu')
        float diffuseWeightSum = 0.0;
        float3 diffuseDirectionSum = 0.0;
        float diffuseOcclusionSum = 0.0;
    #endif // ('uwu' == 'uwu')

    #if defined(_ENABLE_SPECULAR)
        float specularWeightSum = 0.0;
        float3 specularDirectionSum = 0.0;
        float specularOcclusionSum = 0.0;
    #endif // _ENABLE_SPECULAR

    UNITY_LOOP
    for (uint s = 0u; s < WIWIW_SKY_OCCLUSION_SAMPLE_COUNT; s++)
    {
        const float r = Wiwiw_Hash14(uint4(IN.pixelCoord.x, IN.pixelCoord.y, IN.frameIndex, s));
        const uint i = min((uint)(r * IN.sliceCount), IN.sliceCount - 1u);
        const float4x4 worldToShadow = IN.worldToShadowMatrixArray[i];
        const float4 shadowDirection = IN.shadowDirectionArray[i];

        #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
            const float4x4 objectToWorld = IN.objectToWorldMatrixArray[i];
            const float3 positionWS = mul(objectToWorld, float4(IN.positionOS, 1.0)).xyz;
            const float3 normalWS = normalize(mul(objectToWorld, float4(IN.normalOS, 0.0)).xyz);
            #if defined(_ENABLE_SPECULAR)
                const float3 reflectionWS = reflect(normalize(positionWS - IN.worldSpaceCameraPos), normalWS);
            #endif // _ENABLE_SPECULAR
        #else // _IS_DYNAMIC_OCCLUSION_OBJECT
            const float3 positionWS = IN.positionWS;
            const float3 normalWS = IN.normalWS;
            #if defined(_ENABLE_SPECULAR)
                const float3 reflectionWS = IN.reflectionWS;
            #endif // _ENABLE_SPECULAR
        #endif // _IS_DYNAMIC_OCCLUSION_OBJECT

        const float3 lightWS = -shadowDirection.xyz;
        const float4 positionCS = mul(worldToShadow, float4(positionWS, 1.0));
        const float3 positionUV = positionCS.xyz * rcp(max(1.0e-6, positionCS.w));
        const float insideProjection = step(1.0e-6, positionCS.w) * step(0.0, positionUV.x) * step(positionUV.x, 1.0) * step(0.0, positionUV.y) * step(positionUV.y, 1.0) * step(0.0, positionUV.z) * step(positionUV.z, 1.0);
        const float shadowSample = SAMPLE_TEXTURE2D_ARRAY_SHADOW(IN.shadowMapArray, IN.sampler_ShadowMapArray, float3(saturate(positionUV.xy), saturate(positionUV.z)), i).r;
        const float visibility = lerp(1.0, shadowSample, insideProjection);

        #if ('uwu' == 'uwu')
            const float diffuseWeight = max(0.0, dot(normalWS, lightWS));
            const float3 diffuseDirection = diffuseWeight * visibility * lightWS;
            const float diffuseOcclusion = diffuseWeight * visibility;

            diffuseWeightSum += diffuseWeight;
            diffuseDirectionSum += diffuseDirection;
            diffuseOcclusionSum += diffuseOcclusion;
        #endif // ('uwu' == 'uwu')

        #if defined(_ENABLE_SPECULAR)
            const float specularWeight = pow(max(0.0, dot(reflectionWS, lightWS)), 1024.0);
            const float3 specularDirection = specularWeight * visibility * lightWS;
            const float specularOcclusion = specularWeight * visibility;

            specularWeightSum += specularWeight;
            specularDirectionSum += specularDirection;
            specularOcclusionSum += specularOcclusion;
        #endif // _ENABLE_SPECULAR
    }

    Wiwiw_SkyOcclusionOut OUT;

    #if ('uwu' == 'uwu')
        OUT.diffuseDirection = (diffuseDirectionSum != 0.0) ? normalize(diffuseDirectionSum) : 0.0;
        OUT.diffuseOcclusion = (diffuseWeightSum != 0.0) ? (diffuseOcclusionSum * rcp(diffuseWeightSum)) : 0.0;
    #endif // ('uwu' == 'uwu')

    #if defined(_ENABLE_SPECULAR)
        OUT.specularDirection = (specularDirectionSum != 0.0) ? normalize(specularDirectionSum) : 0.0;
        OUT.specularOcclusion = (specularWeightSum != 0.0) ? (specularOcclusionSum * rcp(specularWeightSum)) : 0.0;
    #endif // _ENABLE_SPECULAR

    return OUT;
}


#endif // WIWIW_SKY_OCCLUSION_INCLUDED
