using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Planet : MonoBehaviour
{
    public static List<Planet> planetList;
    [HideInInspector]
    public int planetIndex = -1;

    [SerializeField]
    private GameObject instance;
    [Header("General Settings")]
    public GeneralSettings generalSettings;

    [Header("Ocean Settings")]
    public bool hasOcean = false;
    public OceanSettings oceanSettings;

    private Vector2 minMax;

    [Header("Noise Settings")]
    public NoiseSettings[] noiseSettings;

    private PlanetChunk[] rootChunks = new PlanetChunk[6];

#if UNITY_EDITOR
    [SerializeField]
    private bool regenerate = false;
#endif

    void Awake()
    {
        minMax = new Vector2(float.MaxValue, float.MinValue);

        if (planetList == null)
            planetList = new List<Planet>();
        planetList.Add(this);

        UpdateIndex();

        for (int i = 0; i < 6; i++)
        {
            GameObject chunk = GameObject.Instantiate(instance);
            chunk.transform.parent = transform;
            chunk.transform.position = transform.position;
            chunk.transform.rotation = transform.rotation;

            rootChunks[i] = new PlanetChunk(this, i, chunk.transform, 0, Vector2.zero);
        }
    }

    public void UpdateMinMax(Vector2 _minMax)
    {
        bool change = false;
        if(minMax.x > _minMax.x)
        {
            minMax.x = _minMax.x;
            change = true;
        }
        if(minMax.y < _minMax.y)
        {
            minMax.y = _minMax.y;
            change = true;
        }

        if(change)
            generalSettings.mat.SetVector("_HeightMinMax", minMax);
    }

    public void UpdateIndex()
    {
        planetIndex = planetList.IndexOf(this);
    }

    private void OnDestroy()
    {
        planetList.Remove(this);
        foreach (Planet p in planetList)
        {
            p.UpdateIndex();
        }
    }

    private void RegenerateChunks()
    {
        minMax = new Vector2(float.MaxValue, float.MinValue);
        foreach (PlanetChunk c in rootChunks) 
        {
            c.RequestRegenerate();
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (regenerate)
        {
            RegenerateChunks();
            regenerate = false;
        }
    }

#endif
}