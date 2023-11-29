using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    public class BGGrassCutUtils
    {
        private static RaycastHit[] raycastHits = new RaycastHit[8];

        public static List<Terrain> DetectTerrain(Vector3 origin, int layerMask, float radius = 10.0f, float maxDistance = 1000.0f)
        {
            List<Terrain> terrainList = new List<Terrain>();

            if (Physics.SphereCastNonAlloc(origin, radius, Vector3.down, raycastHits, maxDistance, layerMask) > 0)
            {
                foreach (var result in raycastHits)
                {
                    if (result.collider != null)
                    {
                        Terrain terrain = result.collider.GetComponent<Terrain>();

                        if (terrain != null)
                        {
                            terrainList.Add(terrain);
                        }
                    }
                }
            }

            return terrainList;
        }

        public static List<Terrain> DetectTerrain(GameObject go, int layerMask, float radius = 10.0f, float maxDistance = 1000.0f)
        {
            Vector3 origin = go.transform.position;
            origin.y += 2.0f;

            return DetectTerrain(origin, layerMask, radius, maxDistance);
        }

        public static Vector3 GetWorldPositionOnTerrain(Terrain terrain, int x, int z, float multiplierX, float multiplierZ)
        {
            Vector3 terrainPos = terrain.GetPosition();

            float worldX = x / multiplierX + terrainPos.x;
            float worldZ = z / multiplierZ + terrainPos.z;
            float worldY = terrain.SampleHeight(new Vector3(worldX, terrainPos.y + 10, worldZ)) + terrainPos.y;

            return new Vector3(worldX, worldY, worldZ);
        }

        public static float GetWorldHeightOnTerrain(Terrain terrain, Vector3 worldPos)
        {
            float worldY = terrain.SampleHeight(worldPos) + terrain.GetPosition().y;
            return worldY;
        }
    }
}
