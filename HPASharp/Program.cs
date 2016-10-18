using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Search;
using HPASharp.Smoother;

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

			/// <summary>
			/// Creates obstacles in the map
			/// </summary>
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
            var height = 70;
            var width = 70;
            var clusterSize = 8;
            var maxLevel = 2;

            // Prepare the abstract graph beforehand
			IPassability passability = new Passability(width, height);
            var tiling = TilingFactory.CreateTiling(width, height, passability);
            var wizard = new AbstractMapFactory();
            wizard.CreateAbstractMap(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
            var absTiling = wizard.AbstractMap;

	        var sw = Stopwatch.StartNew();
            var path1 = RegularSearch(tiling, absTiling, clusterSize);
	        var elapsed = sw.ElapsedMilliseconds;

			Console.WriteLine("Regular search: " + elapsed + " ms");
			PrintFormatted(tiling, absTiling, clusterSize, path1);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

	        sw = Stopwatch.StartNew();
            var path2 = HierarchicalSearch(absTiling, maxLevel, tiling, clusterSize);
	        elapsed = sw.ElapsedMilliseconds;
			Console.WriteLine("Hierachical search: " + elapsed + " ms");
			PrintFormatted(tiling, absTiling, clusterSize, path2);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }

	    private static List<Position> HierarchicalSearch(AbstractMap abstractMap, int maxLevel, ConcreteMap concreteMap, int clusterSize)
	    {
            // Hierarchical pathfinding
	        var startAbsNode = abstractMap.InsertSTAL(new Position(0, 0), 0);
	        var targetAbsNode = abstractMap.InsertSTAL(new Position(69, 69), 1);
	        var maxPathsToRefine = int.MaxValue;
            var hierarchicalSearch = new HierarchicalSearch();
	        var hierarchicalmap = (HierarchicalMap) abstractMap;
            var abstractPath = hierarchicalSearch.DoHierarchicalSearch(hierarchicalmap, startAbsNode, targetAbsNode, maxLevel, maxPathsToRefine);
			var path = hierarchicalSearch.AbstractPathToLowLevelPath(hierarchicalmap, abstractPath, abstractMap.Width, maxPathsToRefine);
            abstractMap.RemoveStal(targetAbsNode, 1);
			abstractMap.RemoveStal(startAbsNode, 0);
            var smoother = new SmoothWizard(concreteMap, path);
            path = smoother.SmoothPath();

			return path.Select(n => n.Level == 0 ? concreteMap.Graph.GetNodeInfo(n.Id).Position : abstractMap.Graph.GetNodeInfo(n.Id).Position).ToList();
        }

	    private static List<Position> RegularSearch(ConcreteMap concreteMap, AbstractMap abstractMap, int clusterSize)
	    {
			var tilingGraph = concreteMap.Graph;
			Func<int, int, Graph<TilingNodeInfo, TilingEdgeInfo>.Node> getNode =
				(top, left) => tilingGraph.GetNode(concreteMap.GetNodeIdFromPos(top, left));

			// Regular pathfinding
			var searcher = new AStar();
			var path = searcher.FindPath(concreteMap, getNode(14, 20).NodeId, getNode(40, 40).NodeId);
	        var path2 = path.PathNodes;
		    return path2.Select(n => concreteMap.Graph.GetNodeInfo(n).Position).ToList();
	    }

	    private static List<char> GetCharVector(ConcreteMap concreteMap)
        {
            var result = new List<char>();
            var numberNodes = concreteMap.NrNodes;
            for (var i = 0; i < numberNodes; ++i)
            {
                result.Add(concreteMap.Graph.GetNodeInfo(i).IsObstacle ? '@' : '.');
            }

            return result;
        }

        public static void PrintFormatted(ConcreteMap concreteMap, AbstractMap abstractGraph, int clusterSize, List<Position> path)
        {
            PrintFormatted(GetCharVector(concreteMap), concreteMap, abstractGraph, clusterSize, path);
        }

        private static void PrintFormatted(List<char> chars, ConcreteMap concreteMap, AbstractMap abstractGraph, int clusterSize, List<Position> path)
        {
            for (var y = 0; y < concreteMap.Height; ++y)
            {
                if (y % clusterSize == 0) Console.WriteLine("---------------------------------------------------------");
                for (var x = 0; x < concreteMap.Width; ++x)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    if (x % clusterSize == 0) Console.Write('|');

                    var nodeId = concreteMap.GetNodeIdFromPos(x, y);
                    var hasAbsNode = abstractGraph.Graph.Nodes.FirstOrDefault(n => n.Info.CenterId == nodeId);
                    
                    if (hasAbsNode != null)
                        switch (hasAbsNode.Info.Level)
                        {
                            case 1: Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case 2: Console.ForegroundColor = ConsoleColor.DarkGreen;
                                break;
                        }
                        
                    Console.Write(path.Any(n => n.X == x && n.Y == y) ? 'X' : chars[nodeId]);
                }

                Console.WriteLine();
            }
        }
    }
}
