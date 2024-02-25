using System;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlanetaryOceanRenderFeature : ScriptableRendererFeature
{
    public Shader oceanShader;
    private Material[] materials;
    private Planet[] planets;
    private OceanPass oceanPass;
    [Range(0,1)]
    public float ambientLigting = .1f;

    public override void Create()
    {
        if(oceanShader == null)
            return;

        planets = GameObject.FindObjectsOfType<Planet>();
        materials = new Material[planets.Length];
        for(int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(oceanShader);
        }

        if(oceanPass == null)
            oceanPass = new OceanPass(materials, planets);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {   
        if(oceanShader == null)
            return;

        if(oceanPass == null)
        {
            Create();
            return;
        }

        if(renderingData.cameraData.cameraType == CameraType.Game)
            renderer.EnqueuePass(oceanPass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if(oceanPass == null)
            return;

        oceanPass.Setup(renderer.cameraColorTargetHandle, ambientLigting);
    }

    protected override void Dispose(bool disposing)
    {
        oceanPass = null;
    }

    class OceanPass : ScriptableRenderPass
    {
        private Material[] mats;
        RenderTargetIdentifier src;
        private Transform camTransform;
        private Planet[] planets;
        private Vector3 dirToSun;
        private float ambientLigting;
        public OceanPass(Material[] _mats, Planet[] _plantes)
        {
            mats = _mats;
            this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            planets = _plantes;

            UpdateDirToSun();
        }

        public void Setup(RTHandle color, float _ambientLighting)
        {
            src = color;
            ambientLigting = _ambientLighting;
            planets = GameObject.FindObjectsOfType<Planet>();
        }

        public void UpdateDirToSun()
        {
            dirToSun = -RenderSettings.sun.transform.forward;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            foreach(Material mat in mats)
            {
                mat.SetVector("dirToSun", dirToSun);
                mat.SetFloat("ambientLigting", ambientLigting);
            }

            if(planets != null || planets.Length != 0)
            {
                for(int i = 0; i < planets.Length; i++)
                {
                    Planet p = planets[i];
                    if(!p.hasOcean)
                        continue;
                        
                    Material mat = mats[i];

                    mat.SetFloat("radius", p.oceanRadius);
                    mat.SetVector("center", p.transform.position);
                    mat.SetColor("deepColor", p.deepColor);
                    mat.SetColor("shallowColor", p.shallowColor);
                    mat.SetFloat("depthMultiplier", p.depthMultiplier);
                    mat.SetFloat("alphaMultiplier", p.alphaMultiplier);
                    mat.SetFloat("smoothness", p.smoothness);

                    Blit(cmd, src, src, mat, 0);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        /*public void Dispose()
        {

        }*/
    }
}