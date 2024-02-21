using System;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable, VolumeComponentMenuForRenderPipeline
    ("Planetary/Ocean", typeof(UniversalRenderPipeline))]
public class PlanetaryOceanMenuItem : VolumeComponent, IPostProcessComponent
{
    //public FloatParameter oceanLevel = new FloatParameter(1);
    /*public ColorParameter shallowColor = new ColorParameter(Color.blue);
    public ColorParameter deepColor = new ColorParameter(Color.black);*/
    public bool IsActive() => true;
    public bool IsTileCompatible() => true;
}