using System;
using System.Collections.Generic;
using HPASharp.Factories;
using HPASharp.Graph;
using HPASharp.Infrastructure;

namespace HPASharp
{

    public enum TileType
    {
        Hex,
        /** Octiles with cost 1 to adjacent and sqrt(2) to diagonal. */
        Octile,
        /** Octiles with uniform cost 1 to adjacent and diagonal. */
        OctileUnicost,
        Tile
    }

    public class ConcreteMap : IMap<ConcreteNode>
    {
		public IPassability Passability { get; set; }

	    public TileType TileType { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public int MaxEdges { get; set; }

        public ConcreteGraph Graph { get; set; }

        public ConcreteMap(TileType tileType, int width, int height, IPassability passability)
        {
            Passability = passability;
			TileType = tileType;
			MaxEdges = Helpers.GetMaxEdges(tileType);
			Height = height;
			Width = width;
			Graph = GraphFactory.CreateGraph(width, height, Passability);
		}

        // Create a new concreteMap as a copy of another concreteMap (just copying obstacles)
        public ConcreteMap Slice(int horizOrigin, int vertOrigin, int width, int height, IPassability passability)
        {
            var slicedConcreteMap = new ConcreteMap(this.TileType, width, height, passability);

	        foreach (var slicedMapNode in slicedConcreteMap.Graph.Nodes)
	        {
		        var globalConcreteNode =
			        Graph.GetNode(GetNodeIdFromPos(horizOrigin + slicedMapNode.Info.Position.X,
				        vertOrigin + slicedMapNode.Info.Position.Y));
				slicedMapNode.Info.IsObstacle = globalConcreteNode.Info.IsObstacle;
				slicedMapNode.Info.Cost = globalConcreteNode.Info.Cost;
			}

            return slicedConcreteMap;
		}

	    public int NrNodes => Width * Height;

        public Id<ConcreteNode> GetNodeIdFromPos(int x, int y)
	    {
		    return Id<ConcreteNode>.From(y * Width + x);
	    }

        public int GetHeuristic(Id<ConcreteNode> startNodeId, Id<ConcreteNode> targetNodeId)
        {
            var startPosition = Graph.GetNodeInfo(startNodeId).Position;
            var targetPosition = Graph.GetNodeInfo(targetNodeId).Position;

            var startX = startPosition.X;
            var targetX = targetPosition.X;
            var startY = startPosition.Y;
            var targetY = targetPosition.Y;
            var diffX = Math.Abs(targetX - startX);
            var diffY = Math.Abs(targetY - startY);
            switch (TileType)
            {
                case TileType.Hex:
                    // Vancouver distance
                    // See P.Yap: Grid-based Path-Finding (LNAI 2338 pp.44-55)
                    {
                        var correction = 0;
                        if (diffX % 2 != 0)
                        {
                            if (targetY < startY)
                                correction = targetX % 2;
                            else if (targetY > startY)
                                correction = startX % 2;
                        }

                        // Note: formula in paper is wrong, corrected below.  
                        var dist = Math.Max(0, diffY - diffX / 2 - correction) + diffX;
                        return dist * 1;
                    }
                case TileType.OctileUnicost:
                    return Math.Max(diffX, diffY) * Constants.COST_ONE;
                case TileType.Octile:
                    int maxDiff;
                    int minDiff;
                    if (diffX > diffY)
                    {
                        maxDiff = diffX;
                        minDiff = diffY;
                    }
                    else
                    {
                        maxDiff = diffY;
                        minDiff = diffX;
                    }

                    return (minDiff * Constants.COST_ONE * 34) / 24 + (maxDiff - minDiff) * Constants.COST_ONE;

                case TileType.Tile:
                    return (diffX + diffY) * Constants.COST_ONE;
                default:
                    return 0;
            }
        }

        public IEnumerable<Connection<ConcreteNode>> GetConnections(Id<ConcreteNode> nodeId)
        {
            var result = new List<Connection<ConcreteNode>>();
            var node = Graph.GetNode(nodeId);
            var nodeInfo = node.Info;

            foreach (var edge in node.Edges.Values)
            {
                var targetNodeId = edge.TargetNodeId;
                var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);
                if (CanJump(targetNodeInfo.Position, nodeInfo.Position) && !targetNodeInfo.IsObstacle)
                    result.Add(new Connection<ConcreteNode>(targetNodeId, edge.Info.Cost));
            }

            return result;
        }

        /// <summary>
        /// Tells whether we can move from p1 to p2 in line. Bear in mind
        /// this function does not consider intermediate points (it is
        /// assumed you can jump between intermediate points)
        /// </summary>
        public bool CanJump(Position p1, Position p2)
        {
            if (TileType != TileType.Octile && this.TileType != TileType.OctileUnicost)
                return true;
            if (Helpers.AreAligned(p1, p2))
                return true;

			// The following piece of code existed in the original implementation.
			// It basically checks that you do not forcefully cross a blocked diagonal.
			// Honestly, this is weird, bad designed and supposes that each position is adjacent to each other.
            var nodeInfo12 = Graph.GetNode(GetNodeIdFromPos(p2.X, p1.Y)).Info;
            var nodeInfo21 = Graph.GetNode(GetNodeIdFromPos(p1.X, p2.Y)).Info;
            return !(nodeInfo12.IsObstacle && nodeInfo21.IsObstacle);
        }
		
        #region Printing

        private List<char> GetCharVector()
        {
            var result = new List<char>();
            var numberNodes = NrNodes;
            for (var i = 0; i < numberNodes; ++i)
                result.Add(Graph.GetNodeInfo(Id<ConcreteNode>.From(i)).IsObstacle ? '@' : '.');

            return result;
        }

        public void PrintFormatted()
        {
            PrintFormatted(GetCharVector());
        }

        private void PrintFormatted(List<char> chars)
        {
            for (var y = 0; y < Height; ++y)
            {
                for (var x = 0; x < Width; ++x)
                {
                    var nodeId = this.GetNodeIdFromPos(x, y);
                    Console.Write(chars[nodeId.IdValue]);
                }

                Console.WriteLine();
            }
        }

        public void PrintFormatted(List<int> path)
        {
            var chars = GetCharVector();
            if (path.Count > 0)
            {
                foreach (var i in path)
                {
                    chars[i] = 'x';
                }

                chars[path[0]] = 'T';
                chars[path[path.Count - 1]] = 'S';
            }

            PrintFormatted(chars);
        }

        #endregion
    }
}
