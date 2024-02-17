using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

public class FlatWorldManager : MonoBehaviour
{
    public Transform chunkInstance;

    private Transform camTrans;

    private List<FlatWorldChunk> chunksToGenerate;
    private List<FlatWorldChunk> chunksToDissapear;
    private List<FlatWorldChunk> childrenToDelete;

    bool coroutineRunning = false;

    int vertCount = 0;
    int borderVertCount = 0;
    int triangleCount = 0;
    List<ChunkData> chunkDataList = new List<ChunkData>();

    [Range(2, 255)][SerializeField]
    private int res = 25;

    public struct ChunkData 
    {
        public int resolution;
        public int vertCount;
        public int triCount;
        public int vertOffset;
        public int triOffset;
        public int borderVertOffset;
        public float2 center;
        public int currentTreeDepth;
        public float2 worldPosition;
    }

    void Start()
    {
        chunksToGenerate = new List<FlatWorldChunk>();
        chunksToDissapear = new List<FlatWorldChunk>();
        childrenToDelete = new List<FlatWorldChunk>();

        camTrans = Camera.main.transform;

        SetupSomeChunks();
    }

    private void SetupSomeChunks() 
    {
        for (int i = -1; i <= 1; i++) 
        {
            for (int j = -1; j <= 1; j++) 
            {
                Transform chunk = Instantiate(chunkInstance);
                chunk.position = new Vector3(i * 5, 0, j * 5);

                FlatWorldChunk worldChunk = new FlatWorldChunk(chunk,res,30,5,0,Vector2.zero);
            }
        }
    }

    void Update() 
    {
        if (!coroutineRunning)
        {
            CheckDistance(FlatWorldChunk.chunks);

            GenerateChunks();
        }
    }

