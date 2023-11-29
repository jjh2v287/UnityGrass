using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static UnityEngine.Rendering.DebugUI.Table;

public class QuadtreeNode
{
    public Rect Bounds { get; private set; }
    public int MaxObjectsPerNode { get; private set; }

    private List<Vector2> objects = new List<Vector2>();
    private QuadtreeNode[] children = new QuadtreeNode[4];

    public QuadtreeNode(Rect bounds, int maxObjectsPerNode)
    {
        Bounds = bounds;
        MaxObjectsPerNode = maxObjectsPerNode;
    }

    public void Insert(Vector2 point)
    {
        if (objects.Count < MaxObjectsPerNode)
        {
            objects.Add(point);
        }
        else
        {
            if (children[0] == null)
            {
                Split();
            }

            for (int i = 0; i < 4; i++)
            {
                if (children[i].Bounds.Contains(point))
                {
                    children[i].Insert(point);
                    break;
                }
            }
        }
    }

    private void Split()
    {
        float halfWidth = Bounds.width * 0.5f;
        float halfHeight = Bounds.height * 0.5f;

        children[0] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y, halfWidth, halfHeight), MaxObjectsPerNode);
        children[1] = new QuadtreeNode(new Rect(Bounds.x + halfWidth, Bounds.y, halfWidth, halfHeight), MaxObjectsPerNode);
        children[2] = new QuadtreeNode(new Rect(Bounds.x, Bounds.y + halfHeight, halfWidth, halfHeight), MaxObjectsPerNode);
        children[3] = new QuadtreeNode(new Rect(Bounds.x + halfWidth, Bounds.y + halfHeight, halfWidth, halfHeight), MaxObjectsPerNode);

        foreach (var obj in objects)
        {
            for (int i = 0; i < 4; i++)
            {
                //if (children[i].Bounds.Contains(obj))
                {
                    children[i].Insert(obj);
                    break;
                }
            }
        }

        objects.Clear();
    }

    public bool HasChildren()
    {
        return !IsLeaf();
    }

    public bool IsLeaf()
    {
        return children[0] == null;
    }

    public QuadtreeNode[] GetChildren()
    {
        return children;
    }

    public List<Vector2> GetObjects()
    {
        return objects;
    }

    public void Remove(Vector2 obj)
    {
        objects.Remove(obj);
    }
}

[ExecuteInEditMode]
public class Gizmos : MonoBehaviour
{
    private int MaxObjectsPerNode = 4;
    private Vector2 TerrainSize = new Vector2(1f, 1f);
    private QuadtreeNode quadtree;

    private static int cellCountRow = 10;
    private static int cellCountCol = 10;
    private static int rows = 1 * cellCountRow;
    private static int cols = 1 * cellCountCol;
    private float minX = -0.5f;
    private float maxX = 0.5f;
    private float minZ = -0.5f;
    private float maxZ = 0.5f;
    //private static int rows = 1000 * cellCountRow;
    //private static int cols = 1000 * cellCountCol;
    //private float minX = -500f;
    //private float maxX = 500f;
    //private float minZ = -500f;
    //private float maxZ = 500f;

    public void OnDrawGizmos()
    {
        float stepX = (maxX - minX) / cols;
        float stepZ = (maxZ - minZ) / rows;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float posX = minX + col * stepX;
                float posZ = minZ + row * stepZ;
                Vector3 position = new Vector3(posX, 0f, posZ) + gameObject.transform.position;

                UnityEngine.Gizmos.color = Color.green;
                UnityEngine.Gizmos.DrawLine(position, position + new Vector3(0.0f, 1.0f, 0.0f));
            }
        }
    }

    void Start()
    {
        List<Vector2> grassPoints = new List<Vector2>();
        float stepX = (maxX - minX) / cols;
        float stepZ = (maxZ - minZ) / rows;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                float posX = minX + row * stepX; 
                float posZ = minZ + col * stepZ;
                Vector3 position = new Vector3(posX, 0f, posZ) + gameObject.transform.position;

                grassPoints.Add(new Vector2(position.x, position.y));
            }
        }

        // Build quadtree
        Vector3 remposition = new Vector3(TerrainSize.x, 0f, TerrainSize.y) + gameObject.transform.position;
        quadtree = new QuadtreeNode(new Rect(0, 0, remposition.x, remposition.z), MaxObjectsPerNode);
        foreach (var point in grassPoints)
        {
            quadtree.Insert(point);
        }

        // Example: Remove grass near a point
        Vector3 playerPosition = new Vector3(0.0f, 0.0f, 0.0f) + gameObject.transform.position;
        RemoveGrassNear(playerPosition);
    }

    private void RemoveGrassNear(Vector2 position)
    {
        // Find the quadtree node that contains the player position
        QuadtreeNode node = FindNodeContaining(quadtree, position);

        // Remove grass from the node
        if (node != null)
        {
            List<Vector2> removedGrass = new List<Vector2>();
            foreach (var grass in node.GetObjects())
            {
                if (Vector2.Distance(grass, position) < 0.1f) // Example removal radius
                {
                    removedGrass.Add(grass);
                }
            }

            foreach (var grass in removedGrass)
            {
                node.Remove(grass);
            }
        }
    }

    private QuadtreeNode FindNodeContaining(QuadtreeNode node, Vector2 point)
    {
        //if (!node.Bounds.Contains(point))
        //{
        //    return null;
        //}

        if (node.HasChildren())
        {
            for (int i = 0; i < 4; i++)
            {
                QuadtreeNode child = node.GetChildren()[i];
                QuadtreeNode result = FindNodeContaining(child, point);
                if (result != null)
                {
                    return result;
                }
            }
        }

        return node;
    }
}
