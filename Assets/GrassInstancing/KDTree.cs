using System;
using System.Collections.Generic;
using UnityEngine;

namespace RRSKDTree
{
    public class KDTreePosition
    {
        public Vector3 Position;
        public int Hash;

        public KDTreePosition(Vector3 position)
        {
            Position = position;
            Hash = position.GetHashCode();
        }

        public KDTreePosition(Vector3 position, int hash)
        {
            Position = position;
            Hash = hash;
        }
    }

    public class Node
    {
        public KDTreePosition point;
        public Node left;
        public Node right;
    }

    public class KDTree
    {
        private Node root;
        public List<Node> findNodes;

        public void Build(List<KDTreePosition> points)
        {
            root = BuildRecursive(points, 0);
        }

        public void Clear()
        {
            root = null;
        }

        private Node BuildRecursive(List<KDTreePosition> points, int depth)
        {
            if (points.Count == 0)
                return null;

            // 3차원 축을 선택
            int axis = depth % 3;
            points.Sort((a, b) => a.Position[axis].CompareTo(b.Position[axis]));

            int middle = points.Count / 2;
            Node node = new Node
            {
                point = points[middle],
                left = BuildRecursive(points.GetRange(0, middle), depth + 1),
                right = BuildRecursive(points.GetRange(middle + 1, points.Count - middle - 1), depth + 1)
            };

            return node;
        }

        public Vector3 FindNearest(Vector3 queryPoint)
        {
            return FindNearestRecursive(root, queryPoint, 0);
        }

        private Vector3 FindNearestRecursive(Node node, Vector3 queryPoint, int depth)
        {
            if (node == null)
                return Vector3.zero;

            // 3차원 축을 선택
            int axis = depth % 3;

            Vector3 best = node.point.Position;
            Node nextBranch, oppositeBranch;

            if (queryPoint[axis] < node.point.Position[axis])
            {
                nextBranch = node.left;
                oppositeBranch = node.right;
            }
            else
            {
                nextBranch = node.right;
                oppositeBranch = node.left;
            }

            Vector3 bestNext = FindNearestRecursive(nextBranch, queryPoint, depth + 1);
            if (Vector3.Distance(queryPoint, bestNext) < Vector3.Distance(queryPoint, best))
                best = bestNext;

            if (Mathf.Abs(queryPoint[axis] - node.point.Position[axis]) < Vector3.Distance(queryPoint, best))
            {
                Vector3 bestOpposite = FindNearestRecursive(oppositeBranch, queryPoint, depth + 1);
                if (Vector3.Distance(queryPoint, bestOpposite) < Vector3.Distance(queryPoint, best))
                    best = bestOpposite;
            }

            return best;
        }

        public List<KDTreePosition> FindInRange(Vector3 center, float radius)
        {
            if (findNodes == null)
                findNodes = new List<Node>();
            else
                findNodes.Clear();
            List<KDTreePosition> pointsInRange = new List<KDTreePosition>();
            FindInRangeRecursive(root, center, radius, 0, pointsInRange);
            return pointsInRange;
        }

        private void FindInRangeRecursive(Node node, Vector3 center, float radius, int depth, List<KDTreePosition> pointsInRange)
        {
            if (node == null)
                return;

            // 3차원 축을 선택
            int axis = depth % 3;

            float axisDistance = Mathf.Abs(center[axis] - node.point.Position[axis]);
            bool isInsideRange = true;

            for (int d = 0; d < 3; d++)
            {
                if (d != axis)
                {
                    float distance = Mathf.Abs(center[d] - node.point.Position[d]);
                    if (distance > radius)
                    {
                        isInsideRange = false;
                        break;
                    }
                }
            }

            if (axisDistance <= radius && isInsideRange)
            {
                pointsInRange.Add(node.point);
                findNodes.Add(node);
            }

            if (center[axis] - radius < node.point.Position[axis])
                FindInRangeRecursive(node.left, center, radius, depth + 1, pointsInRange);
            if (center[axis] + radius >= node.point.Position[axis])
                FindInRangeRecursive(node.right, center, radius, depth + 1, pointsInRange);
        }

        public void RemoveNode(Node nodeToRemove)
        {
            root = DeleteNode(root, nodeToRemove, 0);
        }

