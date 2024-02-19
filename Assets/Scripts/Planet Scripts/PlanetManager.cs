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

    public struct PlanetChunkData 
    {
        public int planetIndex;
        public int resolution;
        public float radius;
        public int facingDir;
        public int vertCount;
        public int triCount;
        public int vertOffset;
        public int triOffset;
        public int borderVertOffset;
        public float2 center;
        public int currentTreeDepth;
        public float2 minMaxTerrainHeight;
    }

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
        List<PlanetChunk> newChunks = new List<PlanetChunk>();
        foreach (PlanetChunk chunk in chunks) 
        {
            Planet planet = chunk.planet;
            float dis = planet.distance / Mathf.Pow(planet.distanceDivideFactor, chunk.currentTreeDepth);
            if (chunk.hasChildern)
                continue;

            if (chunk.transform == null)
                continue;

            float distance = Vector3.Distance(chunk.transform.position + chunk.boundCenter, camTrans.position);
            if (distance > dis)
            {
                if (chunk.hasParent)
                {
                    float parentDistance = Vector3.Distance(chunk.transform.position + chunk.parent.boundCenter, camTrans.position);
                    if (parentDistance > dis * planet.distanceDivideFactor)
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

            if (planet.maxTreeDepth <= chunk.currentTreeDepth)
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
                newChunks.Add(children[i]);
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
            int res = chunk.planet.res;
            if (chunk.hasMesh)
                continue;

            chunksToGenerate.Add(chunk);
            
            int tempVert = (res + 1) * (res + 1);
            int tempTri = res * res * 6;

            PlanetChunkData data = new PlanetChunkData()
            {
                planetIndex = chunk.planet.planetIndex,
                resolution = res,
                radius = chunk.planet.radius,
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
    }

    [BurstCompile]
    public struct GeneratePlanetChunks : IJobFor
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> vertices;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> borderVerts;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> normals;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float4> colors;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float2> heights;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> triangles;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> noiseRange;
        [NativeDisableContainerSafetyRestriction] public NativeArray<NoiseSettings> noiseSettingsData;
        public NativeArray<PlanetChunkData> chunkDataList;

        public void Execute(int i) 
        {
            int res = chunkDataList[i].resolution;
            float radius = chunkDataList[i].radius;
            int facingDir = chunkDataList[i].facingDir;
            int vertOffset = chunkDataList[i].vertOffset;
            int triOffset = chunkDataList[i].triOffset;
            float2 center = chunkDataList[i].center;
            int borderOffset = chunkDataList[i].borderVertOffset;

            float scale = 1;
            for (int j = 0; j < chunkDataList[i].currentTreeDepth; j++) 
            {
                scale /= 2f;
            }

            int counter = 0;
            int borderCounter = 0;

            float2 minMax = new float2(float.MaxValue, float.MinValue);

            for (float x = 0; x <= res + 2; x++) 
            {
                for (float y = 0; y <= res + 2; y++) 
                {
                    float xPos;
                    float yPos;
                    float zPos;

                    float tempPosX = 2 * scale * ((x - 1) / res - .5f) + center.x;
                    float tempPosY = 2 * scale * ((y - 1) / res - .5f) + center.y;

                    switch (facingDir) 
                    {
                        case 0:
                            xPos = 1;
                            yPos = tempPosY;
                            zPos = tempPosX;
                            break;
                        case 1:
                            xPos = -1;
                            yPos = tempPosX;
                            zPos = tempPosY;
                            break;
                        case 2:
                            xPos = tempPosX;
                            yPos = 1;
                            zPos = tempPosY;
                            break;
                        case 3:
                            xPos = tempPosY;
                            yPos = -1;
                            zPos = tempPosX;
                            break;
                        case 4:
                            xPos = tempPosY;
                            yPos = tempPosX;
                            zPos = 1;
                            break;
                        default:
                            xPos = tempPosX;
                            yPos = tempPosY;
                            zPos = -1;
                            break;
                    }

                    float3 vec = math.normalize(new float3(xPos, yPos, zPos));

                    float height = 0f;

                    for (int j = noiseRange[chunkDataList[i].planetIndex * 2]; j < noiseRange[chunkDataList[i].planetIndex * 2 + 1]; j++) 
                    {
                        height += GetNoise(noiseSettingsData[j], vec);
                    }

                    vec = vec * (radius / 2) + (vec * height);
                    if (x == 0 || x == res + 2 || y == 0 || y == res + 2) 
                    {
                        borderVerts[borderCounter + borderOffset] = vec;
                        borderCounter++;
                        continue;
                    }

                    if(height > minMax.y)
                        minMax.y = height;
                    else if(height < minMax.x)
                        minMax.x = height;

                    colors[vertOffset + counter] = new float4(height,0,0,0);
                    vertices[vertOffset + counter] = vec;
                    counter++;
                }
            }

            heights[i] = minMax;

            int resP = res + 1;
            for (int j = 0; j < res + 2; j++)
            {
                for (int k = 0; k < res + 2; k++)
                {
                    bool isRealVert1 = false, isRealVert2 = false, isRealVert3 = false, isRealVert4 = false;

                    float3 vert0 = FindVert(i, j, k, ref isRealVert1);
                    float3 vert1 = FindVert(i, j, k + 1, ref isRealVert2);
                    float3 vert2 = FindVert(i, j + 1, k, ref isRealVert3);
                    float3 vert3 = FindVert(i, j + 1, k + 1, ref isRealVert4);

                    float3 normalA = math.normalize(math.cross(vert3 - vert1, vert0 - vert1));
                    float3 normalB = math.normalize(math.cross(vert0 - vert2, vert3 - vert2));

                    if (isRealVert1)
                        normals[vertOffset + (j - 1) * resP + (k - 1)] += normalA + normalB;
                    if (isRealVert2)
                        normals[vertOffset + (j - 1) * resP + k] += normalA;
                    if (isRealVert3)
                        normals[vertOffset + j * resP + (k - 1)] += normalB;
                    if (isRealVert4)
                        normals[vertOffset + j * resP + k] += normalA + normalB;
                }
            }

            int vert = 0;
            int tris = 0;
            for (int j = 0; j < res; j++) 
            {
                for (int k = 0; k < res; k++)
                {
                    triangles[triOffset + tris] = vert + 1;
                    triangles[triOffset + tris + 1] = vert + res + 1;
                    triangles[triOffset + tris + 2] = vert;
                    triangles[triOffset + tris + 3] = vert + res + 2;
                    triangles[triOffset + tris + 4] = vert + res + 1;
                    triangles[triOffset + tris + 5] = vert + 1;

                    vert++;
                    tris += 6;
                }
                vert++;
            }
        }
        private float GetNoise(NoiseSettings settings, float3 point)
        {
            if(!settings.enabled)
                return 0;

            switch ((int)settings.type)
            {
                case 0:
                    return NormalNoise(point, settings);

                case 1:
                    return RigidNoise(point, settings);

                default:
                    return NormalNoise(point, settings);
            }
        }

        private float CalculateMask(float3 point, NoiseSettings settings)
        {
            if(!settings.useMask)
                return 1;

            float maskValue = 0;
            float amplitude = 1;
            float f = settings.maskBaseRoughness;

            for (int i = 0; i < settings.maskNumLayers; i++)
            {
                float v = noise.cnoise(point * f + settings.maskCenter);
                maskValue += v * amplitude;
                
                f *= settings.maskRoughness;
                amplitude *= settings.maskPersistance;
            }

            maskValue = math.pow(math.clamp(maskValue/2 + .5f, 0, 1), settings.maskExponent);
            return maskValue;
        }

        private float NormalNoise(float3 point, NoiseSettings settings)
        {
            float maskValue = CalculateMask(point, settings);

            if(maskValue == 0)
                return 0;

            float noiseValue = 0;
            float amplitude = 1;
            float f = settings.baseRoughness;

            for (int i = 0; i < settings.numLayers; i++)
            {
                float v = noise.cnoise(point * f + settings.center);
                noiseValue += v * amplitude;
                f *= settings.roughness;
                amplitude *= settings.persistance;
            }

            noiseValue = SmoothMin(noiseValue, settings.clampMax, settings.clampSmoothing);
            noiseValue = SmoothMax(noiseValue, settings.clampMin, settings.clampSmoothing);

            return maskValue * noiseValue * settings.strength;
        }

        private float RigidNoise(float3 point, NoiseSettings settings)
        {
            float maskValue = CalculateMask(point, settings);

            if(maskValue == 0)
                return 0;

            float noiseValue = 0;
            float amplitude = 1;
            float f = settings.baseRoughness;

            for (int i = 0; i < settings.numLayers; i++)
            {
                float a = math.pow(math.abs(noise.cnoise(point * f + settings.center) + 1), settings.exponent);
                float b = math.pow(math.abs(noise.cnoise(point * f + settings.center) - 1), settings.exponent);

                noiseValue += SmoothMin(a, b, settings.rigidSmoothing) * amplitude;
                f *= settings.roughness;
                amplitude *= settings.persistance;
            }
            return maskValue * noiseValue * settings.strength;
        }

        private float SmoothMax(float a, float b, float k) 
        {
            return (a + b + math.sqrt(math.pow(a - b, 2) + k)) / 2;
        }

        private float SmoothMin(float a, float b, float k) 
        {
            float h = math.clamp((b - a + k) / (2 * k), 0f, 1f);
            return a * h + b * (1 - h) - k * h * (1 - h);
        }

        private float3 FindVert(int i, int x, int y, ref bool isRealVert) 
        {
            int res = chunkDataList[i].resolution;
            int vertOffset = chunkDataList[i].vertOffset;
            int borderVertOffset = chunkDataList[i].borderVertOffset;
            int resAdd = res + 2;

            if (x > 0 && x < resAdd && y > 0 && y < resAdd)
            {
                isRealVert = true;
                return vertices[vertOffset + (x - 1) * (res + 1) + (y - 1)];
            }
            else if (x == 0)
            {
                return borderVerts[borderVertOffset + y];
            }
            else if (x == resAdd)
            {
                return borderVerts[borderVertOffset + ((resAdd) * 4 - (resAdd + 1)) + y];
            }
            else 
            {
                return borderVerts[borderVertOffset + resAdd + (x - 1) * 2 + ((y > 0) ? 2 : 1)];
            }
        }
    }

    [BurstCompile]
    public struct BakePhysicsMeshes : IJobFor
    {
        public NativeArray<int> meshIDs;

        public void Execute(int i) 
        {
            Physics.BakeMesh(meshIDs[i], false);
        }
    }
}