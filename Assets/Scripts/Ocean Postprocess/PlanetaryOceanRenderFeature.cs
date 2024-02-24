using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanetaryOceanRenderFeature : ScriptableRendererFeature
{
    public Material material;

    private OceanPass oceanPass;

    public override void Create()
    {
        if(oceanPass == null)
            oceanPass = new OceanPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {   
        if(material == null)
            return;

        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(oceanPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        oceanPass.SetTargets(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        oceanPass.Dispose();
    }

    class OceanPass : ScriptableRenderPass
    {
        private Material mat;
        RTHandle src, temp;
        public OceanPass(Material _mat)
        {
            mat = _mat;
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            this.ConfigureInput(ScriptableRenderPassInput.Color);
            this.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public void SetTargets(RTHandle color)
        {
            src = color;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            /*RenderTextureDescriptor tempDesc = renderingData.cameraData.cameraTargetDescriptor;
            tempDesc.depthBufferBits = 0;
            tempDesc.colorFormat = RenderTextureFormat.ARGB32;

            RenderingUtils.ReAllocateIfNeeded(ref temp, tempDesc, name:"_TemporaryColorTexture");*/
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            mat.SetTexture("_MainTex", src);

            Blitter.BlitCameraTexture(cmd, src, temp, mat, 0);
            Blitter.BlitCameraTexture(cmd, temp, src);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }

        public void Dispose()
        {
            temp?.Release();
        }
    }
}