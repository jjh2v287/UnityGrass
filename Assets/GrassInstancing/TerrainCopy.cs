using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

public readonly struct Triangle
{
    public float3 V0 { get; }
    public float3 V1 { get; }
    public float3 V2 { get; }
    ///


//    /// This is already normalized.
//    ///
//    public float3 Normal { get; }
//    public Triangle(float3 v0, float3 v1, float3 v2)
//    {
//        V0 = v0;
//        V1 = v1;
//        V2 = v2;
//        Normal = math.normalize(math.cross(V1 - V0, V2 - V0));
//    }

//    public float SampleHeight(float3 position)
//    {
//        // plane formula: a(x - x0) + b(y - y0) + c(z - z0) = 0
//        // <a,b,c> is a normal vector for the plane
//        // (x,y,z) and (x0,y0,z0) are any points on the plane
//        return (-Normal.x * (position.x - V0.x) - Normal.z * (position.z - V0.z)) / Normal.y + V0.y;
//    }
//}

//public struct TerrainCopy
//{
//    List<float> heightMap;
//    int resolution;
//    float2 sampleSize;
//    public AABB AABB { get; private set; }
//    int QuadCount => resolution - 1;


//    public TerrainCopy(Terrain terrain, Allocator alloc)
//    {
//        resolution = terrain.terrainData.heightmapResolution;
//        sampleSize = new float2(terrain.terrainData.heightmapScale.x, terrain.terrainData.heightmapScale.z);
//        AABB = GetTerrrainAABB(terrain);
//        heightMap = GetHeightMap(terrain, alloc);
//    }

//    /// <summary>
//    /// Returns world height of terrain at x and z position values.
//    /// </summary>
//    public float SampleHeight(float3 worldPosition)
//    {
//        GetTriAtPosition(worldPosition, out Triangle tri);
//        return tri.SampleHeight(worldPosition);
//    }

//    /// <summary>
//    /// Returns world height of terrain at x and z position values. Also outputs normalized normal vector of terrain at position.
//    /// </summary>
//    public float SampleHeight(float3 worldPosition, out float3 normal)
//    {
//        GetTriAtPosition(worldPosition, out Triangle tri);
//        normal = tri.Normal;
//        return tri.SampleHeight(worldPosition);
//    }

//    void GetTriAtPosition(float3 worldPosition, out Triangle tri)
//    {
//        if (!IsWithinBounds(worldPosition))
//        {
//            throw new System.ArgumentException("Position given is outside of terrain x or z bounds.");
//        }
//        float2 localPos = new float2(
//            worldPosition.x - AABB.Min.x,
//            worldPosition.z - AABB.Min.z);
//        float2 samplePos = localPos / sampleSize;
//        int2 sampleFloor = (int2)math.floor(samplePos);
//        float2 sampleDecimal = samplePos - sampleFloor;
//        bool upperLeftTri = sampleDecimal.y > sampleDecimal.x;
//        int2 v1Offset = upperLeftTri ? new int2(0, 1) : new int2(1, 1);
//        int2 v2Offset = upperLeftTri ? new int2(1, 1) : new int2(1, 0);
//        float3 v0 = GetWorldVertex(sampleFloor);
//        float3 v1 = GetWorldVertex(sampleFloor + v1Offset);
//        float3 v2 = GetWorldVertex(sampleFloor + v2Offset);
//        tri = new Triangle(v0, v1, v2);
//    }

//    bool IsWithinBounds(float3 worldPos)
//    {
//        return
//            worldPos.x >= AABB.Min.x &&
//            worldPos.z >= AABB.Min.z &&
//            worldPos.x <= AABB.Max.x &&
//            worldPos.z <= AABB.Max.z;
//    }

//    float3 GetWorldVertex(int2 heightMapCrds)
//    {
//        int i = heightMapCrds.x + heightMapCrds.y * resolution;
//        float3 vertexPercentages = new float3(
//            (float)heightMapCrds.x / QuadCount,
//            heightMap *, *(float)heightMapCrds.y / QuadCount);
//        return AABB.Min + AABB.Size * vertexPercentages;
//    }
//    static AABB GetTerrrainAABB(Terrain terrain)
//    {
//        float3 min = terrain.transform.position;
//        float3 max = min + (float3)terrain.terrainData.size;
//        float3 extents = (max - min) / 2;
//        return new AABB() { Center = min + extents, Extents = extents };
//    }

//    static UnsafeList GetHeightMap(Terrain terrain, Allocator alloc)
//    {
//        int resolution = terrain.terrainData.heightmapResolution;
//        var heightList = new UnsafeList(resolution * resolution, alloc);
//        var map = terrain.terrainData.GetHeights(0, 0, resolution, resolution);
//        for (int y = 0; y < resolution; y++)
//        {
//            for (int x = 0; x < resolution; x++)
//            {
//                int i = y * resolution + x;
//                heightList = map[y, x];
//            }
//        }
//        return heightList;
//    }
}