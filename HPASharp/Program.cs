using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
	using System.Diagnostics;
	using System.Net.NetworkInformation;
	using System.Security.Cryptography.X509Certificates;

	public class Program
    {
		public class Passability : IPassability
		{
			float obstaclePercentage = 0.20f;

			private bool[,] obstacles;

			public Passability(int width, int height)
			{
				obstacles = new bool[width,height];
				CreateObstacles(obstaclePercentage, width, height, true);
			}

			private Random random = new Random(1000);

			public bool CanEnter(Position pos, out int cost)
			{
				cost = Constants.COST_ONE;
				return !obstacles[pos.X, pos.Y];
			}

			private void CreateObstacles(float obstaclePercentage, int width, int height, bool avoidDiag = false)
			{
				var RAND_MAX = 0x7fff;

				var numberNodes = width * height;
				var numberObstacles = (int)(obstaclePercentage * numberNodes);
				for (var count = 0; count < numberObstacles; )
				{
					var nodeId = random.Next() / (RAND_MAX / numberNodes + 1) % (width * height);
					var x = nodeId % width;
					var y = nodeId / width;
					if (!obstacles[x, y])
					{
						if (avoidDiag)
						{
							if (!ConflictDiag(y, x, -1, -1, width, height) &&
								 !ConflictDiag(y, x, -1, +1, width, height) &&
								 !ConflictDiag(y, x, +1, -1, width, height) &&
								 !ConflictDiag(y, x, +1, +1, width, height))
							{
								obstacles[x, y] = true;
								++count;
							}
						}
						else
						{
							obstacles[x, y] = true;
							++count;
						}
					}
				}
			}

			private bool ConflictDiag(int row, int col, int roff, int coff, int width, int height)
			{
				// Avoid generating cofigurations like:
				//
				//    @   or   @
				//     @      @
				//
				// that favor one grid topology over another.
				if ((row + roff < 0) || (row + roff >= height) ||
					 (col + coff < 0) || (col + coff >= width))
					return false;

				if (obstacles[col + coff, row + roff])
				{
					if (!obstacles[col + coff, row] &&
						 !obstacles[col, row + roff])
						return true;
				}

				return false;
			}
		}

        public static void Main(string[] args)
        {
            var height = 50;
            var width = 50;
            var clusterSize = 8;
            var maxLevel = 2;

			IPassability passability = new Passability(width, height);
            var tiling = TilingFactory.CreateTiling(width, height, passability);

            var wizard = new AbstractMapFactory(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
            wizard.CreateAbstractMap();
            var absTiling = wizard.AbsTiling;
	        var startAbsNode = absTiling.InsertSTAL(new Position(13, 20), 0);
            var targetAbsNode = absTiling.InsertSTAL(new Position(40, 37), 1);
            PrintFormatted(tiling, absTiling, clusterSize, new List<int>());
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            var abstractPath = absTiling.DoHierarchicalSearch(startAbsNode, targetAbsNode, 2);
            var path = absTiling.AbstractPathToLowLevelPath(abstractPath, absTiling.Width);
            PrintFormatted(tiling, absTiling, clusterSize, path);
            Console.WriteLine();
			Console.WriteLine("Press any key to quit...");
	        Console.ReadKey();
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

        public static void PrintFormatted(Tiling tiling, AbsTiling abstractGraph, int clusterSize, List<int> path)
        {
            PrintFormatted(GetCharVector(tiling), tiling, abstractGraph, clusterSize, path);
        }

        private static void PrintFormatted(List<char> chars, Tiling tiling, AbsTiling abstractGraph, int clusterSize, List<int> path)
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
                        
                    Console.Write(path.Any(n => n == nodeId) ? 'X' : chars[nodeId]);
                }

                Console.WriteLine();
            }
        }
    }
}