    [BurstCompile]
    private void CheckDistance(List<FlatWorldChunk> chunks) 
    {
        chunks = new List<FlatWorldChunk>(chunks);
        List<FlatWorldChunk> newChunks = new List<FlatWorldChunk>();
        foreach (FlatWorldChunk chunk in chunks) 
        {
            if (chunk.hasChildern)
                continue;

            if (chunk.transform == null)
                continue;

            float distance = Vector3.Distance(chunk.transform.position + chunk.boundCenter, camTrans.position);
            if (distance > chunk.distance)
            {
                if (chunk.hasParent)
                {
                    if (distance >= chunk.distance * 3f)
                    {
                        chunk.RequestDestruction();
                    }
                    else if (chunk.destructionRequested)
                        chunk.VoidDestructionRequest();

                    if (chunk.parent.childrenToDestroy == 4)
                    {
                        childrenToDelete.Add(chunk.parent);
                        chunk.parent.RenounceChildren();
                        //newChunks.Add(chunk);
                    }
                }
                continue;
            }

            if (chunk.maxTreeDepth <= chunk.currentTreeDepth)
                continue;

            FlatWorldChunk[] children = new FlatWorldChunk[4];

            for (int i = 0; i < 4; i++) 
            {
                float offset = 1f;

                for (int j = 0; j < chunk.currentTreeDepth + 1; j++) 
                {
                    offset /= 2f;
                }

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
                children[i] = new FlatWorldChunk(t, chunk.resolution, chunk.distance/2, chunk.maxTreeDepth, chunk.currentTreeDepth+1, chunk, childCenter);
                children[i].EstimateBound(chunk.boundCenter.y);
                newChunks.Add(children[i]);
            }

            chunk.AssignChildren(children);
            chunksToDissapear.Add(chunk);
        }
        if(newChunks.Count > 0)
            CheckDistance(newChunks);
    } 
    private void GenerateChunks() 
    {
        foreach (FlatWorldChunk chunk in FlatWorldChunk.visibleChunks)
        {
            if (chunk.hasMesh)
                continue;

            chunksToGenerate.Add(chunk);
            
            int tempVert = (chunk.resolution + 1) * (chunk.resolution + 1);
            int tempTri = chunk.resolution * chunk.resolution * 6;

            ChunkData data = new ChunkData()
            {
                resolution = chunk.resolution,
                vertCount = tempVert,
                triCount = tempTri,
                vertOffset = vertCount,
                triOffset = triangleCount,
                borderVertOffset = borderVertCount,
                center = chunk.center,
                currentTreeDepth = chunk.currentTreeDepth,
                worldPosition = new Vector2(chunk.transform.position.x, chunk.transform.position.z)
            };
            chunkDataList.Add(data);

            vertCount += tempVert;
            triangleCount += tempTri;
            borderVertCount += (chunk.resolution + 3) * 4 - 4;
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
    }

    public IEnumerator GeneratorCoroutine() 
    {
        coroutineRunning = true;

        NativeArray<float3> nVertices = new NativeArray<float3>(vertCount, Allocator.Persistent);
        NativeArray<float3> borderVerts = new NativeArray<float3>(borderVertCount, Allocator.Persistent);
        NativeArray<float3> normals = new NativeArray<float3>(vertCount, Allocator.Persistent);
        NativeArray<int> nTris = new NativeArray<int>(triangleCount, Allocator.Persistent);
        NativeArray<ChunkData> nChunkData= new NativeArray<ChunkData>(chunkDataList.ToArray(), Allocator.Persistent);

        //Debug.Log(borderVertCount);

        GenerateFlatWorldChunks job = new GenerateFlatWorldChunks()
        {
            vertices = nVertices,
            borderVerts = borderVerts,
            normals = normals,
            triangles = nTris,
            chunkDataList = nChunkData
        };

        JobHandle dependency = new JobHandle();
        JobHandle handle = job.ScheduleParallel(chunksToGenerate.Count, 1, dependency);

        while (!handle.IsCompleted)
            yield return null;
        
        handle.Complete();

        Mesh[] meshes = new Mesh[chunksToGenerate.Count];
        NativeArray<int> meshIDs = new NativeArray<int>(meshes.Length, Allocator.Persistent);

        int i = 0;
        foreach (ChunkData c in chunkDataList) 
        {
            Mesh mesh = new Mesh();
            mesh.SetVertices(nVertices, c.vertOffset, c.vertCount);
            mesh.SetIndices(nTris, c.triOffset, c.triCount, MeshTopology.Triangles, 0);
            mesh.SetNormals(normals, c.vertOffset, c.vertCount);

            meshes[i] = mesh;
            meshIDs[i] = mesh.GetInstanceID();
            //chunksToGenerate[i].SetMesh(mesh);

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
        nTris.Dispose();
        nChunkData.Dispose();
        meshIDs.Dispose();

        ClearData();
        DisableNonVisibleChunks();

        coroutineRunning = false;
    }

    private void DisableNonVisibleChunks() 
    {
        foreach (FlatWorldChunk chunk in chunksToDissapear) 
        {
            chunk.ClearMesh();
        }
        chunksToDissapear.Clear();

        foreach (FlatWorldChunk parent in childrenToDelete)
        {
            foreach (FlatWorldChunk child in parent.children)
            {
                child.OnDestroy();
            }
            parent.VoidChildren();
        }
        childrenToDelete.Clear();
    }

    [BurstCompile]
    public struct GenerateFlatWorldChunks : IJobFor
    {
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> vertices;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> borderVerts;
        [NativeDisableContainerSafetyRestriction] public NativeArray<float3> normals;
        [NativeDisableContainerSafetyRestriction] public NativeArray<int> triangles;
        public NativeArray<ChunkData> chunkDataList;

        public void Execute(int i) 
        {
            int res = chunkDataList[i].resolution;
            int vertOffset = chunkDataList[i].vertOffset;
            int triOffset = chunkDataList[i].triOffset;
            float2 center = chunkDataList[i].center;
            float2 offset = chunkDataList[i].worldPosition;
            int borderOffset = chunkDataList[i].borderVertOffset;

            float scale = 1;
            for (int j = 0; j < chunkDataList[i].currentTreeDepth; j++) 
            {
                scale /= 2f;
            }

            int counter = 0;
            int borderCounter = 0;
            for (float x = 0; x <= res + 2; x++) 
            {
                for (float y = 0; y <= res + 2; y++) 
                {
                    float xPos = 5f * scale * ((x - 1) / res - .5f) + (2.5f * center.x);
                    float zPos = 5f * scale * ((y - 1) / res - .5f) + (2.5f * center.y);

                    float height = noise.cnoise(new float3(xPos + offset.x, 0, zPos + offset.y)) * .5f;
                    float f = 1f;
                    float a = 1f;
                    for (float m = 0; m < 10; m++) 
                    {
                        float _noise = noise.cnoise(new float3((xPos + offset.x) * f, 0, (zPos + offset.y) * f)) * .5f;
                        height += _noise * a;
                        f *= 1.7f;
                        a *= .6f;
                    }

                    //height *= noise.cnoise(new float3((xPos + offset.x) * .5f, 0, (zPos + offset.y) * .5f));

                    if (x == 0 || x == res + 2 || y == 0 || y == res + 2) 
                    {
                        borderVerts[borderCounter + borderOffset] = new float3(xPos, height, zPos);
                        borderCounter++;
                        continue;
                    }
                    vertices[vertOffset + counter] = new float3(xPos, height, zPos);
                    counter++;
                }
            }

            int resP = res + 1;
            for (int j = 0; j < res + 2; j++)
            {
                for (int k = 0; k < res + 2; k++)
                {
                    bool true0 = false;
                    bool true1 = false;
                    bool true2 = false;
                    bool true3 = false;

                    float3 vert0 = FindVert(i, j, k, ref true0);
                    float3 vert1 = FindVert(i, j, k + 1, ref true1);
                    float3 vert2 = FindVert(i, j + 1, k, ref true2);
                    float3 vert3 = FindVert(i, j + 1, k + 1, ref true3);

                    float3 AB = vert0 - vert1;
                    float3 AC = vert3 - vert1;
                    float3 BD = vert0 - vert2;
                    float3 BC = vert3 - vert2;

                    float3 normalA = math.normalize(math.cross(AC, AB));
                    float3 normalB = math.normalize(math.cross(BD, BC));

                    if (true0)
                        normals[vertOffset + (j - 1) * resP + (k - 1)] += normalA + normalB;
                    if (true1)
                        normals[vertOffset + (j - 1) * resP + k] += normalA;
                    if (true2)
                        normals[vertOffset + j * resP + (k - 1)] += normalB;
                    if (true3)
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