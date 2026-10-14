// Draws spectral objects only where the viewfinder's stencil mask is, faded in by how far the camera is raised.
// The Scene view shows them everywhere.

using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SpectralRenderFeature : ScriptableRendererFeature
{
    const int StencilReference = 1;

    [SerializeField] LayerMask spectralLayers;
    [SerializeField] LayerMask viewfinderLayers;

    SpectralPass opaquePass;
    SpectralPass viewfinderPass;

    public override void Create()
    {
        opaquePass = new SpectralPass(this, true) { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
        viewfinderPass = new SpectralPass(this, false) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        CameraType type = renderingData.cameraData.cameraType;
        if (type != CameraType.Game && type != CameraType.SceneView) return;
        if (opaquePass == null || viewfinderPass == null) Create();
        renderer.EnqueuePass(opaquePass);
        renderer.EnqueuePass(viewfinderPass);
    }

    class SpectralPass : ScriptableRenderPass
    {
        static readonly List<ShaderTagId> ShaderTags = new()
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        static readonly int RevealId = Shader.PropertyToID("_SpectralReveal");
        static readonly int StencilCompId = Shader.PropertyToID("_SpectralStencilComp");

        readonly SpectralRenderFeature feature;
        readonly bool opaque;
        readonly string displayName;
        readonly ProfilingSampler sampler;

        class PassData
        {
            public RendererListHandle first;
            public RendererListHandle second;
            public float reveal;
            public CompareFunction stencilComp;
        }

        public SpectralPass(SpectralRenderFeature feature, bool opaque)
        {
            this.feature = feature;
            this.opaque = opaque;
            displayName = opaque ? "Spectral Opaques" : "Viewfinder";
            sampler = new ProfilingSampler(displayName);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (!resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid()) return;

            bool showAll = cameraData.cameraType == CameraType.SceneView || cameraData.cameraType == CameraType.Preview;

            using IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(displayName, out PassData data, sampler);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
            if (resourceData.mainShadowsTexture.IsValid()) builder.UseTexture(resourceData.mainShadowsTexture);
            if (resourceData.additionalShadowsTexture.IsValid()) builder.UseTexture(resourceData.additionalShadowsTexture);

            SortingCriteria sorting = opaque ? cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
            DrawingSettings drawing = RenderingUtils.CreateDrawingSettings(ShaderTags, renderingData, cameraData, lightData, sorting);
            RenderQueueRange queue = opaque ? RenderQueueRange.opaque : RenderQueueRange.transparent;

            data.stencilComp = showAll ? CompareFunction.Always : CompareFunction.Equal;
            data.reveal = showAll ? 1f : SpectralCamera.Reveal;
            data.first = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, new FilteringSettings(queue, feature.viewfinderLayers)));
            data.second = opaque
                ? CreateStencilTestedList(renderGraph, renderingData.cullResults, drawing, new FilteringSettings(queue, feature.spectralLayers), data.stencilComp)
                : default;

            builder.UseRendererList(data.first);
            if (data.second.IsValid()) builder.UseRendererList(data.second);
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
            {
                context.cmd.SetGlobalFloat(RevealId, d.reveal);
                context.cmd.SetGlobalFloat(StencilCompId, (float)d.stencilComp);
                context.cmd.DrawRendererList(d.first);
                if (d.second.IsValid()) context.cmd.DrawRendererList(d.second);
            });
        }

        static RendererListHandle CreateStencilTestedList(RenderGraph renderGraph, CullingResults cull, DrawingSettings drawing, FilteringSettings filtering, CompareFunction comparison)
        {
            RenderStateBlock stencilBlock = new(RenderStateMask.Stencil)
            {
                stencilReference = StencilReference,
                stencilState = new StencilState(true, 255, 0, comparison)
            };

            using var tagValues = new NativeArray<ShaderTagId>(new[] { ShaderTagId.none }, Allocator.Temp);
            using var stateBlocks = new NativeArray<RenderStateBlock>(new[] { stencilBlock }, Allocator.Temp);

            RendererListParams parameters = new(cull, drawing, filtering)
            {
                tagValues = tagValues,
                stateBlocks = stateBlocks,
                isPassTagName = false
            };
            return renderGraph.CreateRendererList(parameters);
        }
    }
}
