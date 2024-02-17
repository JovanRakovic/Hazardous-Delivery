using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Planet : MonoBehaviour
{
    public static List<Planet> planetList;
    [HideInInspector]
    public int planetIndex = -1;

    [SerializeField]
    private GameObject instance;
    [Header("General Settings")]
    [SerializeField]
    public float radius = 1000f;
    [SerializeField]
    public float distance;
    [SerializeField]
    public float distanceDivideFactor = 2f;
    [SerializeField]
    public int maxTreeDepth = 4;
    [Range(2, 255), SerializeField]
    public int res = 25;

    [Header("Noise Settings")]
    public NoiseSettings[] noiseSettings;

    private PlanetChunk[] rootChunks = new PlanetChunk[6];

#if UNITY_EDITOR
    [SerializeField]
    private bool regenerate = false;
#endif

    void Awake()
    {
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
        foreach (PlanetChunk c in rootChunks) 
        {
            c.RequestRegenerate();
        }
    }

#if UNITY_EDITOR

    private void OnValidate()
    {
        if (regenerate)
        {
            RegenerateChunks();
            regenerate = false;
        }
    }

#endif
}