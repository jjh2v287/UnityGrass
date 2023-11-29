using BadDog;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GrassCutMove : MonoBehaviour
{
    public GrassCutTerrain SelectTerrain;

    public float width = 2;
    public float length = 4;
    public float maxHeight = 3.0f;

    public float centerOffsetX = 0;
    public float centerOffsetZ = 0;

    void Start()
    {
        SelectTerrain?.mGrassCutMove.Add(this);
    }

    void Update()
    {
        transform.position += transform.forward * (5.0f * Time.deltaTime);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        UnityEditor.Handles.color = Color.red;

        Vector3 centerPos = transform.position + centerOffsetZ * transform.forward + centerOffsetX * transform.right;
        centerPos.y += 0.25f;

        Quaternion rotation = Quaternion.LookRotation(transform.forward);

        Vector3 p1 = new Vector3(-width / 2, 0, 0);
        Vector3 p2 = new Vector3(width / 2, 0, 0);
        Vector3 p3 = new Vector3(-width / 2, 0, length);
        Vector3 p4 = new Vector3(width / 2, 0, length);

        p1 = rotation * p1 + centerPos;
        p2 = rotation * p2 + centerPos;
        p3 = rotation * p3 + centerPos;
        p4 = rotation * p4 + centerPos;
        UnityEditor.Handles.DrawLines( new Vector3[] { centerPos, p1, centerPos, p2, p2, p4, p1, p3, p3, p4 } );
    }
#endif
}
