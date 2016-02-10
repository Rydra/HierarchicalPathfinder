using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    /// <summary>
    /// Constructs Tiling objects
    /// </summary>
    public static class TilingFactory
    {
        public static Tiling CreateTiling(TileType tilingType, int width, int height, float obstaclePercentage)
        {
            var tiling = new Tiling(tilingType, width, height);
            CreateObstacles(tiling, obstaclePercentage);
            return tiling;
        }
        
        private static void CreateObstacles(Tiling tiling, float obstaclePercentage, bool avoidDiag = false)
        {
            var RAND_MAX = 0x7fff;
            var random = new Random();

            ClearObstacles(tiling);
            var numberNodes = tiling.NrNodes;
            var numberObstacles = (int)(obstaclePercentage * numberNodes);
            for (var count = 0; count < numberObstacles;)
            {
                var nodeId = random.Next() / (RAND_MAX / numberNodes + 1) % (tiling.Width * tiling.Height);
                var nodeInfo = tiling.Graph.GetNodeInfo(nodeId);
                if (!nodeInfo.IsObstacle)
                {
                    if (avoidDiag)
                    {
                        var y = nodeInfo.Position.Y;
                        var x = nodeInfo.Position.X;

                        if (!ConflictDiag(tiling, y, x, -1, -1) &&
                             !ConflictDiag(tiling, y, x, -1, +1) &&
                             !ConflictDiag(tiling, y, x, +1, -1) &&
                             !ConflictDiag(tiling, y, x, +1, +1))
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
        
        private static bool ConflictDiag(Tiling tiling, int row, int col, int roff, int coff)
        {
            // Avoid generating cofigurations like:
            //
            //    @   or   @
            //     @      @
            //
            // that favor one grid topology over another.
            if ((row + roff < 0) || (row + roff >= tiling.Height) ||
                 (col + coff < 0) || (col + coff >= tiling.Width))
                return false;

            if (tiling[col + coff, row + roff].Info.IsObstacle)
            {
                if (!tiling[col + coff, row].Info.IsObstacle &&
                     !tiling[col, row + roff].Info.IsObstacle)
                    return true;
            }

            return false;
        }
        
        public static void ClearObstacles(Tiling tiling)
        {
            var numberNodes = tiling.NrNodes;
            for (var nodeId = 0; nodeId < numberNodes; ++nodeId)
            {
                var nodeInfo = tiling.Graph.GetNodeInfo(nodeId);
                nodeInfo.IsObstacle = false;
            }
        }
    }
}
