using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Factories;

namespace HPASharp
{
    public class TilingEdgeInfo
    {
        public TilingEdgeInfo(int cost)
        {
            Cost = cost;
        }

        public int Cost { get; set; }
    }
    
    public class TilingNodeInfo
    {
        public TilingNodeInfo(bool isObstacle, int cost, Position position)
        {
            IsObstacle = isObstacle;
            Position = position;
            Cost = cost;
        }

        public Position Position { get; set; }
        public bool IsObstacle { get; set; }
	    public int Cost { get; set; }
    }

    public enum TileType
    {
        HEX,

        /** Octiles with cost 1 to adjacent and sqrt(2) to diagonal. */
        OCTILE,

        /** Octiles with uniform cost 1 to adjacent and diagonal. */
        OCTILE_UNICOST,

        TILE
    }

    public class ConcreteMap : IMap
    {
		public IPassability Passability { get; set; }

	    public TileType TileType { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public int MaxEdges { get; set; }

        public Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; set; }

        public ConcreteMap(TileType tileType, int width, int height, IPassability passability)
        {
            Passability = passability;
            this.Init(tileType, width, height);
        }
        
        // Create a new concreteMap as a copy of another concreteMap (just copying obstacles)
        public ConcreteMap Slice(int horizOrigin, int vertOrigin, int width, int height, IPassability passability)
        {
            var slicedConcreteMap = new ConcreteMap(this.TileType, width, height, passability);
			
			// so we now put the obstacles in place
			for (var x = 0; x < width; x++)
				for (var y = 0; y < height; y++)
				{
					// get the local node
					var localNodeInfo = slicedConcreteMap.Graph.GetNode(slicedConcreteMap.GetNodeIdFromPos(x, y)).Info;
					// get the initial concreteMap node
					var nodeInfo = this.Graph.GetNode(this.GetNodeIdFromPos(horizOrigin + x, vertOrigin + y)).Info;
					// set obstacle for the local node
					localNodeInfo.IsObstacle = nodeInfo.IsObstacle;
					localNodeInfo.Cost = nodeInfo.Cost;
				}

            return slicedConcreteMap;
		}

        private void Init(TileType tileType, int width, int height)
        {
            this.TileType = tileType;
            this.MaxEdges = Helpers.GetMaxEdges(tileType);
            this.Height = height;
            this.Width = width;
            this.Graph = GraphFactory.CreateGraph(width, height, this.Passability);
        }

	    public int NrNodes { get { return Width * Height; } }

	    public int GetNodeIdFromPos(int x, int y)
	    {
		    return y * Width + x;
	    }

        public int GetHeuristic(int startNodeId, int targetNodeId)
        {
            var startPos = Graph.GetNodeInfo(startNodeId).Position;
            var targetPos = Graph.GetNodeInfo(targetNodeId).Position;

            var startX = startPos.X;
            var targetX = targetPos.X;
            var startY = startPos.Y;
            var targetY = targetPos.Y;
            var diffX = Math.Abs(targetX - startX);
            var diffY = Math.Abs(targetY - startY);
            switch (TileType)
            {
                case TileType.HEX:
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
                case TileType.OCTILE_UNICOST:
                    return Math.Max(diffX, diffY) * Constants.COST_ONE;
                case TileType.OCTILE:
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

                case TileType.TILE:
                    return (diffX + diffY) * Constants.COST_ONE;
                default:
                    return 0;
            }
        }

        public IEnumerable<Neighbour> GetNeighbours(int nodeId)
        {
            var result = new List<Neighbour>();
            var node = Graph.GetNode(nodeId);
            var nodeInfo = node.Info;

            foreach (var edge in node.Edges)
            {
                var targetNodeId = edge.TargetNodeId;
                var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);
                if (CanJump(targetNodeInfo.Position, nodeInfo.Position) && !targetNodeInfo.IsObstacle)
                    result.Add(new Neighbour(targetNodeId, edge.Info.Cost));
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
            if (TileType != TileType.OCTILE && this.TileType != TileType.OCTILE_UNICOST)
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
                result.Add(Graph.GetNodeInfo(i).IsObstacle ? '@' : '.');

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
                    Console.Write(chars[nodeId]);
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

                chars[path.First()] = 'T';
                chars[path.Last()] = 'S';
            }

            PrintFormatted(chars);
        }

        #endregion
    }
}
