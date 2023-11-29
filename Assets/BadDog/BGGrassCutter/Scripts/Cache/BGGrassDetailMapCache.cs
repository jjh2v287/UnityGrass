using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    public class DetailMaps
    {
        private Dictionary<int, int[,]> m_DetailMaps = new Dictionary<int, int[,]>();

        public void AddDetailMap(int layer, int[,] detailMap)
        {
            if(m_DetailMaps.ContainsKey(layer))
            {
                m_DetailMaps[layer] = detailMap;
            }
            else
            {
                m_DetailMaps.Add(layer, detailMap);
            }
        }

        public int[,] RemoveDetailMap(int layer)
        {
            int[,] detailMap;

            if(m_DetailMaps.TryGetValue(layer, out detailMap))
            {
                m_DetailMaps.Remove(layer);
                return detailMap;
            }

            return null;
        }

        public int[,] GetDetailMap(int layer)
        {
            int[,] detailMaps;

            if(m_DetailMaps.TryGetValue(layer, out detailMaps))
            {
                return detailMaps;
            }

            return null;
        }

        public void RestoreToBorn(Terrain terrain)
        {
            foreach(KeyValuePair<int, int[,]> kvp in m_DetailMaps)
            {
                terrain.terrainData.SetDetailLayer(0, 0, kvp.Key, kvp.Value);
            }
        }

        public void Clear()
        {
            m_DetailMaps.Clear();
        }
    }

    public class BGGrassDetailMapCache
    {
        private DetailMaps m_RuntimeDetailMaps = new DetailMaps();
        private DetailMaps m_BornDetailMaps = new DetailMaps();

        public int[,] GetOrInsertDetailMap(Terrain terrain, int layer)
        {
            int[,] detailMap = m_RuntimeDetailMaps.GetDetailMap(layer);

            if (detailMap == null)
            {
                detailMap = terrain.terrainData.GetDetailLayer(0, 0, terrain.terrainData.detailWidth, terrain.terrainData.detailHeight, layer);
                m_RuntimeDetailMaps.AddDetailMap(layer, detailMap);

                int[,] clonedDetailMaps = (int[,])detailMap.Clone();
                m_BornDetailMaps.AddDetailMap(layer, clonedDetailMaps);
            }

            return detailMap;
        }

        public void RestoreToBorn(Terrain terrain)
        {
            m_BornDetailMaps.RestoreToBorn(terrain);
        }

        public void ClearRuntimeCache()
        {
            m_RuntimeDetailMaps.Clear();
        }

        public void ClearBornCache()
        {
            m_BornDetailMaps.Clear();
        }
    }
}
