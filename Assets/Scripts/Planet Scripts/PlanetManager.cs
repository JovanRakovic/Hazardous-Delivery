using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

public class PlanetManager : MonoBehaviour
{
    public Transform chunkInstance;

    private Transform camTrans;

    private List<PlanetChunk> chunksToGenerate;
    private List<PlanetChunk> chunksToDissapear;
    private List<PlanetChunk> childrenToDelete;

    bool coroutineRunning = false;

    int vertCount = 0;
    int borderVertCount = 0;
    int triangleCount = 0;
    private List<PlanetChunkData> chunkDataList = new List<PlanetChunkData>();
    private List<NoiseSettings> noiseSettings = new List<NoiseSettings>();
    private int[] noiseIndices;

    void Start()
    {
        chunksToGenerate = new List<PlanetChunk>();
        chunksToDissapear = new List<PlanetChunk>();
        childrenToDelete = new List<PlanetChunk>();

        camTrans = Camera.main.transform;
    }

    void Update() 
    {
        if (!coroutineRunning)
        {
            CheckDistance(PlanetChunk.chunks);

            GenerateChunks();
        }
    }

    [BurstCompile]
    private void CheckDistance(List<PlanetChunk> chunks) 
    {
        chunks = new List<PlanetChunk>(chunks);
        //List<PlanetChunk> newChunks = new List<PlanetChunk>();
        foreach (PlanetChunk chunk in chunks) 
        {
            GeneralSettings planetSettings = chunk.planet.generalSettings;
            float dis = planetSettings.distance / Mathf.Pow(planetSettings.distanceDivideFactor, chunk.currentTreeDepth);
            if (chunk.hasChildern)
                continue;

            if (chunk.transform == null)
                continue;

            float distance = Vector3.Distance(chunk.transform.TransformPoint(chunk.boundCenter), camTrans.position);
            if (distance > dis)
            {
                if (chunk.hasParent)
                {
                    float parentDistance = Vector3.Distance(chunk.transform.TransformPoint(chunk.parent.boundCenter), camTrans.position);
                    if (parentDistance > dis * planetSettings.distanceDivideFactor)
                    {
                        chunk.RequestDestruction();
                    }
                    else if (chunk.destructionRequested)
                    {
                        chunk.VoidDestructionRequest();
                        continue;
                    }

                    if (chunk.parent.childrenToDestroy == 4)
                    {
                        childrenToDelete.Add(chunk.parent);
                        chunk.parent.RenounceChildren();
                    }
                }
                continue;
            }

            if (planetSettings.maxTreeDepth <= chunk.currentTreeDepth)
                continue;

            PlanetChunk[] children = new PlanetChunk[4];
            
            float offset = 1f;

            for (int j = 0; j < chunk.currentTreeDepth + 1; j++)
            {
                offset /= 2f;
            }

            for (int i = 0; i < 4; i++) 
            {
                Vector2 offsetVector = Vector2.zero;
                switch (i) 
                {
                    case 0:
                        offsetVector = new Vector2(offset, offset);
                        break;
                    case 1:
                        offsetVector = new Vector2(-offset, offset);
                        break;
                    case 2:
                        offsetVector = new Vector2(offset, -offset);
                        break;
                    case 3:
                        offsetVector = new Vector2(-offset, -offset);
                        break;
                }

                Vector2 childCenter = chunk.center + offsetVector;

                Transform t = Instantiate(chunkInstance, chunk.transform);
                children[i] = new PlanetChunk(chunk.planet, chunk.facingDirection ,t, chunk.currentTreeDepth+1, chunk, childCenter);
                children[i].EstimateBound(chunk.boundCenter.y);
                //newChunks.Add(children[i]);
            }

            chunk.AssignChildren(children);
            chunksToDissapear.Add(chunk);
        }
        
        /*if (newChunks.Count > 0)
            CheckDistance(newChunks);*/
    } 
    private void GenerateChunks() 
    {
        noiseIndices = new int[Planet.planetList.Count * 2];
        for (int i = 0; i < noiseIndices.Length; i++) 
            noiseIndices[i] = -1;

        foreach (PlanetChunk chunk in PlanetChunk.visibleChunks)
        {
            int res = chunk.planet.generalSettings.res;
            if (chunk.hasMesh)
                continue;

            chunksToGenerate.Add(chunk);
            
            int tempVert = (res + 1) * (res + 1);
            int tempTri = res * res * 6;

            PlanetChunkData data = new PlanetChunkData()
            {
                planetIndex = chunk.planet.planetIndex,
                resolution = res,
                radius = chunk.planet.generalSettings.radius,
                facingDir = chunk.facingDirection,
                vertCount = tempVert,
                triCount = tempTri,
                vertOffset = vertCount,
                triOffset = triangleCount,
                borderVertOffset = borderVertCount,
                center = chunk.center,
                currentTreeDepth = chunk.currentTreeDepth
            };
            chunkDataList.Add(data);

            vertCount += tempVert;
            triangleCount += tempTri;
            borderVertCount += (res + 3) * 4 - 4;

            if (noiseIndices[data.planetIndex * 2] == -1) 
            {
                noiseIndices[data.planetIndex * 2] = noiseSettings.Count;
                noiseSettings.AddRange(chunk.planet.noiseSettings);
                noiseIndices[data.planetIndex * 2 + 1] = noiseSettings.Count;
            }
        }
        if(chunksToGenerate.Count > 0)
            StartCoroutine(GeneratorCoroutine());
    }

