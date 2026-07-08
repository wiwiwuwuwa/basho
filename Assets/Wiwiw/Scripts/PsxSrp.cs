using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Experimental.Rendering;
using Unity.Mathematics;


public sealed class PsxSrp : RenderPipeline
{
    private static readonly int s_DitherScale = Shader.PropertyToID("Wiwiw_DitherScale");
    private static readonly int s_SkyOcclusion_ObjectToWorldMatrixArray = Shader.PropertyToID("Wiwiw_SkyOcclusion_ObjectToWorldMatrixArray");
    private static readonly int s_SkyOcclusion_ShadowMapArray = Shader.PropertyToID("Wiwiw_SkyOcclusion_ShadowMapArray");
    private static readonly int s_SkyOcclusion_WorldToShadowMatrixArray = Shader.PropertyToID("Wiwiw_SkyOcclusion_WorldToShadowMatrixArray");
    private static readonly int s_SkyOcclusion_ShadowDirectionArray = Shader.PropertyToID("Wiwiw_SkyOcclusion_ShadowDirectionArray");
    private static readonly int s_SkyOcclusion_SliceCount = Shader.PropertyToID("Wiwiw_SkyOcclusion_SliceCount");
    private static readonly int s_SkyOcclusion_FrameIndex = Shader.PropertyToID("Wiwiw_SkyOcclusion_FrameIndex");
    private static readonly int s_Camera_ColorMsaaTarget = Shader.PropertyToID("Wiwiw_Camera_ColorMsaaTarget");
    private static readonly int s_Camera_ColorTarget = Shader.PropertyToID("Wiwiw_Camera_ColorTarget");
    private static readonly MaterialPropertyBlock s_MaterialPropertyBlock = new();
    private static readonly ShaderTagId s_ShadowCasterPassTag = new("ShadowCasterPass");
    private static readonly ShaderTagId s_OpaquePassTag = new("OpaquePass");
    private readonly PsxSrpAsset m_Asset;
    private Camera m_ShadowCamera;


