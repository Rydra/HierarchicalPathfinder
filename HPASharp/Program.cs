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

	public partial class Program
    {
		//private static readonly int Height = 16;
		//private static readonly int Width = 16;

		//private static readonly Position StartPosition = new Position(1, 0);
		//private static readonly Position EndPosition = new Position(15, 15);
        
		public static void Main2(string[] args)
        {
            const int clusterSize = 8;
            const int maxLevel = 2;
            const int height = 70;
            const int width = 70;

            Position startPosition = new Position(1, 0);
            Position endPosition = new Position(69, 69);

            // Prepare the abstract graph beforehand
            IPassability passability = new FakePassability(width, height);
            var concreteMap = ConcreteMapFactory.CreateTiling(width, height, passability);
            var abstractMapFactory = new AbstractMapFactory();
			abstractMapFactory.CreateHierarchicalMap(concreteMap, clusterSize, maxLevel, EntranceStyle.EndEntrance);
			var absTiling = abstractMapFactory.HierarchicalMap;
            //var edges = absTiling.AbstractGraph.Nodes.SelectMany(x => x.Edges.Values)
            //    .GroupBy(x => x.Info.Level)
            //    .ToDictionary(x => x.Key, x => x.Count());

            var watch = Stopwatch.StartNew();
            var regularSearchPath = RegularSearch(concreteMap, startPosition, endPosition);
	        var regularSearchTime = watch.ElapsedMilliseconds;

            watch = Stopwatch.StartNew();
            var hierarchicalSearchPath = HierarchicalSearch(absTiling, maxLevel, concreteMap, startPosition, endPosition);
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
			PrintFormatted(concreteMap, absTiling, clusterSize, regularSearchPath);
			Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
			Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
			Console.WriteLine($"{hierarchicalSearchPath.Count} path nodes");
			PrintFormatted(concreteMap, absTiling, clusterSize, hierarchicalSearchPath);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

			Console.WriteLine("Press any key to quit...");
			Console.ReadKey();
#endif

		}

        public static void Main(string[] args)
        {
            const int clusterSize = 10;
            const int maxLevel = 1;
            const int height = 40;
            const int width = 40;

            Position startPosition = new Position(18, 0);
            Position endPosition = new Position(20, 0);
            IPassability passability = new ExamplePassability();
            var tiling = ConcreteMapFactory.CreateTiling(width, height, passability);
            var wizard = new AbstractMapFactory();
            wizard.CreateHierarchicalMap(tiling, clusterSize, maxLevel, EntranceStyle.EndEntrance);
            var absTiling = wizard.HierarchicalMap;
            //var edges = absTiling.AbstractGraph.Nodes.SelectMany(x => x.Edges.Values)
            //    .GroupBy(x => x.Info.Level)
            //    .ToDictionary(x => x.Key, x => x.Count());

            var watch = Stopwatch.StartNew();
            var regularSearchPath = RegularSearch(tiling, startPosition, endPosition);
            var regularSearchTime = watch.ElapsedMilliseconds;

            // Se siguen repitiendo nodos!
            watch = Stopwatch.StartNew();
            var hierarchicalSearchPath = HierarchicalSearch(absTiling, maxLevel, tiling, startPosition, endPosition);
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
            PrintFormatted(tiling, absTiling, clusterSize, regularSearchPath);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
            Console.WriteLine($"{hierarchicalSearchPath.Count} path nodes");
            PrintFormatted(tiling, absTiling, clusterSize, hierarchicalSearchPath);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
#endif
        }

        private static List<Position> HierarchicalSearch(HierarchicalMap hierarchicalMap, int maxLevel, ConcreteMap concreteMap, Position startPosition, Position endPosition)
	    {
			// Hierarchical pathfinding
			var factory = new AbstractMapFactory();
			var startAbsNode = factory.InsertAbstractNode(hierarchicalMap, startPosition, 0);
	        var targetAbsNode = factory.InsertAbstractNode(hierarchicalMap, endPosition, 1);
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

	    private static List<Position> RegularSearch(ConcreteMap concreteMap, Position startPosition, Position endPosition)
	    {
			var tilingGraph = concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(concreteMap.GetNodeIdFromPos(top, left));

			// Regular pathfinding
			var searcher = new AStar<ConcreteNode>();
			var path = searcher.FindPath(concreteMap, getNode(startPosition.X, startPosition.Y).NodeId, getNode(endPosition.X, endPosition.Y).NodeId);
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
