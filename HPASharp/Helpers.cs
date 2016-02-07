using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public static class Helpers
    {
        public static int GetMaxEdges(TileType tileType)
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

        public static int GetHeuristic(Position start, Position target, TileType tileType)
        {
            var startX = start.X;
            var targetX = target.X;
            var startY = start.Y;
            var targetY = target.Y;
            var diffCol = Math.Abs(targetX - startX);
            var diffRow = Math.Abs(targetY - startY);
            switch (tileType)
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
        
        public static int GetPathCost(List<Position> path, TileType tileType)
        {
            var cost = 0;
            switch (tileType)
            {
                case TileType.TILE:
                case TileType.OCTILE_UNICOST:
                    return Constants.COST_ONE * (path.Count - 1);
                case TileType.OCTILE:
                    for (var i = 0; i < path.Count - 1; i++)
                    {
                        if (AreAligned(path[i], path[i + 1]))
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

        public static bool AreAligned(Position p1, Position p2)
        {
            return p1.X == p2.X || p1.Y == p2.Y;
        }
    }
}
