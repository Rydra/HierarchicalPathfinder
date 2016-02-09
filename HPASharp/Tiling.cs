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
        public TilingNodeInfo(bool isObstacle, Position position)
        {
            IsObstacle = isObstacle;
            Position = position;
        }

        public Position Position { get; set; }
        public bool IsObstacle { get; set; }
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
        public TileType TileType { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public int MaxEdges { get; set; }

        public Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; set; }

        public Tiling(TileType tileType, int width, int height)
        {
            this.Init(tileType, width, height);
        }
        
        // Create a new tiling as a copy of another tiling (just copying obstacles)
        public Tiling(Tiling tiling, int horizOrigin, int vertOrigin, int width, int height)
        {
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

        
        public int NrNodes => Width * Height;

        public Graph<TilingNodeInfo, TilingEdgeInfo>.Node this[int x, int y] => Graph.GetNode(GetNodeIdFromPos(x, y));

        private int GetNodeIdFromPos(int x, int y) => y*Width + x;

        public List<Neighbour> GetNeighbours(int nodeId, int lastNodeId)
        {
            var result = new List<Neighbour>();
            var node = Graph.GetNode(nodeId);
            var nodeInfo = node.Info;
            if (nodeInfo.IsObstacle)
            {
                return result;
            }

            var edges = node.OutEdges;
            foreach (var edge in edges)
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
            var edges = lastNode.OutEdges;
            return edges.Any(edge => edge.TargetNodeId == targetNodeId);
        }

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

        private void AddOutEdge(int nodeId, int x, int y, int cost)
        {
            if (y < 0 || y >= Height || x < 0 || x >= Width)
                return;

            Graph.AddOutEdge(nodeId, this[x, y].NodeId, new TilingEdgeInfo(cost));
        }

        private void CreateEdges()
        {
            for (var y = 0; y < Height; ++y)
                for (var x = 0; x < Width; ++x)
                {
                    var nodeId = this[x, y].NodeId;
                    this.AddOutEdge(nodeId, x, y - 1, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, x, y + 1, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, x - 1, y, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, x + 1, y, Constants.COST_ONE);
                    if (this.TileType == TileType.OCTILE)
                    {
                        this.AddOutEdge(nodeId, x + 1, y + 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, x - 1, y + 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, x + 1, y - 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, x - 1, y - 1, Constants.SQRT2);
                    }
                    else if (this.TileType == TileType.OCTILE_UNICOST)
                    {
                        this.AddOutEdge(nodeId, x + 1, y + 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, x - 1, y + 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, x + 1, y - 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, x - 1, y - 1, Constants.COST_ONE);
                    }
                    else if (this.TileType == TileType.HEX)
                    {
                        if (x % 2 == 0)
                        {
                            this.AddOutEdge(nodeId, x + 1, y - 1, Constants.COST_ONE);
                            this.AddOutEdge(nodeId, x - 1, y - 1, Constants.COST_ONE);
                        }
                        else
                        {
                            this.AddOutEdge(nodeId, x + 1, y + 1, Constants.COST_ONE);
                            this.AddOutEdge(nodeId, x - 1, y + 1, Constants.COST_ONE);
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
                Graph.AddNode(nodeId, new TilingNodeInfo(false, new Position(x, y)));
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
