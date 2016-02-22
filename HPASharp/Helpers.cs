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

        public static bool AreAligned(Position p1, Position p2)
        {
            return p1.X == p2.X || p1.Y == p2.Y;
        }
    }
}
