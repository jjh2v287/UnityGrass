using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Rendering.VolumeComponent;

[ExecuteInEditMode]
public class VectorToIndex : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Vector3 pos = new Vector3(-4.00f, 0, -4.00f);
        long index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-3.9f, 0, -4.00f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-3.8f, 0, -4.00f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-3.0f, 0, -4.00f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-4.00f, 0, -3.9f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-4.00f, 0, -3.8f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);

        pos = new Vector3(-3.9f, 0, -3.9f);
        index = Vector3ToIndex(pos, 4 * 2, 10);
        Debug.LogError("index1 : " + index.ToString());
        pos = IndexToVector3(index, 4 * 2, 10);


        List<long> indexs = new List<long>();
        int iCount = 4;
        for (int zCount = -iCount; zCount <= iCount; zCount++)
        {
            for (int xCount = -iCount; xCount <= iCount; xCount++)
            {
                Vector3 newpos = new Vector3(xCount, 0, zCount);
                long index1 = Vector3ToIndex(newpos, iCount*2, 10);
                Debug.LogError("index1 : " + index1.ToString());
                indexs.Add(index1);
#if UNITY_EDITOR
                debubPos.Add(newpos);
#endif
            }
        }


        for (int icount = 0; icount < indexs.Count; icount++)
        {
            Vector3 newpos = IndexToVector3(indexs[icount], iCount * 2, 10);
#if UNITY_EDITOR
            if (debubPos[icount] == newpos)
                Debug.LogError("newpos : true : " + newpos.ToString());
            else
                Debug.LogError("newpos : false : " + newpos.ToString());
#endif
        }
    }

    long Vector3ToIndex(Vector3 vector, int size, int scale)
    {
        int gridSizeX = size; // X���� �׸��� ũ��
        int gridSizeZ = size; // Z���� �׸��� ũ��

        float x = vector.x; // ��� X ��ǥ
        float z = vector.z; // ��� Z ��ǥ

        // ���� ��ǥ�� ����� �̵���Ű�� �۾� (�� �κ��� �ʿ信 ���� �ٸ� �� �ֽ��ϴ�)
        int adjustedX = Mathf.FloorToInt(x * (float)scale); // �Ҽ��� ��° �ڸ����� ���
        int adjustedZ = Mathf.FloorToInt(z * (float)scale); // �Ҽ��� ��° �ڸ����� ���

        // �׸��� ũ�⸦ ����� ����
        adjustedX += (gridSizeX / 2) * scale;
        adjustedZ += (gridSizeZ / 2) * scale;

        // �̰� ����� �ε��� ���� 
        if (adjustedZ > 0)
            adjustedX += adjustedZ;
        
        // �ε��� ���
        long index = adjustedX + (adjustedZ * gridSizeX);
        return index;
    }

    Vector3 IndexToVector3(long index, int size, int scale)
    {
        int gridSizeX = size; // X���� �׸��� ũ��
        int gridSizeZ = size; // Z���� �׸��� ũ��

        // �ε����� X, Y, Z ��ǥ�� �и�
        // n�� 0���� gridSizeX���� ���� ǥ���ҷ��� 0���ֱ⶧���� 1�� ���Ѵ�
        long x = index % ((gridSizeX + 1) * scale);
        long z = (index / ((gridSizeZ + 1) * scale) * scale);

        // �׸��� ũ�⸦ ����� ����
        x -= (gridSizeX / 2) * scale;
        z -= (gridSizeZ / 2) * scale;

        // �Ҽ��� �� �ڸ����� ����Ͽ� ��ǥ�� ��ȯ
        return new Vector3((float)x / (float)scale, 0, (float)z / (float)scale);
    }

#if UNITY_EDITOR
    List<Vector3> debubPos = new List<Vector3>();
    private void OnDrawGizmos()
    {
        UnityEditor.Handles.color = Color.red;
        for (int i = 0; i < debubPos.Count; i++)
        {
            if(i <= 6)
                UnityEditor.Handles.color = Color.yellow;
            else if (i > 6 && i <= 13)
                UnityEditor.Handles.color = Color.blue;
            else
                UnityEditor.Handles.color = Color.red;
            Vector3 p1 = debubPos[i];
            Vector3 p2 = debubPos[i];
            p2.y = p2.y + 1;
            Handles.DrawLine(p1, p2);
        }
    }
#endif
}
