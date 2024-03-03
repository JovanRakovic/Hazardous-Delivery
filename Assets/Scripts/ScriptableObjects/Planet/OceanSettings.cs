using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable, CreateAssetMenu(menuName = "Planet Settings/Ocean Settings")]
public class OceanSettings : OceanSettingsMaster 
{
    public float oceanRadius = 1f;
    public Color shallowColor = Color.white;
    public Color deepColor = Color.black;
    [Range(0,.2f)]
    public float depthMultiplier = 1f;
    [Range(0,1)]
    public float alphaMultiplier = 1f;
    [Range(0,1)]
    public float smoothness = 1f;

    [Header("Wave Normals")]
    public Texture2D normalA;
    public Texture2D normalB;
    [Range(0,1)]
    public float waveStrength = 1;
    public float waveNormalScale = 1;
    public float waveSpeed = 1;
}

[System.Serializable, CreateAssetMenu(menuName = "Planet Settings/Ocean Settings Master")]
public class OceanSettingsMaster : ScriptableObject
{
    
}