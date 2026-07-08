using UnityEngine;
using Unity.Mathematics;


public static class MathUtil
{
    public static float4x4 ComputeWorldToShadowMatrix(float4x4 view, float4x4 proj)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            var flip = float4x4.Scale(1f, 1f, -1f);
            proj = math.mul(flip, proj);
        }

        var worldToShadow = math.mul(proj, view);

        {
            var clip = float4x4.TRS(0.5f, quaternion.identity, 0.5f);
            worldToShadow = math.mul(clip, worldToShadow);
        }

        return worldToShadow;
    }


    public static float3 ComputeShadowDirection(uint gridSize, uint sliceIndex, float coneDegrees)
    {
        if (gridSize <= 0 || sliceIndex >= math.dot(gridSize, gridSize))
            return 0f;

        coneDegrees = math.clamp(coneDegrees, 0f, 180f);

        var squareCoord = math.uint2(sliceIndex % gridSize, sliceIndex / gridSize);
        var squarePoint = ((float2)squareCoord + 0.5f) * math.rcp(gridSize);

        var diskPoint = ConcentricDiskSample(squarePoint);
        var coneRadius = math.sin(math.radians(coneDegrees) * 0.5f);

        var skyGround = diskPoint * coneRadius;
        var skyDown = -math.sqrt(math.saturate(1f - math.dot(skyGround, skyGround)));

        return math.float3(skyGround.x, skyDown, skyGround.y);
    }


    public static float2 ConcentricDiskSample(float2 uv)
    {
        var offsetFromCenter = math.remap(0f, 1f, -1f, 1f, uv);
        if (math.all(offsetFromCenter == 0f))
            return 0f;

        var diskRadius = 0f;
        var diskPolarAngle = 0f;

        if (math.dot(offsetFromCenter.x, offsetFromCenter.x) > math.dot(offsetFromCenter.y, offsetFromCenter.y))
        {
            diskRadius = offsetFromCenter.x;
            diskPolarAngle = (math.PI * 0.25f) * (offsetFromCenter.y * math.rcp(offsetFromCenter.x));
        }
        else
        {
            diskRadius = offsetFromCenter.y;
            diskPolarAngle = (math.PI * 0.5f) - (math.PI * 0.25f) * (offsetFromCenter.x * math.rcp(offsetFromCenter.y));
        }

        math.sincos(diskPolarAngle, out var diskY, out var diskX);
        var diskPoint = math.float2(diskX, diskY) * diskRadius;

        return diskPoint;
    }
}
