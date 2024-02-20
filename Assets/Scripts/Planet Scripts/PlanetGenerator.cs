using UnityEngine;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;

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