using System;
using System.Collections.Generic;
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
        public List<IPathNode> InitialPath { get; set; }
	    private static readonly Id<ConcreteNode> INVALID_ID = Id<ConcreteNode>.From(Constants.NO_NODE);

        private readonly ConcreteMap _concreteMap;

        // This is a dictionary, indexed by nodeId, that tells in which order does this node occupy in the path
        private readonly Dictionary<int, int> _pathMap;

        public SmoothWizard(ConcreteMap concreteMap, List<IPathNode> path)
        {
            InitialPath = path;
            _concreteMap = concreteMap;

            _pathMap = new Dictionary<int, int>();
	        for (var i = 0; i < InitialPath.Count; i++)
	        {
			    _pathMap[InitialPath[i].IdValue] = i + 1;
			}
        }

        private Position GetPosition(Id<ConcreteNode> nodeId)
        {
            return _concreteMap.Graph.GetNodeInfo(nodeId).Position;
        }

        public List<IPathNode> SmoothPath()
        {
			var smoothedPath = new List<IPathNode>();
            var smoothedConcretePath = new List<ConcretePathNode>();
			var index = 0;
            for (; index < InitialPath.Count && InitialPath[index] is ConcretePathNode; index++)
            {
				var pathNode = (ConcretePathNode)InitialPath[index];
				if (smoothedConcretePath.Count == 0)
					smoothedConcretePath.Add(pathNode);

                // add this node to the smoothed path
                if (smoothedConcretePath[smoothedConcretePath.Count - 1].Id != pathNode.Id)
                {
                    // It's possible that, when smoothing, the next node that will be put in the path
                    // will not be adjacent. In those cases, since OpenRA requires a continuous path
                    // without breakings, we should calculate a new path for that section
                    var lastNodeInSmoothedPath = smoothedConcretePath[smoothedConcretePath.Count - 1];
                    var currentNodeInPath = pathNode;

                    if (!AreAdjacent(GetPosition(lastNodeInSmoothedPath.Id), GetPosition(currentNodeInPath.Id)))
                    {
                        var intermediatePath = GenerateIntermediateNodes(smoothedConcretePath[smoothedConcretePath.Count - 1].Id, pathNode.Id);
	                    for (int i = 1; i < intermediatePath.Count; i++)
	                    {
							smoothedConcretePath.Add(new ConcretePathNode(intermediatePath[i]));
						}
                    }

					smoothedConcretePath.Add(pathNode);
                }

                index = DecideNextNodeToConsider(index);
            }

	        foreach (var pathNode in smoothedConcretePath)
	        {
				smoothedPath.Add(pathNode);
			}

	        for (;index < InitialPath.Count; index++)
		    {
				smoothedPath.Add(InitialPath[index]);
			}

			return smoothedPath;
        }

	    private int DecideNextNodeToConsider(int index)
	    {
		    var newIndex = index;
		    for (var dir = (int) Direction.North; dir <= (int) Direction.NorthWest; dir++)
		    {
			    if (_concreteMap.TileType == TileType.Tile && dir > (int) Direction.West)
				    break;

			    var seenPathNode = AdvanceThroughDirection(Id<ConcreteNode>.From(InitialPath[index].IdValue), dir);

			    if (seenPathNode == INVALID_ID)
				    // No node in advance in that direction, just continue
				    continue;
			    if (index > 0 && seenPathNode.IdValue == InitialPath[index - 1].IdValue)
				    // If the point we are advancing is the same as the previous one, we didn't
				    // improve at all. Just continue looking other directions
				    continue;
			    if (index < InitialPath.Count - 1 && seenPathNode.IdValue == InitialPath[index + 1].IdValue)
				    // If the point we are advancing is the same as a next node in the path,
				    // we didn't improve either. Continue next direction
				    continue;

				newIndex = _pathMap[seenPathNode.IdValue] - 2;

			    // count the path reduction (e.g., 2)
			    break;
		    }

		    return newIndex;
	    }

	    private static bool AreAdjacent(Position a, Position b)
        {
            // if the Manhattan distance between a and b is > 2, then they are not 
            // (At least on OCTILE)
            return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) <= 2;
        }

        private List<Id<ConcreteNode>> GenerateIntermediateNodes(Id<ConcreteNode> nodeid1, Id<ConcreteNode> nodeid2)
        {
            var search = new AStar<ConcreteNode>(_concreteMap, nodeid1, nodeid2);
            var path = search.FindPath();
	        return path.PathNodes;
        }

        /// <summary>
        /// Returns the next node in the init path in a straight line that
        /// lies in the same direction as the origin node
        /// </summary>
        private Id<ConcreteNode> AdvanceThroughDirection(Id<ConcreteNode> originId, int direction)
        {
            var nodeId = originId;
            var lastNodeId = originId;
            while (true)
            {
                // advance in the given direction
                nodeId = AdvanceNode(nodeId, direction);

                // If in the direction we advanced there was an invalid node or we cannot enter the node,
                // just return that no node was found
                if (nodeId == INVALID_ID || !_concreteMap.CanJump(GetPosition(nodeId), GetPosition(lastNodeId)))
                    return INVALID_ID;

                // Otherwise, if the node we advanced was contained in the original path, and
                // it was positioned after the node we are analyzing, return it
                if (_pathMap.ContainsKey(nodeId.IdValue) && _pathMap[nodeId.IdValue] > _pathMap[originId.IdValue])
                {
                    return nodeId;
                }

                // If we have found an obstacle, just return that no next node to advance was found
                var newNodeInfo = _concreteMap.Graph.GetNodeInfo(nodeId);
                if (newNodeInfo.IsObstacle)
                    return INVALID_ID;

                lastNodeId = nodeId;
            }
        }

        private Id<ConcreteNode> AdvanceNode(Id<ConcreteNode> nodeId, int direction)
        {
            var nodeInfo = _concreteMap.Graph.GetNodeInfo(nodeId);
            var y = nodeInfo.Position.Y;
            var x = nodeInfo.Position.X;

			var tilingGraph = _concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(_concreteMap.GetNodeIdFromPos(top, left));
			switch ((Direction)direction)
            {
                case Direction.North:
                    if (y == 0)
                        return INVALID_ID;
                    return getNode(x, y - 1).NodeId;
                case Direction.East:
                    if (x == _concreteMap.Width - 1)
                        return INVALID_ID;
                    return getNode(x + 1, y).NodeId;
                case Direction.South:
                    if (y == _concreteMap.Height - 1)
                        return INVALID_ID;
                    return getNode(x, y + 1).NodeId;
                case Direction.West:
                    if (x == 0)
                        return INVALID_ID;
                    return getNode(x - 1, y).NodeId;
                case Direction.NorthEast:
                    if (y == 0 || x == _concreteMap.Width - 1)
                        return INVALID_ID;
                    return getNode(x + 1, y - 1).NodeId;
                case Direction.SouthEast:
                    if (y == _concreteMap.Height - 1 || x == _concreteMap.Width - 1)
                        return INVALID_ID;
                    return getNode(x + 1, y + 1).NodeId;
                case Direction.SouthWest:
                    if (y == _concreteMap.Height - 1 || x == 0)
                        return INVALID_ID;
                    return getNode(x - 1, y + 1).NodeId;
                case Direction.NorthWest:
                    if (y == 0 || x == 0)
                        return INVALID_ID;
                    return getNode(x - 1, y - 1).NodeId;
                default:
                    return INVALID_ID;
            }
        }
    }
}
