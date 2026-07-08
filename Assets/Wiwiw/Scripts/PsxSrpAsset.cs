using UnityEngine;
using UnityEngine.Rendering;


[CreateAssetMenu(fileName = "Psx Srp", menuName = "Wiwiw/Psx SRP Asset", order = int.MinValue)]
public sealed class PsxSrpAsset : RenderPipelineAsset<PsxSrp>
{
    [Header("Sky Occlusion")]
    [Range(1u, PsxSrpConst.SkyOcclusion.MaxGridSize)]
    public uint SkyOcclusionGridSize = PsxSrpConst.SkyOcclusion.DefaultGridSize;

    [Header("Camera")]
    [Range(1u, PsxSrpConst.Camera.MaxRenderingHeight)]
    public uint RenderingHeight = PsxSrpConst.Camera.DefaultRenderingResolution.y;
    [Range(1u, PsxSrpConst.Camera.MaxEmulationHeight)]
    public uint EmulationHeight = PsxSrpConst.Camera.DefaultEmulationResolution.y;

    [Header("Anti-Aliasing")]
    public MsaaSampleCount MsaaSampleCount = MsaaSampleCount.X8;


    protected override RenderPipeline CreatePipeline()
    {
        return new PsxSrp(this);
    }
}
