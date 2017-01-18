using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Factories;
using HPASharp.Graph;
using HPASharp.Infrastructure;
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

		private static readonly Position StartPosition = new Position(1, 0);
		private static readonly Position EndPosition = new Position(69, 69);

		//private static readonly int Height = 16;
		//private static readonly int Width = 16;

		//private static readonly Position StartPosition = new Position(1, 0);
		//private static readonly Position EndPosition = new Position(15, 15);

		private static readonly int ClusterSize = 8;
		private static readonly int MaxLevel = 2;

		public static void Main(string[] args)
        {
            // Prepare the abstract graph beforehand
			IPassability passability = new FakePassability(Width, Height);
            var tiling = TilingFactory.CreateTiling(Width, Height, passability);
            var wizard = new AbstractMapFactory();
			wizard.CreateHierarchicalMap(tiling, ClusterSize, MaxLevel, EntranceStyle.EndEntrance);
			var absTiling = wizard.HierarchicalMap;

			var watch = Stopwatch.StartNew();
            var regularSearchPath = RegularSearch(tiling);
	        var regularSearchTime = watch.ElapsedMilliseconds;

            watch = Stopwatch.StartNew();
            var hierarchicalSearchPath = HierarchicalSearch(absTiling, MaxLevel, tiling);
            var hierarchicalSearchTime = watch.ElapsedMilliseconds;

#if !DEBUG
            Console.WriteLine("Regular search: " + regularSearchTime + " ms");
            Console.WriteLine("Number of nodes: " + regularSearchPath.Count);

            Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
            Console.WriteLine("Number of nodes: " + hierarchicalSearchPath.Count);
#endif

#if DEBUG
            Console.WriteLine("Regular search: " + regularSearchTime + " ms");
			Console.WriteLine($"{regularSearchPath.Count} path nodes");
			PrintFormatted(tiling, absTiling, ClusterSize, regularSearchPath);
			Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
			Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
			Console.WriteLine($"{hierarchicalSearchPath.Count} path nodes");
			PrintFormatted(tiling, absTiling, ClusterSize, hierarchicalSearchPath);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

			Console.WriteLine("Press any key to quit...");
			Console.ReadKey();
#endif

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

			var smoother = new SmoothWizard(concreteMap, path);
			path = smoother.SmoothPath();
			var posPath = path.Select(p =>
		    {
			    if (p is ConcretePathNode)
			    {
				    var concretePathNode = (ConcretePathNode) p;
				    return concreteMap.Graph.GetNodeInfo(concretePathNode.Id).Position;
			    }

				var abstractPathNode = (AbstractPathNode)p;
				return hierarchicalMap.AbstractGraph.GetNodeInfo(abstractPathNode.Id).Position;
		    }).ToList();

			factory.RemoveAbstractNode(hierarchicalMap, targetAbsNode, 1);
			factory.RemoveAbstractNode(hierarchicalMap, startAbsNode, 0);

			return posPath;
	    }

	    private static List<Position> RegularSearch(ConcreteMap concreteMap)
	    {
			var tilingGraph = concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(concreteMap.GetNodeIdFromPos(top, left));

			// Regular pathfinding
			var searcher = new AStar<ConcreteNode>();
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
                result.Add(concreteMap.Graph.GetNodeInfo(Id<ConcreteNode>.From(i)).IsObstacle ? '@' : '.');
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
                        
                    Console.Write(path.Any(n => n.X == x && n.Y == y) ? 'X' : chars[nodeId.IdValue]);
                }

                Console.WriteLine();
            }
        }
    }
}
