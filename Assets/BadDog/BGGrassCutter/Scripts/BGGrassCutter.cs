using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BadDog
{
    public enum BGGrassCutShape
    {
        Circle,
        Sector,
        Rect,
    }

    public enum BGGrassCutTpye
    {
        AllLayers,
        OneLayer
    }

    public enum BGGrassProcessingType
    {
        OnEnable,
        Update,
        LateUpdate,
        Manual
    }

    public class BGGrassCutter : MonoBehaviour
    {
        public LayerMask terrainLayer = -1;
        public BGGrassProcessingType processingType = BGGrassProcessingType.OnEnable;
        public float updateStep = 0.1f;

        public BGGrassCutTpye cutType;
        public int cutLayer = 0;

        public BGGrassCutShape cutShape;
        public float radius = 3;
        public float degree = 90;
        public float width = 2;
        public float length = 4;
        public float maxHeight = 3.0f;

        public float centerOffsetX = 0;
        public float centerOffsetZ = 0;

        private float m_LastUpdateTime = 0;

        private void CutGrassByCircle(BGGrassCutManager grassCutManager, Vector3 centerPos)
        {
            if (cutType == BGGrassCutTpye.AllLayers)
            {
                grassCutManager.CutAllGrassByCircle(centerPos, transform.forward, radius);
            }
            else
            {
                grassCutManager.CutGrassByCircle(cutLayer, centerPos, transform.forward, radius);
            }
        }

        private void CutGrassBySector(BGGrassCutManager grassCutManager, Vector3 centerPos)
        {
            if (cutType == BGGrassCutTpye.AllLayers)
            {
                grassCutManager.CutAllGrassBySector(centerPos, transform.forward, radius, degree);
            }
            else
            {
                grassCutManager.CutGrassBySector(cutLayer, centerPos, transform.forward, radius, degree);
            }
        }

        private void CutGrassByRect(BGGrassCutManager grassCutManager, Vector3 centerPos)
        {
            if (cutType == BGGrassCutTpye.AllLayers)
            {
                grassCutManager.CutAllGrassByRect(centerPos, transform.forward, width, length);
            }
            else
            {
                grassCutManager.CutGrassByRect(cutLayer, centerPos, transform.forward, width, length);
            }
        }

        private void CutGrassByType()
        {
            List<Terrain> terrainList = BGGrassCutUtils.DetectTerrain(gameObject, terrainLayer);

            for (int i = 0; i < terrainList.Count; i++)
            {
                Terrain terrain = terrainList[i];

                BGGrassCutManager grassCutManager = terrain.GetComponent<BGGrassCutManager>();

                if (grassCutManager == null || !grassCutManager.isActiveAndEnabled)
                {
                    return;
                }

                Vector3 centerPos = transform.position + centerOffsetZ * transform.forward + centerOffsetX * transform.right;

                if (Mathf.Abs(centerPos.y - BGGrassCutUtils.GetWorldHeightOnTerrain(terrain, centerPos)) > maxHeight)
                {
                    return;
                }

                if (cutShape == BGGrassCutShape.Circle)
                {
                    CutGrassByCircle(grassCutManager, centerPos);
                }
                else if (cutShape == BGGrassCutShape.Sector)
                {
                    CutGrassBySector(grassCutManager, centerPos);
                }
                else if (cutShape == BGGrassCutShape.Rect)
                {
                    CutGrassByRect(grassCutManager, centerPos);
                }
            }
        }

        private void OnEnable()
        {
            m_LastUpdateTime = Time.time;

            if (processingType == BGGrassProcessingType.OnEnable)
            {
                CutGrassByType();
            }
        }

        private void Update()
        {
            if(processingType == BGGrassProcessingType.Update)
            {
                if (Time.time - m_LastUpdateTime > updateStep)
                {
                    CutGrassByType();
                    m_LastUpdateTime = Time.time;
                }
            }
        }

        private void LateUpdate()
        {
            if(processingType == BGGrassProcessingType.LateUpdate)
            {
                if (Time.time - m_LastUpdateTime > updateStep)
                {
                    CutGrassByType();
                    m_LastUpdateTime = Time.time;
                }
            }
        }

        public void Cut()
        {
            CutGrassByType();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            UnityEditor.Handles.color = Color.red;

            Vector3 centerPos = transform.position + centerOffsetZ * transform.forward + centerOffsetX * transform.right;
            centerPos.y += 0.25f;

            if (cutShape == BGGrassCutShape.Circle)
            {
                UnityEditor.Handles.DrawWireArc(centerPos, Vector3.up, transform.forward, 360, radius);
            }
            else if(cutShape == BGGrassCutShape.Sector)
            {
                UnityEditor.Handles.DrawSolidArc(centerPos, Vector3.up, transform.forward, -degree/2, radius);
                UnityEditor.Handles.DrawSolidArc(centerPos, Vector3.up, transform.forward, degree/2, radius);
            }
            else if(cutShape == BGGrassCutShape.Rect)
            {
                Quaternion rotation = Quaternion.LookRotation(transform.forward);

                Vector3 p1 = new Vector3(-width / 2, 0, 0);
                Vector3 p2 = new Vector3(width / 2, 0, 0);
                Vector3 p3 = new Vector3(-width / 2, 0, length);
                Vector3 p4 = new Vector3(width / 2, 0, length);

                p1 = rotation * p1 + centerPos;
                p2 = rotation * p2 + centerPos;
                p3 = rotation * p3 + centerPos;
                p4 = rotation * p4 + centerPos;

                UnityEditor.Handles.DrawLines(new Vector3[] { centerPos, p1, centerPos, p2, p2, p4, p1, p3, p3, p4 });
            }
        }
#endif
    }
}