        private Node DeleteNode(Node node, Node nodeToRemove, int depth)
        {
            if (node == null)
                return null;

            // 3차원 축을 선택
            int axis = depth % 3;

            if (node == nodeToRemove)
            {
                if (node.right == null)
                    return node.left;
                if (node.left == null)
                    return node.right;

                node.point.Position = FindMin(node.right, axis);
                node.right = DeleteNode(node.right, node, depth + 1);
            }
            else if (nodeToRemove.point.Position[axis] < node.point.Position[axis])
                node.left = DeleteNode(node.left, nodeToRemove, depth + 1);
            else
                node.right = DeleteNode(node.right, nodeToRemove, depth + 1);

            return node;
        }

        public void Remove(int hashToRemove)
        {
            root = RemoveRecursive(root, hashToRemove, 0);
        }

        private Node RemoveRecursive(Node node, int hashToRemove, int depth)
        {
            if (node == null)
                return null;

            // 3차원 축을 선택
            int axis = depth % 3;

            if (hashToRemove < node.point.Hash)
                node.left = RemoveRecursive(node.left, hashToRemove, depth + 1);
            else if (hashToRemove > node.point.Hash)
                node.right = RemoveRecursive(node.right, hashToRemove, depth + 1);
            else
            {
                if (node.right == null)
                    return node.left;
                if (node.left == null)
                    return node.right;

                node.point = new KDTreePosition(FindMin(node.right, axis));
                node.right = RemoveRecursive(node.right, node.point.Hash, depth + 1);
            }

            return node;
        }

        public void Remove(Vector3 pointToRemove)
        {
            root = RemoveRecursive(root, pointToRemove, 0);
        }

        private Node RemoveRecursive(Node node, Vector3 pointToRemove, int depth)
        {
            if (node == null)
                return null;

            // 3차원 축을 선택
            int axis = depth % 3;

            if (pointToRemove[axis] < node.point.Position[axis])
                node.left = RemoveRecursive(node.left, pointToRemove, depth + 1);
            else if (pointToRemove[axis] > node.point.Position[axis])
                node.right = RemoveRecursive(node.right, pointToRemove, depth + 1);
            else
            {
                if (node.right == null)
                    return node.left;
                if (node.left == null)
                    return node.right;

                node.point.Position = FindMin(node.right, axis);
                node.right = RemoveRecursive(node.right, node.point.Position, depth + 1);
            }

            return node;
        }

        private Vector3 FindMin(Node node, int axis)
        {
            while (node.left != null)
                node = node.left;
            return node.point.Position;
        }
    }
}

namespace KDTree
{
    public class KDTreePosition
    {
        public Vector4 Position;
        public int Hash;

        public KDTreePosition(Vector4 position)
        {
            Position = position;
            Hash = position.GetHashCode();
        }

        public KDTreePosition(Vector4 position, int hash)
        {
            Position = position;
            Hash = hash;
        }
    }

    public class Node
    {
        public KDTreePosition point;
        public Node left;
        public Node right;
        public bool isDelete = false;

        public Node(KDTreePosition point)
        {
            this.point = point;
            left = null;
            right = null;
        }
    }

    public class KDTree
    {
        private Node root;
        public List<Node> findNodes;
        private int m_axis = 3;

        public void Clear()
        {
            root = null;
        }

        //public KDTree(List<KDTreePosition> points)
        //{
        //    root = BuildTree(points, 0);
        //}

        public void Build(List<KDTreePosition> points)
        {
            root = BuildTree(points, 0);
        }

        private Node BuildTree(List<KDTreePosition> points, int depth)
        {
            if (points.Count == 0)
                return null;

            int axis = depth % m_axis;
            points.Sort((a, b) => a.Position[axis].CompareTo(b.Position[axis]));

            int medianIndex = points.Count / 2;
            Node node = new Node(points[medianIndex]);
            node.left = BuildTree(points.GetRange(0, medianIndex), depth + 1);
            node.right = BuildTree(points.GetRange(medianIndex + 1, points.Count - medianIndex - 1), depth + 1);

            return node;
        }