    public PsxSrp(PsxSrpAsset asset)
    {
        m_Asset = asset;
    }


    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_ShadowCamera);
        m_ShadowCamera = null;

        base.Dispose(disposing);
    }


    protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
    {
        base.Render(context, cameras);
        BeginContextRendering(context, cameras);
        BeginSample(context, "Psx SRP");

        BindShaderGlobals(context);

        foreach (var component in Object.FindObjectsByType<SkyOcclusionDynamicObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (component != null)
                UpdateSkyOcclusionDynamicObject(component);
        }

        foreach (var component in Object.FindObjectsByType<SkyOcclusionComponent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (component != null)
            {
                UpdateSkyOcclusionComponent(component);
                RenderSkyOcclusionComponent(context, component);
            }
        }

        foreach (var component in Object.FindObjectsByType<SkyOcclusionGlobalBinder>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (component != null)
                UpdateSkyOcclusionGlobalBinder(context, component);
        }

        if (cameras != null)
        {
            foreach (var camera in cameras)
            {
                if (camera != null)
                {
                    RenderCamera(context, camera);
                    RenderGizmos(context, camera);
                }
            }
        }

        EndSample(context, "Psx SRP");
        EndContextRendering(context, cameras);
        context.Submit();
    }


    private void BindShaderGlobals(ScriptableRenderContext context)
    {
        var commandBuffer = CommandBufferPool.Get("Bind Shader Globals");
        commandBuffer.BeginSample("Bind Shader Globals");

        { // ------------------------------------------------------------------
            var ditherScale = 1f;

            if (m_Asset != null && m_Asset.RenderingHeight > 0 && m_Asset.EmulationHeight > 0)
                ditherScale = m_Asset.EmulationHeight * math.rcp(m_Asset.RenderingHeight);

            commandBuffer.SetGlobalFloat(s_DitherScale, ditherScale);
        } // ------------------------------------------------------------------

        commandBuffer.EndSample("Bind Shader Globals");
        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void UpdateSkyOcclusionDynamicObject(SkyOcclusionDynamicObject component)
    {
        if (component == null || !component.TryGetComponent<Renderer>(out var renderer))
            return;

        renderer.GetPropertyBlock(s_MaterialPropertyBlock);
        var arr = s_MaterialPropertyBlock.GetMatrixArray(s_SkyOcclusion_ObjectToWorldMatrixArray);

        if (arr != null && arr.Length == PsxSrpConst.SkyOcclusion.MaxSliceCount)
        {
            var gridSize = m_Asset != null ? m_Asset.SkyOcclusionGridSize : PsxSrpConst.SkyOcclusion.DefaultGridSize;
            gridSize = math.clamp(gridSize, 1u, PsxSrpConst.SkyOcclusion.MaxGridSize);
            var sliceCount = gridSize * gridSize;
            sliceCount = math.clamp(sliceCount, 1u, PsxSrpConst.SkyOcclusion.MaxSliceCount);
            var sliceIndex = (uint)Time.frameCount % sliceCount;

            arr[sliceIndex] = component.transform.localToWorldMatrix;
        }
        else
        {
            arr = new Matrix4x4[PsxSrpConst.SkyOcclusion.MaxSliceCount];
            for (var i = 0u; i < arr.Length; i++)
                arr[i] = component.transform.localToWorldMatrix;
        }

        s_MaterialPropertyBlock.SetMatrixArray(s_SkyOcclusion_ObjectToWorldMatrixArray, arr);
        renderer.SetPropertyBlock(s_MaterialPropertyBlock);
    }


    private void UpdateSkyOcclusionComponent(SkyOcclusionComponent component)
    {
        if (component == null)
            return;

        // --------------------------------------------------------------------

        var resolution = component.Resolution;
        resolution = math.clamp(resolution, 1u, PsxSrpConst.SkyOcclusion.MaxResolution);
        var gridSize = m_Asset != null ? m_Asset.SkyOcclusionGridSize : PsxSrpConst.SkyOcclusion.DefaultGridSize;
        gridSize = math.clamp(gridSize, 1u, PsxSrpConst.SkyOcclusion.MaxGridSize);
        var sliceCount = gridSize * gridSize;
        sliceCount = math.clamp(sliceCount, 1u, PsxSrpConst.SkyOcclusion.MaxSliceCount);

        var shadowMapArrayDescriptor = new RenderTextureDescriptor
        (
            width: (int)resolution,
            height: (int)resolution,
            colorFormat: RenderTextureFormat.Shadowmap,
            depthBufferBits: 16
        )
        {
            dimension = TextureDimension.Tex2DArray,
            volumeDepth = (int)sliceCount,
            useMipMap = false,
            autoGenerateMips = false,
            shadowSamplingMode = ShadowSamplingMode.CompareDepths,
        };

        static bool IsSame(RenderTextureDescriptor a, RenderTextureDescriptor b)
            { return a.width == b.width && a.height == b.height && a.volumeDepth == b.volumeDepth; }

        if (component.ShadowMapArray == null)
        {
            component.ShadowMapArray = new RenderTexture(shadowMapArrayDescriptor)
            {
                name = "Sky Occlusion Shadow Map Array",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            component.ShadowMapArray.Create();
        }

        if (!IsSame(component.ShadowMapArray.descriptor, shadowMapArrayDescriptor))
        {
            component.ShadowMapArray.Release();
            component.ShadowMapArray.descriptor = shadowMapArrayDescriptor;
            component.ShadowMapArray.Create();
        }

        component.WorldToShadowMatrixArray ??= new Matrix4x4[PsxSrpConst.SkyOcclusion.MaxSliceCount];
        component.ShadowDirectionArray ??= new Vector4[PsxSrpConst.SkyOcclusion.MaxSliceCount];

        // --------------------------------------------------------------------

        var sliceIndex = (uint)Time.frameCount % sliceCount;
        var skyAngle = math.clamp(component.SkyAngle, 0f, 180f);
        var radius = math.max(0f, component.Radius);
        var nearClipPlane = math.max(0f, component.NearClipPlane);
        var farClipPlane = math.max(0f, component.FarClipPlane);
        var target = (float3)component.transform.position;
        var direction = MathUtil.ComputeShadowDirection(gridSize, sliceIndex, skyAngle);
        var eye = target + direction * (-nearClipPlane + -farClipPlane * 0.5f);
        var projection = float4x4.Ortho(radius * 2f, radius * 2f, nearClipPlane, farClipPlane);
        var view = math.mul(float4x4.Scale(1f, 1f, -1f), math.inverse(float4x4.LookAt(eye, target, math.forward())));
        var worldToShadow = MathUtil.ComputeWorldToShadowMatrix(view, projection);
        var shadowDirection = MathUtil.ComputeShadowDirection(gridSize, sliceIndex, 180f);

        component.WorldToShadowMatrixArray[sliceIndex] = worldToShadow;
        component.ShadowDirectionArray[sliceIndex] = math.float4(shadowDirection, 0f);
    }


    private void RenderSkyOcclusionComponent(ScriptableRenderContext context, SkyOcclusionComponent skyOcclusionComponent)
    {
        var commandBuffer = CommandBufferPool.Get("Render Sky Occlusion");
        commandBuffer.BeginSample("Render Sky Occlusion");

        if (skyOcclusionComponent != null)
        {
            var gridSize = m_Asset != null ? m_Asset.SkyOcclusionGridSize : PsxSrpConst.SkyOcclusion.DefaultGridSize;
            gridSize = math.clamp(gridSize, 1u, PsxSrpConst.SkyOcclusion.MaxGridSize);
            var sliceCount = gridSize * gridSize;
            sliceCount = math.clamp(sliceCount, 1u, PsxSrpConst.SkyOcclusion.MaxSliceCount);
            var sliceIndex = (uint)Time.frameCount % sliceCount;

            var skyAngle = math.clamp(skyOcclusionComponent.SkyAngle, 0f, 180f);
            var radius = math.max(0f, skyOcclusionComponent.Radius);
            var nearClipPlane = math.max(0f, skyOcclusionComponent.NearClipPlane);
            var farClipPlane = math.max(0f, skyOcclusionComponent.FarClipPlane);
            var target = (float3)skyOcclusionComponent.transform.position;
            var direction = MathUtil.ComputeShadowDirection(gridSize, sliceIndex, skyAngle);
            var eye = target + direction * (-nearClipPlane + -farClipPlane * 0.5f);

            if (m_ShadowCamera == null)
            {
                m_ShadowCamera = new GameObject("Shadow Camera").AddComponent<Camera>();
                m_ShadowCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
                m_ShadowCamera.enabled = false;
                m_ShadowCamera.clearFlags = CameraClearFlags.Depth;
                m_ShadowCamera.orthographic = true;
            }

            m_ShadowCamera.orthographicSize = radius;
            m_ShadowCamera.aspect = 1f;
            m_ShadowCamera.nearClipPlane = nearClipPlane;
            m_ShadowCamera.farClipPlane = farClipPlane;
            m_ShadowCamera.transform.position = eye;
            m_ShadowCamera.transform.rotation = quaternion.LookRotation(direction, math.forward());

            if (m_ShadowCamera.TryGetCullingParameters(out var cullingParameters))
            {
                var cullingResult = context.Cull(ref cullingParameters);
                var rendererListDesc = new RendererListDesc(s_ShadowCasterPassTag, cullingResult, m_ShadowCamera)
                {
                    renderQueueRange = RenderQueueRange.opaque,
                    sortingCriteria = SortingCriteria.CommonOpaque,
                    layerMask = m_ShadowCamera.cullingMask,
                };
                var rendererList = context.CreateRendererList(rendererListDesc);

                commandBuffer.SetupCameraProperties(m_ShadowCamera);
                commandBuffer.SetRenderTarget(skyOcclusionComponent.ShadowMapArray, 0, CubemapFace.Unknown, (int)sliceIndex);
                commandBuffer.ClearRenderTarget(true, false, Color.clear, 1f);
                commandBuffer.SetGlobalDepthBias(1f, 1f);
                commandBuffer.DrawRendererList(rendererList);
                commandBuffer.SetGlobalDepthBias(0f, 0f);
            }
        }

        commandBuffer.EndSample("Render Sky Occlusion");
        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void UpdateSkyOcclusionGlobalBinder(ScriptableRenderContext context, SkyOcclusionGlobalBinder skyOcclusionGlobalBinder)
    {
        var commandBuffer = CommandBufferPool.Get("Bind Sky Occlusion Shader Globals");
        commandBuffer.BeginSample("Bind Shader Globals");

        if (skyOcclusionGlobalBinder != null)
        {
            if (skyOcclusionGlobalBinder.TryGetComponent(out SkyOcclusionComponent skyOcclusionComponent))
            {
                if (skyOcclusionComponent.ShadowMapArray != null)
                    commandBuffer.SetGlobalTexture(s_SkyOcclusion_ShadowMapArray, skyOcclusionComponent.ShadowMapArray);
                if (skyOcclusionComponent.WorldToShadowMatrixArray != null)
                    commandBuffer.SetGlobalMatrixArray(s_SkyOcclusion_WorldToShadowMatrixArray, skyOcclusionComponent.WorldToShadowMatrixArray);
                if (skyOcclusionComponent.ShadowDirectionArray != null)
                    commandBuffer.SetGlobalVectorArray(s_SkyOcclusion_ShadowDirectionArray, skyOcclusionComponent.ShadowDirectionArray);
            }

            var gridSize = m_Asset != null ? m_Asset.SkyOcclusionGridSize : PsxSrpConst.SkyOcclusion.DefaultGridSize;
            gridSize = math.clamp(gridSize, 1u, PsxSrpConst.SkyOcclusion.MaxGridSize);
            var sliceCount = gridSize * gridSize;
            sliceCount = math.clamp(sliceCount, 1u, PsxSrpConst.SkyOcclusion.MaxSliceCount);
            var frameIndex = (uint)Time.frameCount;

            commandBuffer.SetGlobalInt(s_SkyOcclusion_SliceCount, (int)sliceCount);
            commandBuffer.SetGlobalInt(s_SkyOcclusion_FrameIndex, (int)frameIndex);
        }

        commandBuffer.EndSample("Bind Sky Occlusion Shader Globals");
        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void RenderCamera(ScriptableRenderContext context, Camera camera)
    {
        var commandBuffer = CommandBufferPool.Get("Render Camera");
        commandBuffer.BeginSample("Render Camera");

        if (camera != null && camera.TryGetCullingParameters(out var cullingParameters))
        {
            BeginCameraRendering(context, camera);
            var cullingResult = context.Cull(ref cullingParameters);

            var renderingHeight = m_Asset != null ? m_Asset.RenderingHeight : PsxSrpConst.Camera.DefaultRenderingResolution.y;
            renderingHeight = math.clamp(renderingHeight, 1u, PsxSrpConst.Camera.MaxRenderingHeight);
            var msaaSampleCount = m_Asset != null ? (uint)m_Asset.MsaaSampleCount : 1u;
            msaaSampleCount = math.clamp(msaaSampleCount, 1u, PsxSrpConst.Camera.MaxMsaaSampleCount);
            var colorTargetResolution = ScaleResolution(math.uint2((uint)camera.pixelWidth, (uint)camera.pixelHeight), renderingHeight);
            colorTargetResolution = !math.any(colorTargetResolution <= 0) ? colorTargetResolution : PsxSrpConst.Camera.DefaultRenderingResolution;

            var colorMsaaTargetDescriptor = new RenderTextureDescriptor
            (
                width: (int)colorTargetResolution.x,
                height: (int)colorTargetResolution.y,
                colorFormat: GraphicsFormat.R5G5B5A1_UNormPack16,
                depthStencilFormat: GraphicsFormat.D16_UNorm
            )
            {
                msaaSamples = (int)msaaSampleCount,
            };
            colorMsaaTargetDescriptor.msaaSamples = SystemInfo.GetRenderTextureSupportedMSAASampleCount(colorMsaaTargetDescriptor);

            var colorTargetDescriptor = new RenderTextureDescriptor
            (
                width: (int)colorTargetResolution.x,
                height: (int)colorTargetResolution.y,
                colorFormat: GraphicsFormat.R5G5B5A1_UNormPack16,
                depthStencilFormat: GraphicsFormat.None
            );

            var opaqueRendererListDesc = new RendererListDesc(s_OpaquePassTag, cullingResult, camera)
            {
                renderQueueRange = RenderQueueRange.opaque,
                sortingCriteria = SortingCriteria.CommonOpaque,
                layerMask = camera.cullingMask,
                rendererConfiguration = PerObjectData.LightProbe | PerObjectData.ReflectionProbes,
            };

            var opaqueRendererList = context.CreateRendererList(opaqueRendererListDesc);
            var skyboxRendererList = context.CreateSkyboxRendererList(camera);

            commandBuffer.BeginSample("Opaque Pass");
            commandBuffer.SetupCameraProperties(camera);
            commandBuffer.GetTemporaryRT(s_Camera_ColorMsaaTarget, colorMsaaTargetDescriptor, FilterMode.Bilinear);
            commandBuffer.SetRenderTarget(s_Camera_ColorMsaaTarget);
            if (camera.clearFlags == CameraClearFlags.Skybox)
                commandBuffer.ClearRenderTarget(true, false, Color.clear, 1.0f);
            if (camera.clearFlags == CameraClearFlags.SolidColor)
                commandBuffer.ClearRenderTarget(true, true, camera.backgroundColor, 1.0f);
            if (camera.clearFlags == CameraClearFlags.Depth)
                commandBuffer.ClearRenderTarget(true, false, Color.clear, 1.0f);
            commandBuffer.DrawRendererList(opaqueRendererList);
            if (camera.clearFlags == CameraClearFlags.Skybox)
                commandBuffer.DrawRendererList(skyboxRendererList);
            commandBuffer.EndSample("Opaque Pass");

            commandBuffer.BeginSample("MSAA Resolve");
            commandBuffer.GetTemporaryRT(s_Camera_ColorTarget, colorTargetDescriptor, FilterMode.Bilinear);
            commandBuffer.Blit(s_Camera_ColorMsaaTarget, s_Camera_ColorTarget);
            commandBuffer.ReleaseTemporaryRT(s_Camera_ColorMsaaTarget);
            commandBuffer.EndSample("MSAA Resolve");

            commandBuffer.BeginSample("Present Pass");
            commandBuffer.Blit(s_Camera_ColorTarget, BuiltinRenderTextureType.CameraTarget);
            commandBuffer.ReleaseTemporaryRT(s_Camera_ColorTarget);
            commandBuffer.EndSample("Present Pass");

            EndCameraRendering(context, camera);
        }

        commandBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(commandBuffer);

        CommandBufferPool.Release(commandBuffer);
    }


    private void RenderGizmos(ScriptableRenderContext context, Camera camera)
    {
        BeginSample(context, "Render Gizmos");

        if (camera != null)
        {
            context.SetupCameraProperties(camera);
            context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        }

        EndSample(context, "Render Gizmos");
    }


    private static void BeginSample(ScriptableRenderContext context, string name)
    {
        var commandBuffer = CommandBufferPool.Get(name);
        commandBuffer.BeginSample(name);
        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    private static void EndSample(ScriptableRenderContext context, string name)
    {
        var commandBuffer = CommandBufferPool.Get(name);
        commandBuffer.EndSample(name);
        context.ExecuteCommandBuffer(commandBuffer);
        CommandBufferPool.Release(commandBuffer);
    }


    private static uint2 ScaleResolution(uint2 currentResolution, uint desiredHeight)
    {
        if (math.any(currentResolution <= 0) || desiredHeight <= 0)
            return 0;

        var aspect = currentResolution.x * math.rcp(currentResolution.y);

        if (aspect >= 1f)
            return math.uint2((uint)math.ceil(desiredHeight * aspect), desiredHeight);
        else
            return math.uint2(desiredHeight, (uint)math.ceil(desiredHeight * math.rcp(aspect)));
    }
}
