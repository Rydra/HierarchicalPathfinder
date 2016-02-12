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
        public static Tiling CreateTiling(int width, int height, IPassability passability, TileType tilingType = TileType.OCTILE)
        {
            var tiling = new Tiling(tilingType, width, height, passability);
            return tiling;
        }
    }
}
