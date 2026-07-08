Shader "Wiwiw/Skybox"
{
    Properties
    {
        [Header(Sky)]
        [Space]
        _CubeTexture ("Cube Texture", Cube) = "grey" {}
        _Intensity ("Intensity", Float) = 1.0
    }


    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "ForceNoShadowCasting" = "True"
            "DisableBatching" = "True"
            "IgnoreProjector" = "True"
            "PreviewType" = "Skybox"
            "CanUseSpriteAtlas" = "False"
        }


        Pass
        {
            Name "Skybox"


            AlphaToMask Off
            Blend Off
            BlendOp Add
            ColorMask RGB 0
            Conservative False
            Cull Off
            Offset 0, 0
            // Stencil
            ZClip False
            ZTest LEqual
            ZWrite Off


            HLSLPROGRAM


            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Wiwiw/ShaderLibrary/Core.hlsl"


            struct o2v
            {
                float4 positionOS : POSITION0;
            };


            struct v2f
            {
                float3 directionWS : TEXCOORD0;
            };


            struct f2o
            {
                float3 color : SV_TARGET0;
            };


            float4x4 unity_MatrixVP;
            float4x4 unity_ObjectToWorld;
            float Wiwiw_DitherScale;
            TEXTURECUBE(_CubeTexture);
            SAMPLER(sampler_CubeTexture);
            float4 _CubeTexture_TexelSize;
            float _Intensity;


            void vert(in o2v IN, out v2f OUT, out float4 OUT_POS_CS : SV_POSITION)
            {
                OUT.directionWS = mul(unity_ObjectToWorld, float4(IN.positionOS.xyz, 0.0)).xyz;
                OUT.directionWS = normalize(OUT.directionWS);

                OUT_POS_CS = mul(unity_ObjectToWorld, IN.positionOS);
                OUT_POS_CS = mul(unity_MatrixVP, OUT_POS_CS);
            }


            void frag(in v2f IN, float4 IN_VPOS : VPOS, out f2o OUT)
            {
                IN.directionWS = normalize(IN.directionWS);

                const uint2 skyboxDitherCoord = uint2(Wiwiw_CubeFaceUv(IN.directionWS) * _CubeTexture_TexelSize.zw);
                float3 skyboxSample;
                skyboxSample = SAMPLE_TEXTURECUBE(_CubeTexture, sampler_CubeTexture, IN.directionWS).rgb;
                skyboxSample = Wiwiw_DitherTo5Bit(skyboxSample, skyboxDitherCoord);

                const uint2 finalDitherCoord = uint2(IN_VPOS.xy * Wiwiw_DitherScale);
                float3 finalColor;
                finalColor = skyboxSample * _Intensity;
                finalColor = Wiwiw_DitherTo5Bit(finalColor, finalDitherCoord);

                OUT.color = finalColor;
            }


            ENDHLSL
        }
    }
}
