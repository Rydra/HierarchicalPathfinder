using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Graph;
using HPASharp.Infrastructure;
using HPASharp.Search;

namespace HPASharp.Smoother
{
    public enum Direction
    {
        North,
        East,
        South,
        West,
        NorthEast,
        SouthEast,
        SouthWest,
        NorthWest
    }

    public class SmoothWizard
    {
        public List<PathNode> InitPath { get; set; }

        private readonly ConcreteMap _concreteMap;

        // This is a dictionary, indexed by nodeId, that tells in which order does this node occupy in the path
        private readonly Dictionary<int, int> _pathMap;

        public SmoothWizard(ConcreteMap concreteMap, List<PathNode> path)
        {
            InitPath = path;
            _concreteMap = concreteMap;

            _pathMap = new Dictionary<int, int>();
            for (var i = 0; i < InitPath.Count; i++)
                _pathMap[InitPath[i].Id] = i + 1;
        }

        private Position GetPosition(int nodeId)
        {
            return _concreteMap.Graph.GetNodeInfo(nodeId).Position;
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
                for (var dir = (int)Direction.North; dir <= (int)Direction.NorthWest; dir++)
                {
                    if (_concreteMap.TileType == TileType.Tile && dir > (int)Direction.West)
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
                        
                    j = _pathMap[seenPathNode] - 2;

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
            var path = search.FindPath(_concreteMap, nodeid1, nodeid2);
            return path.PathNodes;
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
                if (nodeId == Constants.NO_NODE || !this._concreteMap.CanJump(GetPosition(nodeId), GetPosition(lastNodeId)))
                    return Constants.NO_NODE;

                // Otherwise, if the node we advanced was contained in the original path, and
                // it was positioned after the node we are analyzing, return it
                if (this._pathMap.ContainsKey(nodeId) && this._pathMap[nodeId] > this._pathMap[originId])
                {
                    return nodeId;
                }

                // If we have found an obstacle, just return that no next node to advance was found
                var newNodeInfo = this._concreteMap.Graph.GetNodeInfo(nodeId);
                if (newNodeInfo.IsObstacle)
                    return Constants.NO_NODE;

                lastNodeId = nodeId;
            }
        }

        private int AdvanceNode(int nodeId, int direction)
        {
            var nodeInfo = this._concreteMap.Graph.GetNodeInfo(nodeId);
            var y = nodeInfo.Position.Y;
            var x = nodeInfo.Position.X;

			var tilingGraph = this._concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(_concreteMap.GetNodeIdFromPos(top, left));
			switch ((Direction)direction)
            {
                case Direction.North:
                    if (y == 0)
                        return Constants.NO_NODE;
                    return getNode(x, y - 1).NodeId;
                case Direction.East:
                    if (x == this._concreteMap.Width - 1)
                        return Constants.NO_NODE;
                    return getNode(x + 1, y).NodeId;
                case Direction.South:
                    if (y == this._concreteMap.Height - 1)
                        return Constants.NO_NODE;
                    return getNode(x, y + 1).NodeId;
                case Direction.West:
                    if (x == 0)
                        return Constants.NO_NODE;
                    return getNode(x - 1, y).NodeId;
                case Direction.NorthEast:
                    if (y == 0 || x == this._concreteMap.Width - 1)
                        return Constants.NO_NODE;
                    return getNode(x + 1, y - 1).NodeId;
                case Direction.SouthEast:
                    if (y == this._concreteMap.Height - 1 || x == this._concreteMap.Width - 1)
                        return Constants.NO_NODE;
                    return getNode(x + 1, y + 1).NodeId;
                case Direction.SouthWest:
                    if (y == this._concreteMap.Height - 1 || x == 0)
                        return Constants.NO_NODE;
                    return getNode(x - 1, y + 1).NodeId;
                case Direction.NorthWest:
                    if (y == 0 || x == 0)
                        return Constants.NO_NODE;
                    return getNode(x - 1, y - 1).NodeId;
                default:
                    return Constants.NO_NODE;
            }
        }
    }
}
