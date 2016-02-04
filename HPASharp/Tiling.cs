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

        public int Height
        {
            get
            {
                return Rows;
            }
        }

        public int Width
        {
            get
            {
                return Columns;
            }
        }

        public int Columns { get; set; }
        public int Rows { get; set; }
        public int MaxEdges { get; set; }

        public Graph<TilingNodeInfo, TilingEdgeInfo> Graph { get; set; }

        public Tiling(TileType tileType, int rows, int columns)
        {
            init(tileType, rows, columns);
        }

        private void init(TileType tileType, int rows, int columns)
        {
            this.TileType = tileType;
            this.MaxEdges = getMaxEdges(tileType);
            this.Rows = rows;
            this.Columns = columns;
            this.Graph = new Graph<TilingNodeInfo, TilingEdgeInfo>();
            this.createNodes();
            this.createEdges();
        }

        private static int getMaxEdges(TileType tileType)
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
                //assert(false);
                return 0;
        }

        // Create a new tiling as a copy of another tiling (just copying obstacles)
        public Tiling(Tiling tiling, int horizOrigin, int vertOrigin, int width, int height)
        {
            // init builds everything, except for the obstacles...
            init(tiling.TileType, height, width);
            // so we now put the obstacles in place
            for (int col = 0; col < width; col++)
            for (int row = 0; row < height; row++)
            {
                // get the local node
                int localNodeId = getNodeId(row, col);
                var localNodeInfo = Graph.GetNodeInfo(localNodeId);
                // get the initial tiling node
                int nodeId = tiling.getNodeId(vertOrigin + row, horizOrigin + col);
                var nodeInfo = tiling.Graph.GetNodeInfo(nodeId);
                // set obstacle for the local node
                if (nodeInfo.IsObstacle)
                {
                    localNodeInfo.IsObstacle = true;
                }
                else
                {
                    localNodeInfo.IsObstacle = false;
        //            m_storageStatistics.get("nodes").add(1);
                }
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

        public int getHeuristic(int start, int target)
        {
            int colStart = start % Columns;
            int colTarget = target % Columns;
            int rowStart = start / Columns;
            int rowTarget = target / Columns;
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

        public int getMinCost()
        {
            return 1;
        }

        public int NrAbsNodes
        {
            get
            {
                return Rows * Columns;
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int getNodeId(int row, int column)
        {
            return row * Columns + column;
        }

        public int getNumberNodes()
        {
            return Rows * Columns;
        }

        private const int NO_NODE = -1;

        public List<Successor> getSuccessors(int nodeId, int lastNodeId)
        {
            var result = new List<Successor>();
            var node = Graph.GetNode(nodeId);
            var nodeInfo = node.Info;
            if (nodeInfo.IsObstacle)
            {
                //assert(result.size() == 0);
                return result;
            }

            var edges = node.OutEdges;
            foreach (var edge in edges)
            {
                int targetNodeId = edge.TargetNodeId;
                var targetNode = Graph.GetNode(targetNodeId);
                var targetNodeInfo = targetNode.Info;
                if (!canJump(targetNodeId, nodeId))
                    continue;
                if (targetNodeInfo.IsObstacle)
                    continue;
                if (lastNodeId != NO_NODE)
                    if (pruneNode(targetNodeId, lastNodeId))
                        continue;

                result.Add(new Successor(targetNodeId, edge.Info.Cost));
            }

            return result;
        }

        public void setObstacles(float obstaclePercentage, bool avoidDiag = false)
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
                    if ( avoidDiag )
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

        public int getPathCost(List<int> path)
        {
            int cost = 0;
            switch (this.TileType)
            {
                case TileType.TILE:
                case TileType.OCTILE_UNICOST:
                    return Constants.COST_ONE*(path.Count - 1);
                case TileType.OCTILE:
                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        if (areAligned(path[i], path[i+1]))
                            cost += Constants.COST_ONE;
                        else
                            cost += Constants.SQRT2; // SQRT2
                    }
                    break;
                case TileType.HEX:
                    //cerr << "getPathCost() is not implemented for HEX\n";
                    return -1;
            }

            return cost;
        }

        public bool canJump(int p1, int p2)
        {
            if (this.TileType != TileType.OCTILE && this.TileType != TileType.OCTILE_UNICOST)
                return true;
            if (areAligned(p1, p2))
                return true;
            int nodeId12 = getNodeId(p1/Width, p2%Width);
            int nodeId21 = getNodeId(p2/Width, p1%Width);
            var nodeInfo12 = Graph.GetNodeInfo(nodeId12);
            var  nodeInfo21 = Graph.GetNodeInfo(nodeId21);
            if (nodeInfo12.IsObstacle && nodeInfo21.IsObstacle)
                return false;
            return true;
        }

        //typedef Graph<TilingNodeInfo, TilingEdgeInfo>::Edge TilingEdge;

        //typedef Graph<TilingNodeInfo, TilingEdgeInfo> TilingGraph;

        //typedef Graph<TilingNodeInfo, TilingEdgeInfo>::Node TilingNode;

        private void addOutEdge(int nodeId, int row, int col, int cost)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns)
                return;
            Graph.addOutEdge(nodeId, getNodeId(row, col), new TilingEdgeInfo(cost));
        }

        private bool conflictDiag(int row, int col, int roff, int coff)
        {
            // Avoid generating cofigurations like:
            //
            //    @   or   @
            //     @      @
            //
            // that favor one grid topology over another.
            if ((row + roff < 0) || (row + roff >= Rows) ||
                 (col + coff < 0) || (col + coff >= Columns))
                return false;

            if ((Graph.GetNodeInfo(getNodeId(row + roff, col + coff))).IsObstacle)
            {
                if (!Graph.GetNodeInfo(getNodeId(row, col + coff)).IsObstacle &&
                     !Graph.GetNodeInfo(getNodeId(row + roff, col)).IsObstacle)
                    return true;
            }
            return false;
        }

        private void createEdges()
        {
            for (int row = 0; row < Rows; ++row)
                for (int col = 0; col < Columns; ++col)
                {
                    int nodeId = getNodeId(row, col);
                    addOutEdge(nodeId, row - 1, col, Constants.COST_ONE);
                    addOutEdge(nodeId, row + 1, col, Constants.COST_ONE);
                    addOutEdge(nodeId, row, col - 1, Constants.COST_ONE);
                    addOutEdge(nodeId, row, col + 1, Constants.COST_ONE);
                    if (this.TileType == TileType.OCTILE)
                    {
                        addOutEdge(nodeId, row + 1, col + 1, Constants.SQRT2);
                        addOutEdge(nodeId, row + 1, col - 1, Constants.SQRT2);
                        addOutEdge(nodeId, row - 1, col + 1, Constants.SQRT2);
                        addOutEdge(nodeId, row - 1, col - 1, Constants.SQRT2);
                    }
                    else if (this.TileType == TileType.OCTILE_UNICOST)
                    {
                        addOutEdge(nodeId, row + 1, col + 1, Constants.COST_ONE);
                        addOutEdge(nodeId, row + 1, col - 1, Constants.COST_ONE);
                        addOutEdge(nodeId, row - 1, col + 1, Constants.COST_ONE);
                        addOutEdge(nodeId, row - 1, col - 1, Constants.COST_ONE);
                    }
                    else if (this.TileType == TileType.HEX)
                    {
                        if (col % 2 == 0)
                        {
                            addOutEdge(nodeId, row - 1, col + 1, Constants.COST_ONE);
                            addOutEdge(nodeId, row - 1, col - 1, Constants.COST_ONE);
                        }
                        else
                        {
                            addOutEdge(nodeId, row + 1, col + 1, Constants.COST_ONE);
                            addOutEdge(nodeId, row + 1, col - 1, Constants.COST_ONE);
                        }
                    }
                }
        }

        private void createNodes()
        {
            for (int row = 0; row < Rows; ++row)
            for (int col = 0; col < Columns; ++col)
            {
                int nodeId = getNodeId(row, col);
                Graph.addNode(nodeId, new TilingNodeInfo(false, row, col));
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
            for (int row = 0; row < Rows; ++row)
            {
                for (int col = 0; col < Columns; ++col)
                {
                    int nodeId = getNodeId(row, col);
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