        public List<KDTreePosition> FindInRange(Vector4 target, float radius)
        {
            if (findNodes == null)
                findNodes = new List<Node>();
            else
                findNodes.Clear();

            List<KDTreePosition> result = new List<KDTreePosition>();
            SearchSubtree(root, target, radius, 0, result);
            return result;
        }

        private void SearchSubtree(Node node, Vector4 target, float radius, int depth, List<KDTreePosition> result)
        {
            if (node == null)
                return;

            int axis = depth % m_axis;
            float distance = Vector2.Distance(new Vector2(node.point.Position.x, node.point.Position.z),
                new Vector2(target.x, target.z));

            if (distance <= radius && !node.isDelete)
            {
                result.Add(node.point);
                findNodes.Add(node);
            }

            if (target[axis] < node.point.Position[axis])
                SearchSubtree(node.left, target, radius, depth + 1, result);
            else
                SearchSubtree(node.right, target, radius, depth + 1, result);

            if (Mathf.Abs(target[axis] - node.point.Position[axis]) <= radius)
            {
                if (target[axis] < node.point.Position[axis])
                    SearchSubtree(node.right, target, radius, depth + 1, result);
                else
                    SearchSubtree(node.left, target, radius, depth + 1, result);
            }
        }

        public List<KDTreePosition> SearchRange(Vector4 min, Vector4 max)
        {
            if (findNodes == null)
                findNodes = new List<Node>();
            else
                findNodes.Clear();

            List<KDTreePosition> result = new List<KDTreePosition>();
            SearchRange(root, min, max, 0, result);
            return result;
        }

        public List<KDTreePosition> SearchRange(Vector4 center, float rangeX, float rangeY, float rangeZ, float rangeW)
        {
            if (findNodes == null)
                findNodes = new List<Node>();
            else
                findNodes.Clear();

            Vector4 min = new Vector4(center.x - rangeX, center.y - rangeY, center.z - rangeZ, center.w - rangeW);
            Vector4 max = new Vector4(center.x + rangeX, center.y + rangeY, center.z + rangeZ, center.w + rangeW);

            List<KDTreePosition> result = new List<KDTreePosition>();
            SearchRange(root, min, max, 0, result);
            return result;
        }

        private void SearchRange(Node node, Vector4 min, Vector4 max, int depth, List<KDTreePosition> result)
        {
            if (node == null)
                return;

            int axis = depth % 4;

            if (node.point.Position.x >= min.x && node.point.Position.y >= min.y && 
                node.point.Position.z >= min.z && node.point.Position.w >= min.w &&
                node.point.Position.x <= max.x && node.point.Position.y <= max.y && 
                node.point.Position.z <= max.z && node.point.Position.w <= max.w)
            {
                result.Add(node.point);
                findNodes.Add(node);
            }

            if (node.point.Position[axis] >= min[axis])
                SearchRange(node.left, min, max, depth + 1, result);

            if (node.point.Position[axis] <= max[axis])
                SearchRange(node.right, min, max, depth + 1, result);
        }

        public void Delete(Node nodeToDelete)
        {
            nodeToDelete.isDelete = true;
            //root = Delete(root, nodeToDelete, 0);
        }

        private Node Delete(Node currentNode, Node nodeToDelete, int depth)
        {
            if (currentNode == null)
                return null;

            int axis = depth % m_axis;

            if (nodeToDelete == currentNode &&
                nodeToDelete.point == currentNode.point && 
                nodeToDelete.point.Hash == currentNode.point.Hash)
            {
                if (currentNode.right != null)
                {
                    Node successor = FindMin(currentNode.right, axis);
                    currentNode.point = successor.point;
                    currentNode.right = Delete(currentNode.right, successor, depth + 1);
                }
                else if (currentNode.left != null)
                {
                    Node successor = FindMin(currentNode.left, axis);
                    currentNode.point = successor.point;
                    currentNode.right = Delete(currentNode.left, successor, depth + 1);
                    currentNode.left = null;
                }
                else
                {
                    currentNode = null;
                    return null;
                }
            }
            else if (nodeToDelete.point.Position[axis] < currentNode.point.Position[axis])
            {
                currentNode.left = Delete(currentNode.left, nodeToDelete, depth + 1);
            }
            else
            {
                currentNode.right = Delete(currentNode.right, nodeToDelete, depth + 1);
            }

            return currentNode;
        }

