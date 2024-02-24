using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanetaryOceanRenderFeature : ScriptableRendererFeature
{
    public Material material;

    private OceanPass oceanPass;

    public override void Create()
    {
        oceanPass = new OceanPass(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {   
        if (renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(oceanPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        oceanPass.SetTargets(renderer.cameraColorTargetHandle);
    }

    /*protected override void Dispose(bool disposing)
    {

    }*/

    class OceanPass : ScriptableRenderPass
    {
        private Material mat;
        RTHandle src;
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
            //ConfigureTarget(src);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            mat.SetTexture("_MainTex", src);

            Blit(cmd, src, src, mat, 0);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }
}