using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Smoother
{
    public enum Direction
    {
        NORTH,
        EAST,
        SOUTH,
        WEST,
        NE,
        SE,
        SW,
        NW
    }

    public class SmoothWizard
    {
        public List<int> InitPath { get; set; } 
        public List<int> SmoothPath { get; set; } 

        public SmoothWizard(Tiling tiling, List<int> path)
        {
            InitPath = path;
            m_tiling = tiling;

            for (int i = 0; i < InitPath.Count; i++)
            {
                m_pathMap[InitPath[i]] = i + 1;
            }
        }

        public SmoothWizard()
        {
            
        }

        public void smoothPath()
        {
            if (m_tiling.getPathCost(InitPath) == m_tiling.getHeuristic(InitPath[0], InitPath[InitPath.Count - 1]))
            {
                SmoothPath = InitPath;
            }
            else
            {
                for (int j = 0; j < InitPath.Count; j++)
                {
                    // add this node to the smoothed path
                    if (SmoothPath.Count == 0)
                        SmoothPath.Add(InitPath[j]);
                    if (SmoothPath.Count > 0 && SmoothPath[SmoothPath.Count - 1] != InitPath[j])
                        SmoothPath.Add(InitPath[j]);
                    for (int dir = (int)Direction.NORTH; dir <= (int)Direction.NW; dir++)
                    {
                        if (m_tiling.TileType == TileType.TILE && dir > (int)Direction.WEST)
                            break;
                        int seenPathNode = getPathNodeId(InitPath[j], dir);
                        if (seenPathNode == Constants.NO_NODE)
                            continue;
                        if (j > 0 && seenPathNode == InitPath[j - 1])
                            continue;
                        if (j < InitPath.Count - 1 && seenPathNode == InitPath[j + 1])
                            continue;
                        j = m_pathMap[seenPathNode] - 2;
                        // count the path reduction (e.g., 2)
                        break;
                    }
                }
            }
        }
        
        private Tiling m_tiling;
        private int[] m_pathMap = new int[1000000];//262144];

        private int getPathNodeId(int originId, int direction)
        {
            int nodeId = originId;
            int lastNodeId = originId;
            while (true)
            {
                // advance in the given direction
                nodeId = advanceNode(nodeId, direction);
                if (nodeId == Constants.NO_NODE)
                    return Constants.NO_NODE;
                if (!m_tiling.canJump(nodeId, lastNodeId))
                    return Constants.NO_NODE;
                if (m_pathMap[nodeId] != Constants.NO_INDEX && m_pathMap[nodeId] > m_pathMap[originId])
                {
                    return nodeId;
                }
                var newNodeInfo = m_tiling.Graph.GetNodeInfo(nodeId);
                if (newNodeInfo.IsObstacle)
                {
                    return Constants.NO_NODE;
                }
                lastNodeId = nodeId;
            }
        }

        private int advanceNode(int nodeId, int direction)
        {
            var nodeInfo = m_tiling.Graph.GetNodeInfo(nodeId);
            int currentRow = nodeInfo.Row;
            int currentCol = nodeInfo.Column;
            switch((Direction)direction)
            {
                case Direction.NORTH:
                    if (currentRow == 0)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow - 1, currentCol);
                case Direction.EAST:
                    if (currentCol == m_tiling.Width - 1)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow, currentCol + 1);
                case Direction.SOUTH:
                    if (currentRow == m_tiling.Height - 1)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow + 1, currentCol);
                case Direction.WEST:
                    if (currentCol == 0)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow, currentCol - 1);
                case Direction.NE:
                    if (currentRow == 0 || currentCol == m_tiling.Width - 1)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow - 1, currentCol + 1);
                case Direction.SE:
                    if (currentRow == m_tiling.Height - 1 || currentCol == m_tiling.Width - 1)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow + 1, currentCol + 1);
                case Direction.SW:
                    if (currentRow == m_tiling.Height - 1 || currentCol == 0)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow + 1, currentCol - 1);
                case Direction.NW:
                    if (currentRow == 0 || currentCol == 0)
                        return Constants.NO_NODE;
                    return m_tiling.getNodeId(currentRow - 1, currentCol - 1);
                default:
                    return Constants.NO_NODE;
            }
        }
    }
}
