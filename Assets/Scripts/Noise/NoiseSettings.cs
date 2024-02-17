using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct NoiseSettings
{
    public enum NoiseType { NORMAL, RIGID, OCEAN};
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

    [Header("Rigid settings")]
    public float exponent;
    public float smoothing;

    [Header("Mask")]
    public bool useMask;
    public float maskBaseRoughness;
    public float maskRoughness;
    [Range(1,3)]
    public int maskNumLayers;
    public float maskPersistance;
    [Range(-1, 0)]
    public float maskClampMin;
    [Range(0, 1)]
    public float maskClampMax;
}