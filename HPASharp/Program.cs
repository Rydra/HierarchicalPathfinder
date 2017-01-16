using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Factories;
using HPASharp.Search;
using HPASharp.Smoother;

namespace HPASharp
{
	using System.Diagnostics;

	public class Program
    {
		public class FakePassability : IPassability
		{
			float obstaclePercentage = 0.20f;

			private bool[,] obstacles;

			public FakePassability(int width, int height)
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
        
		// Params
		private static readonly int Height = 70;
		private static readonly int Width = 70;
		private static readonly int ClusterSize = 8;
		private static readonly int MaxLevel = 2;
		private static readonly Position StartPosition = new Position(1, 0);
		private static readonly Position EndPosition = new Position(69, 69);

		public static void Main(string[] args)
        {
            // Prepare the abstract graph beforehand
			IPassability passability = new FakePassability(Width, Height);
            var tiling = TilingFactory.CreateTiling(Width, Height, passability);
            var wizard = new AbstractMapFactory();
			wizard.CreateHierarchicalMap(tiling, ClusterSize, MaxLevel, EntranceStyle.EndEntrance);
			var absTiling = wizard.HierarchicalMap;
			var sw = Stopwatch.StartNew();
            var path1 = RegularSearch(tiling);
	        var elapsed = sw.ElapsedMilliseconds;
			

			Console.WriteLine("Regular search: " + elapsed + " ms");
			Console.WriteLine($"{path1.Count} path nodes");
			PrintFormatted(tiling, absTiling, ClusterSize, path1);

			Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
			
			sw = Stopwatch.StartNew();
            var path2 = HierarchicalSearch(absTiling, MaxLevel, tiling);
	        elapsed = sw.ElapsedMilliseconds;
			Console.WriteLine("Hierachical search: " + elapsed + " ms");
			Console.WriteLine($"{path2.Count} path nodes");
			PrintFormatted(tiling, absTiling, ClusterSize, path2);

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }

	    private static List<Position> HierarchicalSearch(HierarchicalMap hierarchicalMap, int maxLevel, ConcreteMap concreteMap)
	    {
			// Hierarchical pathfinding
			var factory = new AbstractMapFactory();
			var startAbsNode = factory.InsertAbstractNode(hierarchicalMap, StartPosition, 0);
	        var targetAbsNode = factory.InsertAbstractNode(hierarchicalMap, EndPosition, 1);
	        var maxPathsToRefine = int.MaxValue;
            var hierarchicalSearch = new HierarchicalSearch();
            var abstractPath = hierarchicalSearch.DoHierarchicalSearch(hierarchicalMap, startAbsNode, targetAbsNode, maxLevel, maxPathsToRefine);
			var path = hierarchicalSearch.AbstractPathToLowLevelPath(hierarchicalMap, abstractPath, hierarchicalMap.Width, maxPathsToRefine);
			factory.RemoveAbstractNode(hierarchicalMap, targetAbsNode, 1);
			factory.RemoveAbstractNode(hierarchicalMap, startAbsNode, 0);
            //var smoother = new SmoothWizard(concreteMap, path);
            //path = smoother.SmoothPath();
		    return path.Select(p => concreteMap.Graph.GetNodeInfo(p.Id).Position).ToList();
			//return path.Select(n => n.Level == 0 ? concreteMap.Graph.GetNodeInfo(n.EntranceId).Position : hierarchicalMap.AbstractGraph.GetNodeInfo(n.EntranceId).Position).ToList();
        }

	    private static List<Position> RegularSearch(ConcreteMap concreteMap)
	    {
			var tilingGraph = concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(concreteMap.GetNodeIdFromPos(top, left));

			// Regular pathfinding
			var searcher = new AStar();
			var path = searcher.FindPath(concreteMap, getNode(StartPosition.X, StartPosition.Y).NodeId, getNode(EndPosition.X, EndPosition.Y).NodeId);
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

        public static void PrintFormatted(ConcreteMap concreteMap, HierarchicalMap hierarchicalGraph, int clusterSize, List<Position> path)
        {
            PrintFormatted(GetCharVector(concreteMap), concreteMap, hierarchicalGraph, clusterSize, path);
        }

        private static void PrintFormatted(List<char> chars, ConcreteMap concreteMap, HierarchicalMap hierarchicalGraph, int clusterSize, List<Position> path)
        {
            for (var y = 0; y < concreteMap.Height; ++y)
            {
                if (y % clusterSize == 0) Console.WriteLine("---------------------------------------------------------");
                for (var x = 0; x < concreteMap.Width; ++x)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    if (x % clusterSize == 0) Console.Write('|');

                    var nodeId = concreteMap.GetNodeIdFromPos(x, y);
                    var hasAbsNode = hierarchicalGraph.AbstractGraph.Nodes.FirstOrDefault(n => n.Info.ConcreteNodeId == nodeId);
                    
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
