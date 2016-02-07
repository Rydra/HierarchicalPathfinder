using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var tilingType = TileType.OCTILE;
            var rows = 50;
            var columns = 50;
            var obstaclePercentage = 0.20f;
            var clusterSize = 8;
            var maxLevel = 2;
            var tiling = new Tiling(tilingType, columns, rows);
            tiling.CreateObstacles(obstaclePercentage);
            tiling.printFormatted();
            

            var wizard = new AbstractMapFactory(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
            wizard.CreateAbstractMap();
            var absTiling = wizard.AbsTiling;
            absTiling.PrintGraph();
        }
    }
}
