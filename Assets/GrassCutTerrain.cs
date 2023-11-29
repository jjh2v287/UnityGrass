using BadDog;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GrassCutTerrain : MonoBehaviour
{
    private Terrain mTerrain;

    private float m_LastUpdateTime = 0;
    public float updateStep = 0.1f;

    public GrassCutEffectLayerInfo[] grassCutEffects;

    [HideInInspector]
    public List<GrassCutMove> mGrassCutMove = new List<GrassCutMove>();

    [Serializable]
    public class GrassCutEffectLayerInfo
    {
        public float life = 2.0f;
        public GameObject effectTemplate;
    }

    private void Awake()
    {
        mTerrain = GetComponent<Terrain>();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    private void OnEnable()
    {
        m_LastUpdateTime = Time.time;
    }

    // Update is called once per frame
    void Update()
    {
        if ( Time.time - m_LastUpdateTime > updateStep )
        {
            if ( mTerrain )
            {
                for ( int layer = 0; layer < grassCutEffects.Length; layer++ )
                {
                    CutGrassByRect( layer, mGrassCutMove );
                }
            }
            m_LastUpdateTime = Time.time;
        }
    }

    public int[,] GetOrInsertDetailMap(Terrain terrain, int layer)
    {
        int[,] detailMap = terrain.terrainData.GetDetailLayer( 0, 0, terrain.terrainData.detailWidth, terrain.terrainData.detailHeight, layer );
        int[,] clonedDetailMaps = (int[,])detailMap.Clone();
        return detailMap;
    }

    public static Vector3 GetWorldPositionOnTerrain(Terrain terrain, int x, int z, float multiplierX, float multiplierZ)
    {
        Vector3 terrainPos = terrain.GetPosition();

        float worldX = x / multiplierX + terrainPos.x;
        float worldZ = z / multiplierZ + terrainPos.z;
        float worldY = terrain.SampleHeight(new Vector3(worldX, terrainPos.y + 10, worldZ)) + terrainPos.y;

        return new Vector3( worldX, worldY, worldZ );
    }

    private void PlayCutGrassEffect(int layer, List<Vector3> grassCutPosList)
    {
        GameObject effectTemplate = grassCutEffects[layer].effectTemplate;
        float life = grassCutEffects[layer].life;

        if ( effectTemplate != null )
        {
            for ( int i = 0; i < grassCutPosList.Count; i++ )
            {
                Vector3 pos = grassCutPosList[i];

                GameObject effect = effectTemplate;// m_GrassCutEffectCache.Get(layer, life);

                if ( effect != null )
                {
                    effect.transform.parent = null;
                    effect.transform.position = pos;

                    effect.SetActive( true );
                }
            }
        }
    }

    public void CutGrassByRect(int detailLayer, List<GrassCutMove> grassCutMove)
    {
        if ( mTerrain == null || !isActiveAndEnabled )
        {
            return;
        }

        List<Vector3> grassCutPosList = new List<Vector3>();

        int[,] detailMap = GetOrInsertDetailMap(mTerrain, detailLayer);

        if ( detailMap == null || detailMap.Length <= 0 )
        {
            return;
        }

        bool changed = false;
        for ( int i = 0; i < grassCutMove.Count; i++ )
        {
            Vector3 position = grassCutMove[i].gameObject.transform.position;
            Vector3 forward = grassCutMove[i].gameObject.transform.forward;
            float width = grassCutMove[i].width;
            float length = grassCutMove[i].length;

            float multiplierX = mTerrain.terrainData.detailResolution / mTerrain.terrainData.size.x;
            float multiplierZ = mTerrain.terrainData.detailResolution / mTerrain.terrainData.size.z;

            Vector3 worldOffset = position - mTerrain.GetPosition();
            int centerX = Mathf.FloorToInt(worldOffset.x * multiplierX);
            int centerZ = Mathf.FloorToInt(worldOffset.z * multiplierZ);
            Quaternion rotation = Quaternion.LookRotation(forward);

            int detailWidth = Mathf.FloorToInt(width * 0.5f * multiplierX);
            int detailLength = Mathf.FloorToInt(length * multiplierZ);

            for ( int x = -detailWidth; x <= detailWidth; x++ )
            {
                for ( int z = 0; z <= detailLength; z++ )
                {
                    Vector3 offsetXZ = Vector3.zero;
                    offsetXZ.x = x / multiplierX;
                    offsetXZ.z = z / multiplierZ;

                    offsetXZ = rotation * offsetXZ;

                    int detailX = Mathf.Clamp(Mathf.FloorToInt(offsetXZ.x * multiplierX) + centerX, 0, mTerrain.terrainData.detailHeight-1);
                    int detailZ = Mathf.Clamp(Mathf.FloorToInt(offsetXZ.z * multiplierZ) + centerZ, 0, mTerrain.terrainData.detailWidth-1);

                    if ( detailMap[detailZ, detailX] != 0 )
                    {
                        detailMap[detailZ, detailX] = 0;
                        changed = true;

                        grassCutPosList.Add( GetWorldPositionOnTerrain( mTerrain, detailX, detailZ, multiplierX, multiplierZ ) );
                    }
                }
            }
        }

        if ( changed )
        {
            mTerrain.terrainData.SetDetailLayer( 0, 0, detailLayer, detailMap );
            //PlayCutGrassEffect( detailLayer, grassCutPosList );
        }
    }
}
