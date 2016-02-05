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
        public TilingNodeInfo()
        {
            IsObstacle = false;
            Row = -1;
            Column = -1;
        }

        public TilingNodeInfo(bool isObstacle, int row, int column)
        {
            IsObstacle = isObstacle;
            Row = row;
            Column = column;
        }

        public int Column { get; set; }
        public int Row { get; set; }
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

    public class Tiling : Environment
    {
        public TileType TileType { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public int MaxEdges { get; set; }

        public Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; set; }

        public Tiling(TileType tileType, int width, int height)
        {
            this.init(tileType, width, height);
        }

        private void init(TileType tileType, int width, int height)
        {
            this.TileType = tileType;
            this.MaxEdges = GetMaxEdges(tileType);
            this.Height = height;
            this.Width = width;
            this.Graph = new Graph<TilingNodeInfo, TilingEdgeInfo>();
            this.CreateNodes();
            this.CreateEdges();
        }

        private static int GetMaxEdges(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.HEX:
                    return 6;
                case TileType.OCTILE:
                case TileType.OCTILE_UNICOST:
                    return 8;
                case TileType.TILE:
                    return 4;
            }

            return 0;
        }

        // Create a new tiling as a copy of another tiling (just copying obstacles)
        public Tiling(Tiling tiling, int horizOrigin, int vertOrigin, int width, int height)
        {
            // init builds everything, except for the obstacles...
            this.init(tiling.TileType, width, height);

            // so we now put the obstacles in place
            for (var col = 0; col < width; col++)
            for (var row = 0; row < height; row++)
            {
                // get the local node
                var localNodeId = this.GetNodeId(row, col);
                var localNodeInfo = Graph.GetNodeInfo(localNodeId);
                // get the initial tiling node
                var nodeId = tiling.GetNodeId(vertOrigin + row, horizOrigin + col);
                var nodeInfo = tiling.Graph.GetNodeInfo(nodeId);
                // set obstacle for the local node
                localNodeInfo.IsObstacle = nodeInfo.IsObstacle;
            }
        }

        public void clearObstacles()
        {
            int numberNodes = getNumberNodes();
            for (int nodeId = 0; nodeId < numberNodes; ++nodeId)
            {
                var nodeInfo = Graph.GetNodeInfo(nodeId);
                nodeInfo.IsObstacle = false;
            }
        }

        public int GetHeuristic(int start, int target)
        {
            int colStart = start % Width;
            int colTarget = target % Width;
            int rowStart = start / Width;
            int rowTarget = target / Width;
            int diffCol = Math.Abs(colTarget - colStart);
            int diffRow = Math.Abs(rowTarget - rowStart);
            switch (this.TileType)
            {
            case TileType.HEX:
                // Vancouver distance
                // See P.Yap: Grid-based Path-Finding (LNAI 2338 pp.44-55)
                {
                    int correction = 0;
                    if (diffCol % 2 != 0)
                    {
                        if (rowTarget < rowStart)
                            correction = colTarget % 2;
                        else if (rowTarget > rowStart)
                            correction = colStart % 2;
                    }
                    // Note: formula in paper is wrong, corrected below.  
                    int dist = Math.Max(0, diffRow - diffCol / 2 - correction) + diffCol;
                    return dist * 1;
                }
            case TileType.OCTILE_UNICOST:
                return Math.Max(diffCol, diffRow) * 1;
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
                return minDiff * 34/24 + (maxDiff - minDiff) * 1; // 34 / 24 is SQRT 2
            case TileType.TILE:
                return (diffCol + diffRow) * 1;
            default:
                //assert(false);
                return 0;
            }
        }

        public int getMaxCost()
        {
            if (this.TileType == TileType.OCTILE)
                return 34 / 24;
            return 1;
        }

        public int GetMinCost()
        {
            return 1;
        }

        public int NrAbsNodes
        {
            get
            {
                return Width * Height;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int GetNodeId(int row, int column)
        {
            return row * Width + column;
        }

        public int getNumberNodes()
        {
            return Width * Height;
        }

        private const int NO_NODE = -1;

        public List<Neighbour> getSuccessors(int nodeId, int lastNodeId)
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
                int targetNodeId = edge.TargetNodeId;
                var targetNode = Graph.GetNode(targetNodeId);
                var targetNodeInfo = targetNode.Info;
                if (!this.CanJump(targetNodeId, nodeId))
                    continue;
                if (targetNodeInfo.IsObstacle)
                    continue;
                if (lastNodeId != NO_NODE)
                    if (pruneNode(targetNodeId, lastNodeId))
                        continue;

                result.Add(new Neighbour(targetNodeId, edge.Info.Cost));
            }

            return result;
        }

        public void CreateObstacles(float obstaclePercentage, bool avoidDiag = false)
        {
            var RAND_MAX = 0x7fff;
            var random = new Random();

            clearObstacles();
            int numberNodes = getNumberNodes();
            int numberObstacles = (int)(obstaclePercentage * numberNodes);
            for (int count = 0; count < numberObstacles; )
            {
                int nodeId = random.Next() / (RAND_MAX / numberNodes + 1) % (Width * Height);
                var nodeInfo = Graph.GetNodeInfo(nodeId);
                if (!nodeInfo.IsObstacle)
                {
                    if (avoidDiag)
                    {
                        int row = nodeInfo.Row;
                        int col = nodeInfo.Column;

                        if ( !conflictDiag(row,col,-1,-1) && 
                             !conflictDiag(row,col,-1,+1) &&
                             !conflictDiag(row,col,+1,-1) &&
                             !conflictDiag(row,col,+1,+1) )
                        {
                            nodeInfo.IsObstacle = true;
                            ++count;
                        }
                    }
                    else 
                    {
                        nodeInfo.IsObstacle = true;
                        ++count;
                    }
                }
            }
        }

        public int GetPathCost(List<int> path)
        {
            int cost = 0;
            switch (this.TileType)
            {
                case TileType.TILE:
                case TileType.OCTILE_UNICOST:
                    return Constants.COST_ONE*(path.Count - 1);
                case TileType.OCTILE:
                    for (var i = 0; i < path.Count - 1; i++)
                    {
                        if (areAligned(path[i], path[i+1]))
                            cost += Constants.COST_ONE;
                        else
                            cost += Constants.SQRT2;
                    }

                    break;
                case TileType.HEX:
                    //GetPathCost() is not implemented for HEX;
                    return -1;
            }

            return cost;
        }

        public bool CanJump(int p1, int p2)
        {
            if (this.TileType != TileType.OCTILE && this.TileType != TileType.OCTILE_UNICOST)
                return true;
            if (areAligned(p1, p2))
                return true;
            int nodeId12 = this.GetNodeId(p1/Width, p2%Width);
            int nodeId21 = this.GetNodeId(p2/Width, p1%Width);
            var nodeInfo12 = Graph.GetNodeInfo(nodeId12);
            var  nodeInfo21 = Graph.GetNodeInfo(nodeId21);
            if (nodeInfo12.IsObstacle && nodeInfo21.IsObstacle)
                return false;
            return true;
        }

        private void AddOutEdge(int nodeId, int row, int col, int cost)
        {
            if (row < 0 || row >= Height || col < 0 || col >= Width)
                return;

            Graph.AddOutEdge(nodeId, this.GetNodeId(row, col), new TilingEdgeInfo(cost));
        }

        private bool conflictDiag(int row, int col, int roff, int coff)
        {
            // Avoid generating cofigurations like:
            //
            //    @   or   @
            //     @      @
            //
            // that favor one grid topology over another.
            if ((row + roff < 0) || (row + roff >= Height) ||
                 (col + coff < 0) || (col + coff >= Width))
                return false;

            if ((Graph.GetNodeInfo(this.GetNodeId(row + roff, col + coff))).IsObstacle)
            {
                if (!Graph.GetNodeInfo(this.GetNodeId(row, col + coff)).IsObstacle &&
                     !Graph.GetNodeInfo(this.GetNodeId(row + roff, col)).IsObstacle)
                    return true;
            }

            return false;
        }

        private void CreateEdges()
        {
            for (int row = 0; row < Height; ++row)
                for (int col = 0; col < Width; ++col)
                {
                    int nodeId = this.GetNodeId(row, col);
                    this.AddOutEdge(nodeId, row - 1, col, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, row + 1, col, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, row, col - 1, Constants.COST_ONE);
                    this.AddOutEdge(nodeId, row, col + 1, Constants.COST_ONE);
                    if (this.TileType == TileType.OCTILE)
                    {
                        this.AddOutEdge(nodeId, row + 1, col + 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, row + 1, col - 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, row - 1, col + 1, Constants.SQRT2);
                        this.AddOutEdge(nodeId, row - 1, col - 1, Constants.SQRT2);
                    }
                    else if (this.TileType == TileType.OCTILE_UNICOST)
                    {
                        this.AddOutEdge(nodeId, row + 1, col + 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, row + 1, col - 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, row - 1, col + 1, Constants.COST_ONE);
                        this.AddOutEdge(nodeId, row - 1, col - 1, Constants.COST_ONE);
                    }
                    else if (this.TileType == TileType.HEX)
                    {
                        if (col % 2 == 0)
                        {
                            this.AddOutEdge(nodeId, row - 1, col + 1, Constants.COST_ONE);
                            this.AddOutEdge(nodeId, row - 1, col - 1, Constants.COST_ONE);
                        }
                        else
                        {
                            this.AddOutEdge(nodeId, row + 1, col + 1, Constants.COST_ONE);
                            this.AddOutEdge(nodeId, row + 1, col - 1, Constants.COST_ONE);
                        }
                    }
                }
        }

        private void CreateNodes()
        {
            for (int row = 0; row < Height; ++row)
            for (int col = 0; col < Width; ++col)
            {
                int nodeId = this.GetNodeId(row, col);
                Graph.AddNode(nodeId, new TilingNodeInfo(false, row, col));
            }
        }

        #region Printing

        List<char> getCharVector()
        {
            List<char> result = new List<char>();
            int numberNodes = getNumberNodes();
            for (int i = 0; i < numberNodes; ++i)
            {
                if (Graph.GetNodeInfo(i).IsObstacle)
                    result.Add('@');
                else
                    result.Add('.');
            }
            return result;
        }

        public void printFormatted()
        {
            printFormatted(getCharVector());
        }

        private void printFormatted(List<char> chars)
        {
            for (int row = 0; row < Height; ++row)
            {
                for (int col = 0; col < Width; ++col)
                {
                    int nodeId = this.GetNodeId(row, col);
                    Console.Write(chars[nodeId]);
                }

                Console.WriteLine();
            }
        }

        public void printFormatted(List<int> path)
        {
            var chars = getCharVector();
            if (path.Count > 0)
            {
                foreach (var i in path)
                {
                    chars[i] = 'x';
                }

                chars[path.First()] = 'T';
                chars[path.Last()] = 'S';
            }

            printFormatted(chars);
        }

        #endregion

        private bool pruneNode(int targetNodeId, int lastNodeId)
        {
            if (targetNodeId == lastNodeId)
                return true;
            if (this.TileType == TileType.TILE)
                return false;
            var lastNode = Graph.GetNode(lastNodeId);
            var edges = lastNode.OutEdges;
            foreach (var edge in edges)
            {
                if (edge.TargetNodeId == targetNodeId)
                    return true;
            }

            return false;
        }

        private bool areAligned(int p1, int p2)
        {
            if (p1 % Width == p2 % Width)
                return true;
            if (p1 / Width == p2 / Width)
                return true;
            return false;
        }


    }
}
