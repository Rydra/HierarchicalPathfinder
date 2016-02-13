using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public interface IGrid
    {
        int Width { get; }
        int Height { get; }
        TileType TileType { get; }
        Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; }
    }

    public class Tiling : IMap, IGrid
    {
		public IPassability Passability { get; set; }

	    public TileType TileType { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public int MaxEdges { get; set; }

        public Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; set; }

        public Tiling(TileType tileType, int width, int height, IPassability passability)
        {
            Passability = passability;
            this.Init(tileType, width, height);
        }
        
        // Create a new tiling as a copy of another tiling (just copying obstacles)
        public Tiling(Tiling tiling, int horizOrigin, int vertOrigin, int width, int height, IPassability passability)
        {
	        this.Passability = passability;

            // init builds everything, except for the obstacles...
            this.Init(tiling.TileType, width, height);

            // so we now put the obstacles in place
            for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                // get the local node
                var localNodeInfo = this[x, y].Info;
                // get the initial tiling node
                var nodeInfo = tiling[horizOrigin + x, vertOrigin + y].Info;
                // set obstacle for the local node
                localNodeInfo.IsObstacle = nodeInfo.IsObstacle;
                localNodeInfo.Cost = nodeInfo.Cost;
            }
        }

        private void Init(TileType tileType, int width, int height)
        {
            this.TileType = tileType;
            this.MaxEdges = Helpers.GetMaxEdges(tileType);
            this.Height = height;
            this.Width = width;
            this.Graph = new Graph<TilingNodeInfo, TilingEdgeInfo>();
            this.CreateNodes();
            this.CreateEdges();
        }

	    public int NrNodes
	    {
		    get
		    {
			    return Width * Height;
		    }
	    }

	    public Graph<TilingNodeInfo, TilingEdgeInfo>.Node this[int x, int y]
	    {
		    get
		    {
			    return Graph.GetNode(GetNodeIdFromPos(x, y));
		    }
	    }

	    public int GetNodeIdFromPos(int x, int y)
	    {
		    return y*Width + x;
	    }

        public int GetHeuristic(int start, int target)
        {
            var startPos = Graph.GetNodeInfo(start).Position;
            var targetPos = Graph.GetNodeInfo(target).Position;

            var startX = startPos.X;
            var targetX = targetPos.X;
            var startY = startPos.Y;
            var targetY = targetPos.Y;
            var diffCol = Math.Abs(targetX - startX);
            var diffRow = Math.Abs(targetY - startY);
            switch (TileType)
            {
                case TileType.HEX:
                    // Vancouver distance
                    // See P.Yap: Grid-based Path-Finding (LNAI 2338 pp.44-55)
                    {
                        var correction = 0;
                        if (diffCol % 2 != 0)
                        {
                            if (targetY < startY)
                                correction = targetX % 2;
                            else if (targetY > startY)
                                correction = startX % 2;
                        }
                        // Note: formula in paper is wrong, corrected below.  
                        var dist = Math.Max(0, diffRow - diffCol / 2 - correction) + diffCol;
                        return dist * 1;
                    }
                case TileType.OCTILE_UNICOST:
                    return Math.Max(diffCol, diffRow) * Constants.COST_ONE;
                case TileType.OCTILE:
                    int maxDiff;
                    int minDiff;
                    if (diffCol > diffRow)
                    {
                        maxDiff = diffCol;
                        minDiff = diffRow;
                    }
                    else
                    {
                        maxDiff = diffRow;
                        minDiff = diffCol;
                    }
                    return minDiff * Constants.SQRT2 + (maxDiff - minDiff) * Constants.COST_ONE;
                case TileType.TILE:
                    return (diffCol + diffRow) * Constants.COST_ONE;
                default:
                    //assert(false);
                    return 0;
            }
        }

        public List<Neighbour> GetNeighbours(int nodeId, int lastNodeId)
        {
            var result = new List<Neighbour>();
            var node = Graph.GetNode(nodeId);
            var nodeInfo = node.Info;
            if (nodeInfo.IsObstacle)
            {
                return result;
            }

            foreach (var edge in node.Edges)
            {
                var targetNodeId = edge.TargetNodeId;
                var targetNode = Graph.GetNode(targetNodeId);
                var targetNodeInfo = targetNode.Info;
                if (!CanJump(targetNodeInfo.Position, nodeInfo.Position))
                    continue;
                if (targetNodeInfo.IsObstacle)
                    continue;
                if (lastNodeId != Constants.NO_NODE)
                    if (PruneNode(targetNodeId, lastNodeId))
                        continue;

                result.Add(new Neighbour(targetNodeId, edge.Info.Cost));
            }

            return result;
        }
        
        private bool PruneNode(int targetNodeId, int lastNodeId)
        {
            if (targetNodeId == lastNodeId)
                return true;
            if (TileType == TileType.TILE)
                return false;
            var lastNode = Graph.GetNode(lastNodeId);
            var edges = lastNode.Edges;
            return edges.Any(edge => edge.TargetNodeId == targetNodeId);
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
            var nodeInfo12 = this[p2.X, p1.Y].Info;
            var nodeInfo21 = this[p1.X, p2.Y].Info;
            if (nodeInfo12.IsObstacle && nodeInfo21.IsObstacle)
                return false;
            return true;
        }

        private void AddEdge(int nodeId, int x, int y, bool isDiag = false)
        {
            if (y < 0 || y >= Height || x < 0 || x >= Width)
                return;

            var cost = Graph.GetNodeInfo(this[x, y].NodeId).Cost;
            cost = isDiag ? (cost*34)/24 : cost;
            Graph.AddEdge(nodeId, this[x, y].NodeId, new TilingEdgeInfo(cost));
        }

        private void CreateEdges()
        {
            for (var y = 0; y < Height; ++y)
                for (var x = 0; x < Width; ++x)
                {
                    var nodeId = this[x, y].NodeId;

					this.AddEdge(nodeId, x, y - 1);
					this.AddEdge(nodeId, x, y + 1);
					this.AddEdge(nodeId, x - 1, y);
					this.AddEdge(nodeId, x + 1, y);
                    if (this.TileType == TileType.OCTILE)
                    {
						this.AddEdge(nodeId, x + 1, y + 1, true);
						this.AddEdge(nodeId, x - 1, y + 1, true);
						this.AddEdge(nodeId, x + 1, y - 1, true);
						this.AddEdge(nodeId, x - 1, y - 1, true);
                    }
                    else if (this.TileType == TileType.OCTILE_UNICOST)
                    {
						this.AddEdge(nodeId, x + 1, y + 1);
						this.AddEdge(nodeId, x - 1, y + 1);
						this.AddEdge(nodeId, x + 1, y - 1);
						this.AddEdge(nodeId, x - 1, y - 1);
                    }
                    else if (this.TileType == TileType.HEX)
                    {
                        if (x % 2 == 0)
                        {
							this.AddEdge(nodeId, x + 1, y - 1);
							this.AddEdge(nodeId, x - 1, y - 1);
                        }
                        else
                        {
							this.AddEdge(nodeId, x + 1, y + 1);
							this.AddEdge(nodeId, x - 1, y + 1);
                        }
                    }
                }
        }

        private void CreateNodes()
        {
            for (var y = 0; y < Height; ++y)
            for (var x = 0; x < Width; ++x)
            {
                var nodeId = GetNodeIdFromPos(x, y);
                var position = new Position(x, y);
                int movementCost;
                var isObstacle = !Passability.CanEnter(position, out movementCost);
                var info = new TilingNodeInfo(isObstacle, movementCost, new Position(x, y));
                    
                Graph.AddNode(nodeId, info);
            }
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