        private Node FindMin(Node node, int axis)
        {
            while (node.left != null)
            {
                node = node.left;
            }
            return node;
        }

        /*public void Delete(KDTreePosition point)
        {
            root = Delete(root, point, 0);
        }

        private Node Delete(Node node, KDTreePosition point, int depth)
        {
            if (node == null)
                return null;

            int axis = depth % 4;

            if (point == node.point)
            {
                if (node.right != null)
                {
                    Node successor = FindMin(node.right, axis);
                    node.point = successor.point;
                    node.right = Delete(node.right, successor.point, depth + 1);
                }
                else if (node.left != null)
                {
                    Node successor = FindMin(node.left, axis);
                    node.point = successor.point;
                    node.right = Delete(node.left, successor.point, depth + 1);
                    node.left = null;
                }
                else
                {
                    return null;
                }
            }
            else if (point.Position[axis] < node.point.Position[axis])
            {
                node.left = Delete(node.left, point, depth + 1);
            }
            else
            {
                node.right = Delete(node.right, point, depth + 1);
            }

            return node;
        }*/
    }
}

namespace BalancedKDTree
{
    public class KDTreePosition
    {
        public Vector4 Position;
        public int Hash;

        public KDTreePosition(Vector4 position)
        {
            Position = position;
            Hash = position.GetHashCode();
        }

        public KDTreePosition(Vector4 position, int hash)
        {
            Position = position;
            Hash = hash;
        }
    }

    public class KDTreeNode
    {
        public KDTreePosition point;
        public KDTreeNode left;
        public KDTreeNode right;
    }

    public class KDTree
    {
        private KDTreeNode root;
        public List<KDTreeNode> findNode;

        public void Clear()
        {
            root = null;
        }

        public void BuildTree(List<KDTreePosition> points, int depth = 0)
        {
            if (points.Count == 0)
                return;

            // 4차원 축을 선택
            int axis = depth % 4;
            points.Sort((a, b) => a.Position[axis].CompareTo(b.Position[axis]));

            int medianIndex = points.Count / 2;
            root = new KDTreeNode
            {
                point = points[medianIndex],
                left = new KDTreeNode(),
                right = new KDTreeNode()
            };

            List<KDTreePosition> leftPoints = points.GetRange(0, medianIndex);
            List<KDTreePosition> rightPoints = points.GetRange(medianIndex + 1, points.Count - medianIndex - 1);

            BuildSubtree(root.left, leftPoints, depth + 1);
            BuildSubtree(root.right, rightPoints, depth + 1);
        }

        private void BuildSubtree(KDTreeNode node, List<KDTreePosition> points, int depth)
        {
            if (points.Count == 0)
                return;

            int axis = depth % 4;
            points.Sort((a, b) => a.Position[axis].CompareTo(b.Position[axis]));

            int medianIndex = points.Count / 2;
            node.point = points[medianIndex];

            List<KDTreePosition> leftPoints = points.GetRange(0, medianIndex);
            List<KDTreePosition> rightPoints = points.GetRange(medianIndex + 1, points.Count - medianIndex - 1);

            if (leftPoints.Count > 0)
            {
                node.left = new KDTreeNode();
                BuildSubtree(node.left, leftPoints, depth + 1);
            }

            if (rightPoints.Count > 0)
            {
                node.right = new KDTreeNode();
                BuildSubtree(node.right, rightPoints, depth + 1);
            }
        }

        public List<KDTreePosition> RangeQuery(Vector4 minRange, Vector4 maxRange)
        {
            List<KDTreePosition> result = new List<KDTreePosition>();
            RangeSearch(root, minRange, maxRange, 0, result);
            return result;
        }

