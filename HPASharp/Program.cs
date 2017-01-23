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
        
		public static void Main1(string[] args)
        {
            const int clusterSize = 8;
            const int maxLevel = 2;
            const int height = 70;
            const int width = 70;

            Position startPosition = new Position(1, 0);
            Position endPosition = new Position(69, 69);

            // Prepare the abstract graph beforehand
            IPassability passability = new FakePassability(width, height);
            var concreteMap = ConcreteMapFactory.CreateConcreteMap(width, height, passability);
            var abstractMapFactory = new AbstractMapFactory();
			var absTiling = abstractMapFactory.CreateHierarchicalMap(concreteMap, clusterSize, maxLevel, EntranceStyle.EndEntrance);
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
   //         Console.WriteLine("Regular search: " + regularSearchTime + " ms");
			//Console.WriteLine($"{regularSearchPath.Count} path nodes");
			//PrintFormatted(concreteMap, absTiling, clusterSize, regularSearchPath);
			//Console.WriteLine();
   //         Console.WriteLine();
   //         Console.WriteLine();
			//Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
			//Console.WriteLine($"{hierarchicalSearchPath.Count} path nodes");
			//PrintFormatted(concreteMap, absTiling, clusterSize, hierarchicalSearchPath);
   //         Console.WriteLine();
   //         Console.WriteLine();
   //         Console.WriteLine();

			//Console.WriteLine("Press any key to quit...");
			//Console.ReadKey();
#endif

		}

        public static void Main(string[] args)
        {
            const int clusterSize = 10;
            const int maxLevel = 2;
            const int height = 40;
            const int width = 40;

            IPassability passability = new ExamplePassability();
            
            var concreteMap = ConcreteMapFactory.CreateConcreteMap(width, height, passability);

            var abstractMapFactory = new AbstractMapFactory();
			var absTiling = abstractMapFactory.CreateHierarchicalMap(concreteMap, clusterSize, maxLevel, EntranceStyle.EndEntrance);
            //var edges = absTiling.AbstractGraph.Nodes.SelectMany(x => x.Edges.Values)
            //    .GroupBy(x => x.Info.Level)
            //    .ToDictionary(x => x.Key, x => x.Count());

            Func<Position, Position, List<IPathNode>> doHierarchicalSearch = (startPosition, endPosition)
                => HierarchicalSearch(absTiling, maxLevel, concreteMap, startPosition, endPosition);

            Func<Position, Position, List<IPathNode>> doRegularSearch = (startPosition, endPosition)
                => RegularSearch(concreteMap, startPosition, endPosition);

            Func<List<IPathNode>, List<Position>> toPositionPath = (path) =>
                path.Select(p =>
                {
                    if (p is ConcretePathNode)
                    {
                        var concretePathNode = (ConcretePathNode) p;
                        return concreteMap.Graph.GetNodeInfo(concretePathNode.Id).Position;
                    }

                    var abstractPathNode = (AbstractPathNode) p;
                    return absTiling.AbstractGraph.GetNodeInfo(abstractPathNode.Id).Position;
                }).ToList();

            //Position startPosition2 = new Position(18, 0);
            //Position endPosition2 = new Position(20, 0);

            var points = Enumerable.Range(0, 500).Select(_ =>
            {
                var pos1 = ((ExamplePassability) passability).GetRandomFreePosition();
                var pos2 = ((ExamplePassability) passability).GetRandomFreePosition();
                while (Math.Abs(pos1.X - pos2.X) + Math.Abs(pos1.Y - pos2.Y) < 10)
                {
                    pos2 = ((ExamplePassability) passability).GetRandomFreePosition();
                }

                return Tuple.Create(pos1, pos2);
            }).ToArray();
            
            var searchStrategies = new[] {doRegularSearch, doHierarchicalSearch};

            foreach (var searchStrategy in searchStrategies)
            {
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < points.Length; i++)
                {
                    Position startPosition2 = points[i].Item1;
                    Position endPosition2 = points[i].Item2;
                    var regularSearchPath = searchStrategy(startPosition2, endPosition2);
                    var posPath1 = toPositionPath(regularSearchPath);
                }

                var regularSearchTime = watch.ElapsedMilliseconds;
                Console.WriteLine(regularSearchTime);
            }
            
            //foreach (var searchStrategy in searchStrategies)
            //{
            //    var watch = Stopwatch.StartNew();
            //    for (int i = 0; i < points.Length; i++)
            //    {
            //        Position startPosition2 = points[i].Item1;
            //        Position endPosition2 = points[i].Item2;
            //        var regularSearchPath = searchStrategy(startPosition2, endPosition2);
            //        var posPath1 = toPositionPath(regularSearchPath);
            //    }

            //    var regularSearchTime = watch.ElapsedMilliseconds;
            //    Console.WriteLine(regularSearchTime);
            //}
