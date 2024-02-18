using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct NoiseSettings
{
    public enum NoiseType { NORMAL, RIGID};
    public NoiseType type;

    public bool enabled;

    [Header("Normal Settings")]
    public float strength;
    public float baseRoughness;
    public float roughness;
    [Range(1,16)]
    public int numLayers;
    public float persistance;
    public float3 center;
    [Range(-1, 0)]
    public float clampMin;
    [Range(0, 1)]
    public float clampMax;
    public float clampSmoothing;

    [Header("Rigid settings")]
    public float exponent;
    public float rigidSmoothing;

    [Header("Mask")]
    public bool useMask;
    public float maskBaseRoughness;
    public float maskRoughness;
    
    public float3 maskCenter;
    [Range(1,3)]
    public int maskNumLayers;
    public float maskPersistance;
    public float maskExponent;
}