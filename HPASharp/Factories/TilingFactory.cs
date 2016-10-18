using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    /// <summary>
    /// Constructs ConcreteMap objects
    /// </summary>
    public static class TilingFactory
    {
        public static ConcreteMap CreateTiling(int width, int height, IPassability passability, TileType tilingType = TileType.OCTILE)
        {
            var tiling = new ConcreteMap(tilingType, width, height, passability);
            return tiling;
        }
    }
}