//            // Se siguen repitiendo nodos!
//            // Se repite el 0,19 al hacer smoothing
//            watch = Stopwatch.StartNew();
//            var hierarchicalSearchPath = doHierarchicalSearch(startPosition2, endPosition2);
//            var hierarchicalSearchTime = watch.ElapsedMilliseconds;
//            var posPath = toPositionPath(hierarchicalSearchPath);

//#if !DEBUG
//            Console.WriteLine("Regular search: " + regularSearchTime + " ms");
//            Console.WriteLine("Number of nodes: " + regularSearchPath.Count);

//            Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
//            Console.WriteLine("Number of nodes: " + hierarchicalSearchPath.Count);
//#endif

//#if DEBUG
//            Console.WriteLine("Regular search: " + regularSearchTime + " ms");
//            Console.WriteLine($"{regularSearchPath.Count} path nodes");
//            PrintFormatted(tiling, absTiling, clusterSize, posPath1);
//            Console.WriteLine();
//            Console.WriteLine();
//            Console.WriteLine();
//            Console.WriteLine("Hierachical search: " + hierarchicalSearchTime + " ms");
//            Console.WriteLine($"{hierarchicalSearchPath.Count} path nodes");
//            PrintFormatted(tiling, absTiling, clusterSize, posPath);
//            Console.WriteLine();
//            Console.WriteLine();
//            Console.WriteLine();

//            Console.WriteLine("Press any key to quit...");
//            Console.ReadKey();
//#endif
        }

        private static List<IPathNode> HierarchicalSearch(HierarchicalMap hierarchicalMap, int maxLevel, ConcreteMap concreteMap, Position startPosition, Position endPosition)
	    {
			// Hierarchical pathfinding
			var factory = new AbstractMapFactory();
			var startAbsNode = factory.InsertAbstractNode(hierarchicalMap, startPosition, 0);
	        var targetAbsNode = factory.InsertAbstractNode(hierarchicalMap, endPosition, 1);
	        var maxPathsToRefine = int.MaxValue;
            var hierarchicalSearch = new HierarchicalSearch();
            var abstractPath = hierarchicalSearch.DoHierarchicalSearch(hierarchicalMap, startAbsNode, targetAbsNode, maxLevel, maxPathsToRefine);
			var path = hierarchicalSearch.AbstractPathToLowLevelPath(hierarchicalMap, abstractPath, hierarchicalMap.Width, maxPathsToRefine);

			//var smoother = new SmoothWizard(concreteMap, path);
			//path = smoother.SmoothPath();
			
			factory.RemoveAbstractNode(hierarchicalMap, targetAbsNode, 1);
			factory.RemoveAbstractNode(hierarchicalMap, startAbsNode, 0);

			return path;
	    }

	    private static List<IPathNode> RegularSearch(ConcreteMap concreteMap, Position startPosition, Position endPosition)
	    {
			var tilingGraph = concreteMap.Graph;
			Func<int, int, ConcreteNode> getNode =
				(top, left) => tilingGraph.GetNode(concreteMap.GetNodeIdFromPos(top, left));

			// Regular pathfinding
			var searcher = new AStar<ConcreteNode>();
			var path = searcher.FindPath(concreteMap, getNode(startPosition.X, startPosition.Y).NodeId, getNode(endPosition.X, endPosition.Y).NodeId);
	        var path2 = path.PathNodes;
	        return new List<IPathNode>(path2.Select(p => (IPathNode)new ConcretePathNode(p)));
	    }

	    private static List<char> GetCharVector(ConcreteMap concreteMap)
        {
            var result = new List<char>();
            var numberNodes = concreteMap.NrNodes;
            for (var i = 0; i < numberNodes; i++)
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
            for (var y = 0; y < concreteMap.Height; y++)
            {
                if (y % clusterSize == 0) Console.WriteLine("---------------------------------------------------------");
                for (var x = 0; x < concreteMap.Width; x++)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    if (x % clusterSize == 0) Console.Write('|');

                    var nodeId = concreteMap.GetNodeIdFromPos(x, y);
                    var hasAbsNode = hierarchicalGraph.AbstractGraph.Nodes.SingleOrDefault(n => n.Info.ConcreteNodeId == nodeId);
                    
                    if (hasAbsNode != null)
                        switch (hasAbsNode.Info.Level)
                        {
                            case 1: Console.ForegroundColor = ConsoleColor.Red;
                                break;
                            case 2: Console.ForegroundColor = ConsoleColor.DarkGreen;
                                break;
                        }
                        
                    Console.Write(path.Any(node => node.X == x && node.Y == y) ? 'X' : chars[nodeId.IdValue]);
                }

                Console.WriteLine();
            }
        }
    }
}
