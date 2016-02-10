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
            var tiling = TilingFactory.CreateTiling(tilingType, columns, rows, obstaclePercentage);
            //tiling.PrintFormatted();

            var wizard = new AbstractMapFactory(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
            wizard.CreateAbstractMap();
            var absTiling = wizard.AbsTiling;
            PrintFormatted(tiling, absTiling, clusterSize);
            //absTiling.PrintGraph();
        }

        private static List<char> GetCharVector(Tiling tiling)
        {
            var result = new List<char>();
            var numberNodes = tiling.NrNodes;
            for (var i = 0; i < numberNodes; ++i)
            {
                result.Add(tiling.Graph.GetNodeInfo(i).IsObstacle ? '@' : '.');
            }

            return result;
        }

        public static void PrintFormatted(Tiling tiling, AbsTiling abstractGraph, int clusterSize)
        {
            PrintFormatted(GetCharVector(tiling), tiling, abstractGraph, clusterSize);
        }

        private static void PrintFormatted(List<char> chars, Tiling tiling, AbsTiling abstractGraph, int clusterSize)
        {
            for (var y = 0; y < tiling.Height; ++y)
            {
                if (y % clusterSize == 0) Console.WriteLine("---------------------------------------------------------");
                for (var x = 0; x < tiling.Width; ++x)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    if (x % clusterSize == 0) Console.Write('|');

                    var nodeId = tiling.GetNodeIdFromPos(x, y);
                    var hasAbsNode = abstractGraph.Graph.Nodes.FirstOrDefault(n => n.Info.CenterId == nodeId);
                    
                    if (hasAbsNode != null)
                        switch (hasAbsNode.Info.Level)
                        {
                            case 1: Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case 2: Console.ForegroundColor = ConsoleColor.DarkGreen;
                                break;
                        }
                        
                    Console.Write(chars[nodeId]);
                }

                Console.WriteLine();
            }
        }
    }
}
