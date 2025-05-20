using UnityEngine;
using System.Collections.Generic;

public class SimpleGridPathfinding : MonoBehaviour
{
    public Vector2 gridWorldSize = new Vector2(20, 10);
    public float nodeRadius = 0.25f;
    public LayerMask unwalkableMask;
    private GridNode[,] grid;
    private float nodeDiameter;
    private int gridSizeX, gridSizeY;

    private void Awake()
    {
        nodeDiameter = nodeRadius * 2;
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);
        CreateGrid();
    }

    private void CreateGrid()
    {
        grid = new GridNode[gridSizeX, gridSizeY];
        Vector2 worldBottomLeft = (Vector2)transform.position - Vector2.right * gridWorldSize.x / 2 - Vector2.up * gridWorldSize.y / 2;
        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector2 worldPoint = worldBottomLeft + Vector2.right * (x * nodeDiameter + nodeRadius) + Vector2.up * (y * nodeDiameter + nodeRadius);
                bool walkable = !Physics2D.OverlapCircle(worldPoint, nodeRadius * 0.9f, unwalkableMask);
                grid[x, y] = new GridNode { gridPos = new Vector2Int(x, y), worldPos = worldPoint, walkable = walkable };
            }
        }
    }

    public List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos)
    {
        GridNode startNode = NodeFromWorldPoint(startPos);
        GridNode targetNode = NodeFromWorldPoint(targetPos);

        List<GridNode> openSet = new List<GridNode>();
        HashSet<GridNode> closedSet = new HashSet<GridNode>();
        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            GridNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || (openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost))
                    currentNode = openSet[i];
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
                return RetracePath(startNode, targetNode);

            foreach (GridNode neighbour in GetNeighbours(currentNode))
            {
                if (closedSet.Contains(neighbour))
                    continue;

                float newCostToNeighbour = currentNode.gCost + Vector2.Distance(currentNode.worldPos, neighbour.worldPos);
                if (newCostToNeighbour < neighbour.gCost || !openSet.Contains(neighbour))
                {
                    neighbour.gCost = newCostToNeighbour;
                    neighbour.hCost = Vector2.Distance(neighbour.worldPos, targetNode.worldPos);
                    neighbour.parent = currentNode;

                    if (!openSet.Contains(neighbour))
                        openSet.Add(neighbour);
                }
            }
        }
        return null; // ѕуть не найден
    }

    private List<Vector2> RetracePath(GridNode startNode, GridNode endNode)
    {
        List<Vector2> path = new List<Vector2>();
        GridNode currentNode = endNode;
        while (currentNode != startNode)
        {
            path.Add(currentNode.worldPos);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }

    public GridNode NodeFromWorldPoint(Vector2 worldPosition)
    {
        float percentX = Mathf.Clamp01((worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x);
        float percentY = Mathf.Clamp01((worldPosition.y + gridWorldSize.y / 2) / gridWorldSize.y);
        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        return grid[x, y];
    }

    public List<GridNode> GetNeighbours(GridNode node)
    {
        List<GridNode> neighbours = new List<GridNode>();
        int maxJumpHeight = 2;
        int maxStepHeight = 1;
        int maxDashLength = 3;

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (Mathf.Abs(x) == Mathf.Abs(y)) continue;
                int checkX = node.gridPos.x + x;
                int checkY = node.gridPos.y + y;
                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    var neighbour = grid[checkX, checkY];
                    if (neighbour.walkable)
                    {
                        int heightDiff = checkY - node.gridPos.y;
                        if (heightDiff > 0 && heightDiff <= maxStepHeight)
                            neighbour.moveType = "step";
                        else if (heightDiff > maxStepHeight && heightDiff <= maxJumpHeight)
                            neighbour.moveType = "jump";
                        else if (heightDiff == 0 || heightDiff < 0)
                            neighbour.moveType = "walk";
                        neighbours.Add(neighbour);
                    }
                }
            }
        }

        for (int dx = 1; dx <= maxDashLength; dx++)
        {
            int checkX = node.gridPos.x + dx;
            int checkY = node.gridPos.y;
            if (checkX < gridSizeX)
            {
                var dashNode = grid[checkX, checkY];
                bool pathClear = true;
                for (int i = 1; i <= dx; i++)
                    if (!grid[node.gridPos.x + i, checkY].walkable)
                        pathClear = false;
                if (pathClear)
                {
                    dashNode.moveType = "dash";
                    neighbours.Add(dashNode);
                }
            }
            checkX = node.gridPos.x - dx;
            if (checkX >= 0)
            {
                var dashNode = grid[checkX, checkY];
                bool pathClear = true;
                for (int i = 1; i <= dx; i++)
                    if (!grid[node.gridPos.x - i, checkY].walkable)
                        pathClear = false;
                if (pathClear)
                {
                    dashNode.moveType = "dash";
                    neighbours.Add(dashNode);
                }
            }
        }

        return neighbours;
    }

    public class GridNode
    {
        public Vector2Int gridPos;
        public Vector2 worldPos;
        public bool walkable;
        public GridNode parent;
        public float gCost, hCost;
        public float fCost => gCost + hCost;
        public string moveType;
    }
}
