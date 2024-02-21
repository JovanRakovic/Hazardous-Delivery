using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanetaryOceanRenderFeature : ScriptableRendererFeature
{
    private OceanPass oceanPass;

    public override void Create()
    {
        oceanPass = new OceanPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(oceanPass);
    }

    class OceanPass : ScriptableRenderPass
    {
        private Material mat;
        int oceanId = Shader.PropertyToID("_Temp");
        RTHandle src, oceanHandle;
        public OceanPass()
        {
            if(!mat)
                mat = CoreUtils.CreateEngineMaterial("CustomPost/test");
            RTHandles.Initialize(Screen.width, Screen.height);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RTHandles.SetReferenceSize(Screen.width, Screen.height);
            src = renderingData.cameraData.renderer.cameraColorTargetHandle;

            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            cmd.GetTemporaryRT(oceanId, desc, FilterMode.Bilinear);

            oceanHandle = RTHandles.Alloc(new Vector2(1f, 1f));
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer commandBuffer = CommandBufferPool.Get("PlanetaryOceanRenderFeature");
            VolumeStack volumes = VolumeManager.instance.stack;
            PlanetaryOceanMenuItem oceanData = volumes.GetComponent<PlanetaryOceanMenuItem>();

            if(oceanData.IsActive())
            {
                mat.SetTexture("_MainTex", src);

                Blit(commandBuffer, src, oceanHandle, mat, 0);
                Blit(commandBuffer, oceanHandle, src);
            }

            context.ExecuteCommandBuffer(commandBuffer);
            CommandBufferPool.Release(commandBuffer);
        } 
        
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(oceanId);
        }
    }
}