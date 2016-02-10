using System;
using System.Collections.Generic;

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
