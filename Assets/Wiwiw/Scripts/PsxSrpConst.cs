using Unity.Mathematics;


public static class PsxSrpConst
{
    public static class SkyOcclusion
    {
        public const uint DefaultResolution = 1024u;
        public const uint DefaultGridSize = 7u;
        public const uint DefaultSliceCount = DefaultGridSize * DefaultGridSize;
        public const uint MaxResolution = 4096u;
        public const uint MaxGridSize = 7u;
        public const uint MaxSliceCount = MaxGridSize * MaxGridSize;
    }


    public static class Camera
    {
        public static readonly uint2 DefaultRenderingResolution = math.uint2(1280u, 720u);
        public static readonly uint2 DefaultEmulationResolution = math.uint2(426u, 240u);
        public const uint MaxRenderingHeight = 4320u;
        public const uint MaxEmulationHeight = 4320u;
        public const uint MaxMsaaSampleCount = 8u;
    }
}
