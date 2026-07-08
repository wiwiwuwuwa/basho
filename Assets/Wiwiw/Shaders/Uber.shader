Shader "Wiwiw/Uber"
{
    Properties
    {
        [Header(Albedo)]
        [Space]
        [MainTexture] _AlbedoTexture ("Albedo Texture", 2D) = "white" {}
        [MainColor] _AlbedoColor ("Albedo Color", Color) = (1, 1, 1, 1)

        [Header(Emission)]
        [Space]
        _EmissionTexture ("Emission Texture", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Sky Occlusion)]
        [Space]
        [Toggle(_ENABLE_SPECULAR)] _EnableSpecular ("Enable Specular", Float) = 1
        [Toggle(_IS_DYNAMIC_OCCLUSION_OBJECT)] _IsDynamicOcclusionObject ("Is Dynamic Occlusion Object", Float) = 0
    }


    SubShader
    {
        Tags
        {
            "RenderPipeline" = "PsxSrp"
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "ForceNoShadowCasting" = "True"
            "DisableBatching" = "True"
            "IgnoreProjector" = "True"
            "PreviewType" = "Sphere"
            "CanUseSpriteAtlas" = "False"
        }


        Pass
        {
            Name "ShadowCasterPass"
            Tags { "LightMode" = "ShadowCasterPass" }


            AlphaToMask Off
            Blend Off
            BlendOp Add
            ColorMask 0
            Conservative False
            Cull Off
            Offset 0, 0
            // Stencil
            ZClip True
            ZTest LEqual
            ZWrite On


            HLSLPROGRAM


            #pragma vertex vert
            #pragma fragment frag


            #include "Assets/Wiwiw/ShaderLibrary/Core.hlsl"


            struct o2v
            {
                float4 positionOS : POSITION0;
            };


            struct f2o
            {
                float color : SV_TARGET0;
            };


            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;


            void vert(in o2v IN, out float4 OUT_POS_CS : SV_POSITION)
            {
                OUT_POS_CS = mul(unity_ObjectToWorld, IN.positionOS);
                OUT_POS_CS = mul(unity_MatrixVP, OUT_POS_CS);
            }


            void frag(out f2o OUT)
            {
                OUT.color = 0;
            }


            ENDHLSL
        }


        Pass
        {
            Name "OpaquePass"
            Tags { "LightMode" = "OpaquePass" }


            AlphaToMask Off
            Blend Off
            BlendOp Add
            ColorMask RGB 0
            Conservative False
            Cull Back
            Offset 0, 0
            // Stencil
            ZClip True
            ZTest LEqual
            ZWrite On


            HLSLPROGRAM


            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ENABLE_SPECULAR
            #pragma shader_feature_local _IS_DYNAMIC_OCCLUSION_OBJECT


            #include "Assets/Wiwiw/ShaderLibrary/Core.hlsl"
            #include "Assets/Wiwiw/ShaderLibrary/SkyOcclusion.hlsl"


            struct o2v
            {
                float3 positionOS : POSITION0;
                float3 normalOS : NORMAL0;
                float2 uv : TEXCOORD0;
            };


            struct v2f
            {
                noperspective float2 albedoUv : TEXCOORD0;
                noperspective float2 emissionUv : TEXCOORD1;

                #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
                    noperspective float3 positionOS : TEXCOORD2;
                    noperspective float3 normalOS : TEXCOORD3;
                #else // _IS_DYNAMIC_OCCLUSION_OBJECT
                    noperspective float3 positionWS : TEXCOORD2;
                    noperspective float3 normalWS : TEXCOORD3;
                    #if defined(_ENABLE_SPECULAR)
                        noperspective float3 reflectionWS : TEXCOORD4;
                    #endif // _ENABLE_SPECULAR
                #endif // _IS_DYNAMIC_OCCLUSION_OBJECT
            };


            struct f2o
            {
                float3 color : SV_TARGET0;
            };


            float4 _WorldSpaceCameraPos;
            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
            float Wiwiw_DitherScale;
            TEXTURE2D(_AlbedoTexture);
            SAMPLER(sampler_AlbedoTexture);
            float4 _AlbedoTexture_ST;
            float4 _AlbedoTexture_TexelSize;
            float4 _AlbedoColor;
            TEXTURE2D(_EmissionTexture);
            SAMPLER(sampler_EmissionTexture);
            float4 _EmissionTexture_ST;
            float4 _EmissionTexture_TexelSize;
            float4 _EmissionColor;

            TEXTURE2D_ARRAY(Wiwiw_SkyOcclusion_ShadowMapArray);
            SAMPLER_CMP(sampler_Wiwiw_SkyOcclusion_ShadowMapArray);
            float4x4 Wiwiw_SkyOcclusion_WorldToShadowMatrixArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
            float4 Wiwiw_SkyOcclusion_ShadowDirectionArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
            uint Wiwiw_SkyOcclusion_SliceCount;
            uint Wiwiw_SkyOcclusion_FrameIndex;

            #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
                float4x4 Wiwiw_SkyOcclusion_ObjectToWorldMatrixArray[WIWIW_MAX_SKY_OCCLUSION_SLICE_COUNT];
            #endif // _IS_DYNAMIC_OCCLUSION_OBJECT


            void vert(in o2v IN, out v2f OUT, out float4 OUT_POS_CS : SV_POSITION)
            {
                const float3 positionWS = mul(unity_ObjectToWorld, float4(IN.positionOS, 1.0)).xyz;

                OUT.albedoUv = TRANSFORM_TEX(IN.uv, _AlbedoTexture);
                OUT.emissionUv = TRANSFORM_TEX(IN.uv, _EmissionTexture);
                OUT_POS_CS = mul(unity_MatrixVP, float4(positionWS, 1.0));
                OUT_POS_CS.xyz *= rcp(OUT_POS_CS.w);
                OUT_POS_CS.xyz = Wiwiw_JitterVertex(OUT_POS_CS.xyz);
                OUT_POS_CS.xyz *= OUT_POS_CS.w;

                #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
                    OUT.positionOS = IN.positionOS;
                    OUT.normalOS = normalize(IN.normalOS);
                #else // _IS_DYNAMIC_OCCLUSION_OBJECT
                    const float3 normalWS = normalize(mul(unity_ObjectToWorld, float4(IN.normalOS, 0.0)).xyz);
                    OUT.positionWS = positionWS;
                    OUT.normalWS = normalWS;
                    #if defined(_ENABLE_SPECULAR)
                        const float3 viewWS = normalize(positionWS.xyz - _WorldSpaceCameraPos.xyz);
                        OUT.reflectionWS = reflect(viewWS, normalWS);
                    #endif // _ENABLE_SPECULAR
                #endif // _IS_DYNAMIC_OCCLUSION_OBJECT
            }


            void frag(in v2f IN, float4 IN_VPOS : VPOS, out f2o OUT)
            {
                Wiwiw_SkyOcclusionIn IN_SKY;
                IN_SKY.shadowMapArray = Wiwiw_SkyOcclusion_ShadowMapArray;
                IN_SKY.sampler_ShadowMapArray = sampler_Wiwiw_SkyOcclusion_ShadowMapArray;
                IN_SKY.worldToShadowMatrixArray = Wiwiw_SkyOcclusion_WorldToShadowMatrixArray;
                IN_SKY.shadowDirectionArray = Wiwiw_SkyOcclusion_ShadowDirectionArray;
                IN_SKY.sliceCount = Wiwiw_SkyOcclusion_SliceCount;
                IN_SKY.frameIndex = Wiwiw_SkyOcclusion_FrameIndex;
                IN_SKY.pixelCoord = uint2(IN_VPOS.xy);

                #if defined(_IS_DYNAMIC_OCCLUSION_OBJECT)
                    IN_SKY.worldSpaceCameraPos = _WorldSpaceCameraPos;
                    IN_SKY.objectToWorldMatrixArray = Wiwiw_SkyOcclusion_ObjectToWorldMatrixArray;
                    IN_SKY.positionOS = IN.positionOS;
                    IN_SKY.normalOS = normalize(IN.normalOS);
                #else // _IS_DYNAMIC_OCCLUSION_OBJECT
                    IN_SKY.positionWS = IN.positionWS;
                    IN_SKY.normalWS = normalize(IN.normalWS);
                    #if defined(_ENABLE_SPECULAR)
                        IN_SKY.reflectionWS = normalize(IN.reflectionWS);
                    #endif // _ENABLE_SPECULAR
                #endif // _IS_DYNAMIC_OCCLUSION_OBJECT

                Wiwiw_SkyOcclusionOut OUT_SKY;
                OUT_SKY = Wiwiw_ComputeSkyOcclusion(IN_SKY);

                float3 albedoSample;
                albedoSample = SAMPLE_TEXTURE2D(_AlbedoTexture, sampler_AlbedoTexture, IN.albedoUv).rgb;
                albedoSample = Wiwiw_DitherTo5Bit(albedoSample, uint2(IN.albedoUv * _AlbedoTexture_TexelSize.zw));

                float3 emissionSample;
                emissionSample = SAMPLE_TEXTURE2D(_EmissionTexture, sampler_EmissionTexture, IN.emissionUv).rgb;
                emissionSample = Wiwiw_DitherTo5Bit(emissionSample, uint2(IN.emissionUv * _EmissionTexture_TexelSize.zw));

                float3 finalColor;
                finalColor = _EmissionColor.rgb * emissionSample;

                #if ('uwu' == 'uwu')
                    const float3 environmentDiffuse = EvaluateAmbientProbe(OUT_SKY.diffuseDirection) * OUT_SKY.diffuseOcclusion;
                    finalColor += _AlbedoColor.rgb * albedoSample * environmentDiffuse;
                #endif // ('uwu' == 'uwu')

                #if defined(_ENABLE_SPECULAR)
                    const float3 environmentSpecular = Wiwiw_SampleSkySpecular(OUT_SKY.specularDirection) * OUT_SKY.specularOcclusion;
                    finalColor += environmentSpecular;
                #endif // _ENABLE_SPECULAR

                OUT.color = Wiwiw_DitherTo5Bit(finalColor, uint2(IN_VPOS.xy * Wiwiw_DitherScale));
            }


            ENDHLSL
        }
    }
}
