using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    [Serializable]
    public class BGGrassCutEffectLayerInfo
    {
        public float life = 2.0f;
        public GameObject effectTemplate;
    }

    public class BGGrassCutManager : MonoBehaviour
    {
        [Tooltip("GameObject index correspond with Terrain's Detail layer index")]
        public BGGrassCutEffectLayerInfo[] grassCutEffects;


        private Terrain m_Terrain;

        private BGGrassCutEffectCache m_GrassCutEffectCache = new BGGrassCutEffectCache();
        private BGGrassDetailMapCache m_GrassDetailMapCache = new BGGrassDetailMapCache();


        private void OnEnable()
        {
            m_Terrain = GetComponent<Terrain>();

            m_GrassCutEffectCache.Init(this, 16);
        }

        private void OnDisable()
        {
            m_GrassCutEffectCache.Clear();

            m_GrassDetailMapCache.RestoreToBorn(m_Terrain);
            m_GrassDetailMapCache.ClearRuntimeCache();
            m_GrassDetailMapCache.ClearBornCache();
        }

        private void Update()
        {
            m_GrassCutEffectCache.UpdateLeavePoolEffects();
        }

        private void PlayCutGrassEffect(int layer, List<Vector3> grassCutPosList)
        {
            GameObject effectTemplate = grassCutEffects[layer].effectTemplate;
            float life = grassCutEffects[layer].life;

            if(effectTemplate != null)
            {
                for (int i = 0; i < grassCutPosList.Count; i++)
                {
                    Vector3 pos = grassCutPosList[i];

                    GameObject effect = m_GrassCutEffectCache.Get(layer, life);

                    if (effect != null)
                    {
                        effect.transform.parent = null;
                        effect.transform.position = pos;

                        effect.SetActive(true);
                    }
                }
            }
        }

        public void CutGrassByCircle(int detailLayer, Vector3 position, Vector3 forward, float radius)
        {
            if(m_Terrain == null || !isActiveAndEnabled)
            {
                return;
            }

            List<Vector3> grassCutPosList = new List<Vector3>();

            int[,] detailMap = m_GrassDetailMapCache.GetOrInsertDetailMap(m_Terrain, detailLayer);

            if(detailMap == null || detailMap.Length <= 0)
            {
                return;
            }

            float multiplierX = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.x;
            float multiplierZ = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.z;

            float squareMultiplierX = 1 / (multiplierX * multiplierX);
            float squareMultiplierZ = 1 / (multiplierZ * multiplierZ);

            Vector3 worldOffset = position - m_Terrain.GetPosition();

            int minX = Mathf.FloorToInt((worldOffset.x - radius) * multiplierX);
            int maxX = Mathf.FloorToInt((worldOffset.x + radius) * multiplierX);
            int minZ = Mathf.FloorToInt((worldOffset.z - radius) * multiplierZ);
            int maxZ = Mathf.FloorToInt((worldOffset.z + radius) * multiplierZ);

            int centerX = Mathf.FloorToInt(worldOffset.x * multiplierX);
            int centerZ = Mathf.FloorToInt(worldOffset.z * multiplierZ);

            bool changed = false;

            for(int x = minX; x <= maxX; x++)
            {
                for(int z = minZ; z <= maxZ; z++)
                {
                    int deltaX = x - centerX;
                    int deltaZ = z - centerZ;

                    if (deltaX * deltaX * squareMultiplierX + deltaZ * deltaZ * squareMultiplierZ <= radius * radius)
                    {
                        int detailX = Mathf.Clamp(x, 0, m_Terrain.terrainData.detailHeight - 1);
                        int detailZ = Mathf.Clamp(z, 0, m_Terrain.terrainData.detailWidth - 1);

                        if (detailMap[detailZ, detailX] != 0)
                        {
                            detailMap[detailZ, detailX] = 0;
                            changed = true;

                            grassCutPosList.Add(BGGrassCutUtils.GetWorldPositionOnTerrain(m_Terrain, detailX, detailZ, multiplierX, multiplierZ));
                        }
                    }
                }
            }

            if (changed)
            {
                m_Terrain.terrainData.SetDetailLayer(0, 0, detailLayer, detailMap);

                PlayCutGrassEffect(detailLayer, grassCutPosList);
            }
        }

        public void CutAllGrassByCircle(Vector3 position, Vector3 forward, float radius)
        {
            if (grassCutEffects == null || grassCutEffects.Length <= 0)
            {
                return;
            }

            for (int layer = 0; layer < grassCutEffects.Length; layer++)
            {
                CutGrassByCircle(layer, position, forward, radius);
            }
        }

        public void CutGrassBySector(int detailLayer, Vector3 position, Vector3 forward, float radius, float degree)
        {
            if(m_Terrain == null || !isActiveAndEnabled)
            {
                return;
            }

            List<Vector3> grassCutPosList = new List<Vector3>();

            int[,] detailMap = m_GrassDetailMapCache.GetOrInsertDetailMap(m_Terrain, detailLayer);

            if(detailMap == null || detailMap.Length <= 0)
            {
                return;
            }

            float multiplierX = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.x;
            float multiplierZ = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.z;

            Vector3 worldOffset = position - m_Terrain.GetPosition();

            int minX = Mathf.FloorToInt((worldOffset.x - radius) * multiplierX);
            int maxX = Mathf.FloorToInt((worldOffset.x + radius) * multiplierX);
            int minZ = Mathf.FloorToInt((worldOffset.z - radius) * multiplierZ);
            int maxZ = Mathf.FloorToInt((worldOffset.z + radius) * multiplierZ);

            int centerX = Mathf.FloorToInt(worldOffset.x * multiplierX);
            int centerZ = Mathf.FloorToInt(worldOffset.z * multiplierZ);

            float dotAngle = 1 - degree / 180;

            Vector2 forwardXZ;
            forwardXZ.x = forward.x;
            forwardXZ.y = forward.z;

            bool changed = false;

            for(int x = minX; x <= maxX; x++)
            {
                for(int z = minZ; z <= maxZ; z++)
                {
                    Vector2 offsetXZ;
                    offsetXZ.x = (x - centerX) / multiplierX;
                    offsetXZ.y = (z - centerZ) / multiplierZ;

                    if(dotAngle > -0.99f && Vector2.Dot(offsetXZ.normalized, forwardXZ) <= dotAngle)
                    {
                        continue;
                    }

                    if(offsetXZ.sqrMagnitude <= radius * radius)
                    {
                        int detailX = Mathf.Clamp(x, 0, m_Terrain.terrainData.detailHeight - 1);
                        int detailZ = Mathf.Clamp(z, 0, m_Terrain.terrainData.detailWidth - 1);

                        if (detailMap[detailZ, detailX] != 0)
                        {
                            detailMap[detailZ, detailX] = 0;
                            changed = true;

                            grassCutPosList.Add(BGGrassCutUtils.GetWorldPositionOnTerrain(m_Terrain, detailX, detailZ, multiplierX, multiplierZ));
                        }
                    }
                }
            }

            if (changed)
            {
                m_Terrain.terrainData.SetDetailLayer(0, 0, detailLayer, detailMap);

                PlayCutGrassEffect(detailLayer, grassCutPosList);
            }
        }

        public void CutAllGrassBySector(Vector3 position, Vector3 forward, float radius, float degree)
        {
            if (grassCutEffects == null || grassCutEffects.Length <= 0)
            {
                return;
            }

            for (int layer = 0; layer < grassCutEffects.Length; layer++)
            {
                CutGrassBySector(layer, position, forward, radius, degree);
            }
        }

        public void CutGrassByRect(int detailLayer, Vector3 position, Vector3 forward, float width, float length)
        {
            if(m_Terrain == null || !isActiveAndEnabled)
            {
                return;
            }

            List<Vector3> grassCutPosList = new List<Vector3>();

            int[,] detailMap = m_GrassDetailMapCache.GetOrInsertDetailMap(m_Terrain, detailLayer);

            if(detailMap == null || detailMap.Length <= 0)
            {
                return;
            }

            float multiplierX = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.x;
            float multiplierZ = m_Terrain.terrainData.detailResolution / m_Terrain.terrainData.size.z;

            Vector3 worldOffset = position - m_Terrain.GetPosition();

            int centerX = Mathf.FloorToInt(worldOffset.x * multiplierX);
            int centerZ = Mathf.FloorToInt(worldOffset.z * multiplierZ);

            Quaternion rotation = Quaternion.LookRotation(forward);

            int detailWidth = Mathf.FloorToInt(width * 0.5f * multiplierX);
            int detailLength = Mathf.FloorToInt(length * multiplierZ);

            bool changed = false;

            for(int x = -detailWidth; x <= detailWidth; x++)
            {
                for(int z = 0; z <= detailLength; z++)
                {
                    Vector3 offsetXZ = Vector3.zero;
                    offsetXZ.x = x / multiplierX;
                    offsetXZ.z = z / multiplierZ;

                    offsetXZ = rotation * offsetXZ;

                    int detailX = Mathf.Clamp(Mathf.FloorToInt(offsetXZ.x * multiplierX) + centerX, 0, m_Terrain.terrainData.detailHeight-1);
                    int detailZ = Mathf.Clamp(Mathf.FloorToInt(offsetXZ.z * multiplierZ) + centerZ, 0, m_Terrain.terrainData.detailWidth-1);

                    if (detailMap[detailZ, detailX] != 0)
                    {
                        detailMap[detailZ, detailX] = 0;
                        changed = true;

                        grassCutPosList.Add(BGGrassCutUtils.GetWorldPositionOnTerrain(m_Terrain, detailX, detailZ, multiplierX, multiplierZ));
                    }
                }
            }

            if (changed)
            {
                m_Terrain.terrainData.SetDetailLayer(0, 0, detailLayer, detailMap);

                PlayCutGrassEffect(detailLayer, grassCutPosList);
            }
        }

        public void CutAllGrassByRect(Vector3 position, Vector3 forward, float width, float length)
        {
            if(grassCutEffects == null || grassCutEffects.Length <= 0)
            {
                return;
            }

            for(int layer = 0; layer < grassCutEffects.Length; layer++)
            {
                CutGrassByRect(layer, position, forward, width, length);
            }
        }
    }
}
