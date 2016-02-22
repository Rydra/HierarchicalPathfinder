using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Search;

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
        public List<PathNode> InitPath { get; set; }

        private Tiling tiling;

        // This is a dictionary, indexed by nodeId, that tells in which order does this node occupy in the path
        private Dictionary<int, int> pathMap;

        public SmoothWizard(Tiling tiling, List<PathNode> path)
        {
            InitPath = path;
            this.tiling = tiling;

            pathMap = new Dictionary<int, int>();
            for (var i = 0; i < InitPath.Count; i++)
                this.pathMap[InitPath[i].Id] = i + 1;
        }

        private Position GetPosition(int nodeId)
        {
            return tiling.Graph.GetNodeInfo(nodeId).Position;
        }

        public List<PathNode> SmoothPath()
        {
            var smoothedPath = new List<PathNode>(InitPath.Count * 2);
            for (var j = 0; j < InitPath.Count; j++)
            {
                var pathNode = InitPath[j];

                // Only process for smoothing points which belong to lvl 0. Anything above that is an abstract
                // path that cannot be smoothed.
                if (pathNode.Level > 0)
                {
                    smoothedPath.Add(pathNode);
                    continue;
                }

                if (smoothedPath.Count == 0)
                    smoothedPath.Add(InitPath[j]);

                // add this node to the smoothed path
                if (smoothedPath[smoothedPath.Count - 1].Id != InitPath[j].Id)
                {
                    // It's possible that, when smoothing, the next node that will be put in the path
                    // will not be adjacent. In those cases, since OpenRA requires a continuous path
                    // without breakings, we should calculate a new path for that section
                    var lastNodeInSmoothedPath = smoothedPath[smoothedPath.Count - 1];
                    var currentNodeInPath = InitPath[j];

                    if (!AreAdjacent(GetPosition(lastNodeInSmoothedPath.Id), GetPosition(currentNodeInPath.Id)))
                    {
                        var intrapath = GenerateIntermediateNodes(smoothedPath[smoothedPath.Count - 1].Id, InitPath[j].Id);
                        smoothedPath.AddRange(intrapath.Skip(1).Select(n => new PathNode(n, 0)));
                    }

                    smoothedPath.Add(InitPath[j]);
                }

                // This loops decides which is the next node of the path to consider in the next iteration (the j)
                for (var dir = (int)Direction.NORTH; dir <= (int)Direction.NW; dir++)
                {
                    if (this.tiling.TileType == TileType.TILE && dir > (int)Direction.WEST)
                        break;

                    var seenPathNode = AdvanceThroughDirection(InitPath[j].Id, dir);
                            
                    if (seenPathNode == Constants.NO_NODE)
                        // No node in advance in that direction, just continue
                        continue;
                    if (j > 0 && seenPathNode == InitPath[j - 1].Id)
                        // If the point we are advancing is the same as the previous one, we didn't
                        // improve at all. Just continue looking other directions
                        continue;
                    if (j < InitPath.Count - 1 && seenPathNode == InitPath[j + 1].Id)
                        // If the point we are advancing is the same as a next node in the path,
                        // we didn't improve either. Continue next direction
                        continue;
                        
                    j = pathMap[seenPathNode] - 2;

                    // count the path reduction (e.g., 2)
                    break;
                }
            }

            return smoothedPath;
        }

        private static bool AreAdjacent(Position a, Position b)
        {
            // if the Manhattan distance between a and b is > 2, then they are not 
            // (At least on OCTILE)
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= 2;
        }

        private List<int> GenerateIntermediateNodes(int nodeid1, int nodeid2)
        {
            var search = new AStar();
            search.FindPath(tiling, nodeid1, nodeid2);
            return search.Path;
        }

        /// <summary>
        /// Returns the next node in the init path in a straight line that
        /// lies in the same direction as the origin node
        /// </summary>
        private int AdvanceThroughDirection(int originId, int direction)
        {
            var nodeId = originId;
            var lastNodeId = originId;
            while (true)
            {
                // advance in the given direction
                nodeId = this.AdvanceNode(nodeId, direction);

                // If in the direction we advanced there was an invalid node or we cannot enter the node,
                // just return that no node was found
                if (nodeId == Constants.NO_NODE || !this.tiling.CanJump(GetPosition(nodeId), GetPosition(lastNodeId)))
                    return Constants.NO_NODE;

                // Otherwise, if the node we advanced was contained in the original path, and
                // it was positioned after the node we are analyzing, return it
                if (this.pathMap.ContainsKey(nodeId) && this.pathMap[nodeId] > this.pathMap[originId])
                {
                    return nodeId;
                }

                // If we have found an obstacle, just return that no next node to advance was found
                var newNodeInfo = this.tiling.Graph.GetNodeInfo(nodeId);
                if (newNodeInfo.IsObstacle)
                    return Constants.NO_NODE;

                lastNodeId = nodeId;
            }
        }

        private int AdvanceNode(int nodeId, int direction)
        {
            var nodeInfo = this.tiling.Graph.GetNodeInfo(nodeId);
            var y = nodeInfo.Position.Y;
            var x = nodeInfo.Position.X;
            switch ((Direction)direction)
            {
                case Direction.NORTH:
                    if (y == 0)
                        return Constants.NO_NODE;
                    return this.tiling[x, y - 1].NodeId;
                case Direction.EAST:
                    if (x == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling[x + 1, y].NodeId;
                case Direction.SOUTH:
                    if (y == this.tiling.Height - 1)
                        return Constants.NO_NODE;
                    return this.tiling[x, y + 1].NodeId;
                case Direction.WEST:
                    if (x == 0)
                        return Constants.NO_NODE;
                    return this.tiling[x - 1, y].NodeId;
                case Direction.NE:
                    if (y == 0 || x == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling[x + 1, y - 1].NodeId;
                case Direction.SE:
                    if (y == this.tiling.Height - 1 || x == this.tiling.Width - 1)
                        return Constants.NO_NODE;
                    return this.tiling[x + 1, y + 1].NodeId;
                case Direction.SW:
                    if (y == this.tiling.Height - 1 || x == 0)
                        return Constants.NO_NODE;
                    return this.tiling[x - 1, y + 1].NodeId;
                case Direction.NW:
                    if (y == 0 || x == 0)
                        return Constants.NO_NODE;
                    return this.tiling[x - 1, y - 1].NodeId;
                default:
                    return Constants.NO_NODE;
            }
        }
    }
}
