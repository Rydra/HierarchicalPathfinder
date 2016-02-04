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
            var tilingType = TileType.TILE;
            var rows = 50;
            var columns = 50;
            var obstaclePercentage = 0.20f;
            var clusterSize = 8;
            var maxLevel = 2;
            Tiling tiling = new Tiling(tilingType, rows, columns);
            tiling.setObstacles(obstaclePercentage);
            tiling.printFormatted();
            

            var wizard = new AbsWizard(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
            wizard.AbstractMaze();
            var absTiling = wizard.AbsTiling;
            absTiling.printGraph();
        }
    }
}
