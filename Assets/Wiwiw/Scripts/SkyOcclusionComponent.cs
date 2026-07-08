using System;
using UnityEngine;
using UnityEngine.Rendering;


[AddComponentMenu("Wiwiw/Sky Occlusion Component", int.MinValue)]
public sealed class SkyOcclusionComponent : MonoBehaviour
{
    [Header("Sky Occlusion Component")]
    [Range(1u, PsxSrpConst.SkyOcclusion.MaxResolution)]
    public uint Resolution = PsxSrpConst.SkyOcclusion.DefaultResolution;
    [Range(0f, 180f)]
    public float SkyAngle = 120f;
    [Min(0f)]
    public float Radius = 32f;
    [Min(0f)]
    public float NearClipPlane = 0.3f;
    [Min(0f)]
    public float FarClipPlane = 256f;

    [NonSerialized] public RenderTexture ShadowMapArray;
    [NonSerialized] public Matrix4x4[] WorldToShadowMatrixArray;
    [NonSerialized] public Vector4[] ShadowDirectionArray;


    private void OnDisable()
    {
        CoreUtils.Destroy(ShadowMapArray);

        ShadowDirectionArray = null;
        WorldToShadowMatrixArray = null;
        ShadowMapArray = null;
    }
}