    private void ClearData() 
    {
        vertCount = 0;
        triangleCount = 0;
        borderVertCount = 0;
        chunkDataList.Clear();
        chunksToGenerate.Clear();
        noiseSettings.Clear();
        noiseIndices = null;
    }

    public IEnumerator GeneratorCoroutine() 
    {
        coroutineRunning = true;

        NativeArray<float3> nVertices = new NativeArray<float3>(vertCount, Allocator.Persistent);
        NativeArray<float3> borderVerts = new NativeArray<float3>(borderVertCount, Allocator.Persistent);
        NativeArray<float3> normals = new NativeArray<float3>(vertCount, Allocator.Persistent);
        NativeArray<float4> colors = new NativeArray<float4>(vertCount, Allocator.Persistent);
        NativeArray<float2> minMax = new NativeArray<float2>(chunkDataList.Count, Allocator.Persistent);
        NativeArray<int> nTris = new NativeArray<int>(triangleCount, Allocator.Persistent);
        NativeArray<PlanetChunkData> nChunkData= new NativeArray<PlanetChunkData>(chunkDataList.ToArray(), Allocator.Persistent);
        NativeArray<int> nNoiseRange = new NativeArray<int>(noiseIndices, Allocator.Persistent);
        NativeArray<NoiseSettings> nNoiseSettingsData = new NativeArray<NoiseSettings>(noiseSettings.ToArray(), Allocator.Persistent);
        
        GeneratePlanetChunks job = new GeneratePlanetChunks()
        {
            vertices = nVertices,
            borderVerts = borderVerts,
            normals = normals,
            colors = colors,
            heights = minMax,
            triangles = nTris,
            chunkDataList = nChunkData,
            noiseRange = nNoiseRange,
            noiseSettingsData = nNoiseSettingsData
        };

        JobHandle dependency = new JobHandle();
        JobHandle handle = job.ScheduleParallel(chunksToGenerate.Count, 1, dependency);

        while (!handle.IsCompleted)
            yield return null;
        
        handle.Complete();

        Mesh[] meshes = new Mesh[chunksToGenerate.Count];
        NativeArray<int> meshIDs = new NativeArray<int>(meshes.Length, Allocator.Persistent);
        float2[] minMaxArray = minMax.ToArray();

        int i = 0;
        foreach (PlanetChunkData c in chunkDataList) 
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(nVertices, c.vertOffset, c.vertCount);
            mesh.SetIndices(nTris, c.triOffset, c.triCount, MeshTopology.Triangles, 0);
            mesh.SetNormals(normals, c.vertOffset, c.vertCount);
            mesh.SetColors(colors, c.vertOffset, c.vertCount);

            Planet.planetList[c.planetIndex].UpdateMinMax(minMaxArray[i]);

            meshes[i] = mesh;
            meshIDs[i] = mesh.GetInstanceID();

            i++;
        }

        BakePhysicsMeshes bakeJob = new BakePhysicsMeshes() 
        {
            meshIDs = meshIDs
        };

        handle = bakeJob.ScheduleParallel(meshes.Length, 1, dependency);

        while (!handle.IsCompleted)
            yield return null;

        handle.Complete();

        i = 0;
        foreach (Mesh m in meshes)
        {
            chunksToGenerate[i].SetMesh(m);
            i++;
        }

        nVertices.Dispose();
        borderVerts.Dispose();
        normals.Dispose();
        colors.Dispose();
        minMax.Dispose();
        nTris.Dispose();
        nChunkData.Dispose();
        meshIDs.Dispose();
        nNoiseRange.Dispose();
        nNoiseSettingsData.Dispose();

        ClearData();
        DisableNonVisibleChunks();

        coroutineRunning = false;
    }

    private void DisableNonVisibleChunks() 
    {
        foreach (PlanetChunk parent in childrenToDelete)
        {
            foreach (PlanetChunk child in parent.children)
            {
                child.OnDestroy();
            }
            parent.VoidChildren();
        }
        childrenToDelete.Clear();

        foreach (PlanetChunk chunk in chunksToDissapear) 
        {
            chunk.ClearMesh();
        }
        chunksToDissapear.Clear();
        childrenToDelete.Clear();

        Resources.UnloadUnusedAssets();
    }
}