        private void RangeSearch(KDTreeNode node, Vector4 minRange, Vector4 maxRange, int depth, List<KDTreePosition> result)
        {
            if (node == null)
                return;

            int axis = depth % 4;

            if (node.point.Position[axis] >= minRange[axis] && node.point.Position[axis] <= maxRange[axis])
            {
                if (node.point.Position.x >= minRange.x && node.point.Position.x <= maxRange.x &&
                    node.point.Position.y >= minRange.y && node.point.Position.y <= maxRange.y &&
                    node.point.Position.z >= minRange.z && node.point.Position.z <= maxRange.z &&
                    node.point.Position.w >= minRange.w && node.point.Position.w <= maxRange.w)
                {
                    result.Add(node.point);
                }

                RangeSearch(node.left, minRange, maxRange, depth + 1, result);
                RangeSearch(node.right, minRange, maxRange, depth + 1, result);
            }
            else if (node.point.Position[axis] > maxRange[axis])
            {
                RangeSearch(node.left, minRange, maxRange, depth + 1, result);
            }
            else
            {
                RangeSearch(node.right, minRange, maxRange, depth + 1, result);
            }
        }

        public List<KDTreePosition> RangeQuery(Vector4 center, float radius)
        {
            if (findNode == null)
                findNode = new List<KDTreeNode>();
            else
                findNode.Clear();
            List<KDTreePosition> result = new List<KDTreePosition>();
            RangeSearch(root, center, radius, 0, result);
            return result;
        }

        private void RangeSearch(KDTreeNode node, Vector4 center, float radius, int depth, List<KDTreePosition> result)
        {
            if (node == null)
                return;

            float distanceSquared = Vector4.SqrMagnitude(node.point.Position - center);

            if (distanceSquared <= radius * radius)
            {
                result.Add(node.point);
                findNode.Add(node);
            }

            int axis = depth % 4;

            if (node.point.Position[axis] >= center[axis] - radius)
            {
                RangeSearch(node.left, center, radius, depth + 1, result);
            }

            if (node.point.Position[axis] <= center[axis] + radius)
            {
                RangeSearch(node.right, center, radius, depth + 1, result);
            }
        }

        public void DeleteNode(KDTreePosition pointToDelete)
        {
            root = Delete(root, pointToDelete, 0);
        }

        private KDTreeNode Delete(KDTreeNode node, KDTreePosition pointToDelete, int depth)
        {
            if (node == null)
                return null;

            int axis = depth % 4;

            if (node.point == pointToDelete)
            {
                if (node.right != null)
                {
                    KDTreeNode minNode = FindMinNode(node.right, axis);
                    node.point = minNode.point;
                    node.right = Delete(node.right, minNode.point, depth + 1);
                }
                else if (node.left != null)
                {
                    KDTreeNode minNode = FindMinNode(node.left, axis);
                    node.point = minNode.point;
                    node.right = Delete(node.left, minNode.point, depth + 1);
                    node.left = null;
                }
                else
                {
                    return null;
                }
            }
            else if (pointToDelete.Position[axis] < node.point.Position[axis])
            {
                node.left = Delete(node.left, pointToDelete, depth + 1);
            }
            else
            {
                node.right = Delete(node.right, pointToDelete, depth + 1);
            }

            return node;
        }

        public void DeleteNode(KDTreeNode nodeToDelete)
        {
            root = Delete(root, nodeToDelete, 0);
        }

        private KDTreeNode Delete(KDTreeNode node, KDTreeNode nodeToDelete, int depth)
        {
            if (node == null)
                return null;

            int axis = depth % 4;

            if (node == nodeToDelete)
            {
                if (node.right != null)
                {
                    KDTreeNode minNode = FindMinNode(node.right, axis);
                    node.point = minNode.point;
                    node.right = Delete(node.right, minNode, depth + 1);
                }
                else if (node.left != null)
                {
                    KDTreeNode minNode = FindMinNode(node.left, axis);
                    node.point = minNode.point;
                    node.right = Delete(node.left, minNode, depth + 1);
                    node.left = null;
                }
                else
                {
                    return null;
                }
            }
            else if (nodeToDelete.point.Position[axis] < node.point.Position[axis])
            {
                node.left = Delete(node.left, nodeToDelete, depth + 1);
            }
            else
            {
                node.right = Delete(node.right, nodeToDelete, depth + 1);
            }

            return node;
        }

        private KDTreeNode FindMinNode(KDTreeNode node, int axis)
        {
            if (node == null)
                return null;

            if (node.left == null)
                return node;

            return FindMinNode(node.left, (axis + 1) % 4);
        }
    }
}