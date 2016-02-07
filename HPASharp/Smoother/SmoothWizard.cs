using System.Collections.Generic;

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
        public List<int> SmoothedPath { get; set; }

        private Tiling tiling;
        // TODO: Study the reason of why it was initialized so high -> private int[] pathMap = new int[1000000];//262144];
        private int[] pathMap;

        public SmoothWizard(Tiling tiling, List<int> path)
        {
            InitPath = path;
            this.tiling = tiling;

            this.pathMap = new int[InitPath.Count];
            for (var i = 0; i < InitPath.Count; i++)
            {
                this.pathMap[InitPath[i]] = i + 1;
            }
        }

        public void SmoothPath()
        {
            if (this.tiling.GetPathCost(InitPath) == this.tiling.GetHeuristic(InitPath[0], InitPath[InitPath.Count - 1]))
            {
                this.SmoothedPath = InitPath;
            }
            else
            {
                for (var j = 0; j < InitPath.Count; j++)
                {
                    // add this node to the smoothed path
                    if (this.SmoothedPath.Count == 0)
                        this.SmoothedPath.Add(InitPath[j]);
                    if (this.SmoothedPath.Count > 0 && this.SmoothedPath[this.SmoothedPath.Count - 1] != InitPath[j])
                        this.SmoothedPath.Add(InitPath[j]);
                    for (var dir = (int)Direction.NORTH; dir <= (int)Direction.NW; dir++)
                    {
                        if (this.tiling.TileType == TileType.TILE && dir > (int)Direction.WEST)
                            break;

                        var seenPathNode = this.GetPathNodeId(InitPath[j], dir);
                        if (seenPathNode == Constants.NO_NODE)
                            continue;
                        if (j > 0 && seenPathNode == InitPath[j - 1])
                            continue;
                        if (j < InitPath.Count - 1 && seenPathNode == InitPath[j + 1])
                            continue;

                        j = this.pathMap[seenPathNode] - 2;

                        // count the path reduction (e.g., 2)
                        break;
                    }
                }
            }
        }

        private int GetPathNodeId(int originId, int direction)
        {
            var nodeId = originId;
            var lastNodeId = originId;
            while (true)
            {
                // advance in the given direction
                nodeId = this.AdvanceNode(nodeId, direction);
                if (nodeId == Constants.NO_NODE)
                    return Constants.NO_NODE;
                if (!this.tiling.CanJump(nodeId, lastNodeId))
                    return Constants.NO_NODE;
                if (this.pathMap[nodeId] != Constants.NO_INDEX && this.pathMap[nodeId] > this.pathMap[originId])
                {
                    return nodeId;
                }

                var newNodeInfo = this.tiling.Graph.GetNodeInfo(nodeId);
                if (newNodeInfo.IsObstacle)
                {
                    return Constants.NO_NODE;
                }

                lastNodeId = nodeId;
            }
        }

        private int AdvanceNode(int nodeId, int direction)
        {
            var nodeInfo = this.tiling.Graph.GetNodeInfo(nodeId);
            var currentRow = nodeInfo.Row;
            var currentCol = nodeInfo.Column;
            switch ((Direction)direction)
            {
                case Direction.NORTH:
                    if (currentRow == 0)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol, currentRow - 1);
                case Direction.EAST:
                    if (currentCol == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol + 1, currentRow);
                case Direction.SOUTH:
                    if (currentRow == this.tiling.Height - 1)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol, currentRow + 1);
                case Direction.WEST:
                    if (currentCol == 0)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol - 1, currentRow);
                case Direction.NE:
                    if (currentRow == 0 || currentCol == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol + 1, currentRow - 1);
                case Direction.SE:
                    if (currentRow == this.tiling.Height - 1 || currentCol == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol + 1, currentRow + 1);
                case Direction.SW:
                    if (currentRow == this.tiling.Height - 1 || currentCol == 0)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol - 1, currentRow + 1);
                case Direction.NW:
                    if (currentRow == 0 || currentCol == 0)
                        return Constants.NO_NODE;
                    return this.tiling.GetNodeId(currentCol - 1, currentRow - 1);
                default:
                    return Constants.NO_NODE;
            }
        }
    }
}
