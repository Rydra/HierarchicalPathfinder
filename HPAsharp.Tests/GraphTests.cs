using System;
using System.Collections.Generic;
using System.Diagnostics;
using HPASharp;
using HPASharp.Factories;
using Moq;
using NUnit.Framework;
using System.Linq;
using HPASharp.Search;
using HPASharp.Smoother;

namespace HPAsharp.Tests
{
	[TestFixture]
	public class GraphTests
	{
		[Test]
		[Ignore("")]
		public void CreateGraphTest()
		{
			var passability = new Mock<IPassability>();
			var movementCost = Constants.COST_ONE;
			passability.Setup(x => x.CanEnter(It.IsAny<Position>(), out movementCost)).Returns(true);

			var graph = GraphFactory.CreateGraph(8, 6, passability.Object);
			Assert.AreEqual(8*6, graph.Nodes.Count);
			Assert.IsTrue(graph.Nodes.TrueForAll(n => !n.Info.IsObstacle));
		}

		[Test]
		[Ignore("")]
		public void AddNodeToGraphTest()
		{
			var passability = new Mock<IPassability>();
			int movementCost;
			passability.Setup(x => x.CanEnter(It.IsAny<Position>(), out movementCost)).Returns(true);

			// Set an arbitrary position as impassable, this should be reflected in the resulting graph
			passability.Setup(x => x.CanEnter(new Position(9, 8), out movementCost)).Returns(false);

			var graph = GraphFactory.CreateGraph(10, 10, passability.Object);
			Assert.IsTrue(graph.Nodes.Find(n => n.Info.IsObstacle).Info.Position.X == new Position(9, 8).X);
		}

		public class Passability : IPassability
		{
			float obstaclePercentage = 0.20f;

			private bool[,] obstacles;

			public Passability(int width, int height)
			{
				obstacles = new bool[width, height];
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
				for (var count = 0; count < numberObstacles;)
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

		[Test]
		public void CheckPath()
		{
			var height = 70;
			var width = 70;
			var clusterSize = 8;
			var maxLevel = 3;

			var expectedPath = new[]
			{
				new Position(0, 0), new Position(1, 1), new Position(2, 2), new Position(3, 3), new Position(4, 4),
				new Position(5, 5), new Position(6, 6), new Position(7, 7), new Position(7, 8), new Position(8, 8),
				new Position(8, 8), new Position(9, 9), new Position(10, 10), new Position(11, 11), new Position(12, 12),
				new Position(13, 13), new Position(14, 13), new Position(15, 14), new Position(15, 15), new Position(16, 15),
				new Position(15, 15), new Position(16, 16), new Position(16, 16), new Position(17, 17), new Position(17, 18),
				new Position(18, 19), new Position(19, 19), new Position(20, 20), new Position(21, 21), new Position(22, 21),
				new Position(23, 22), new Position(24, 22), new Position(24, 22), new Position(25, 23), new Position(26, 23),
				new Position(26, 24), new Position(26, 24), new Position(27, 25), new Position(28, 26), new Position(29, 27),
				new Position(30, 28), new Position(31, 29), new Position(32, 29), new Position(32, 29), new Position(33, 30),
				new Position(33, 31), new Position(33, 32), new Position(33, 32), new Position(34, 33), new Position(34, 34),
				new Position(35, 35), new Position(36, 35), new Position(37, 36), new Position(38, 37), new Position(39, 38),
				new Position(39, 39), new Position(39, 40), new Position(40, 40), new Position(40, 40), new Position(40, 41),
				new Position(41, 42), new Position(42, 43), new Position(43, 44), new Position(44, 45), new Position(45, 46),
				new Position(45, 47), new Position(45, 48), new Position(45, 48), new Position(46, 49), new Position(46, 50),
				new Position(47, 51), new Position(48, 51), new Position(48, 51), new Position(49, 52), new Position(50, 52),
				new Position(51, 53), new Position(52, 54), new Position(53, 55), new Position(53, 56), new Position(53, 56),
				new Position(54, 57), new Position(54, 58), new Position(55, 59), new Position(56, 59), new Position(56, 59),
				new Position(57, 60), new Position(57, 61), new Position(58, 62), new Position(59, 62), new Position(60, 63),
				new Position(60, 64), new Position(60, 64), new Position(61, 65), new Position(62, 65), new Position(63, 65),
				new Position(64, 65), new Position(64, 65), new Position(65, 66), new Position(66, 66), new Position(67, 67),
				new Position(68, 68), new Position(69, 69)

			};


			// Prepare the abstract graph beforehand
			IPassability passability = new Passability(width, height);
			var tiling = TilingFactory.CreateTiling(width, height, passability);
			var wizard = new AbstractMapFactory();
			wizard.CreateAbstractMap(tiling, clusterSize, maxLevel, EntranceStyle.END_ENTRANCE);
			var absTiling = wizard.AbstractMap;
			var path2 = HierarchicalSearch(absTiling, maxLevel, tiling, clusterSize);
			CollectionAssert.AreEqual(expectedPath, path2);

			//var str = string.Join(",", path2.Select(p => string.Format("new Position({0},{1})", p.X, p.Y)));
		}

		private static List<Position> HierarchicalSearch(AbstractMap abstractMap, int maxLevel, ConcreteMap concreteMap, int clusterSize)
		{
			// Hierarchical pathfinding
			var startAbsNode = abstractMap.InsertSTAL(new Position(0, 0), 0);
			var targetAbsNode = abstractMap.InsertSTAL(new Position(69, 69), 1);
			var maxPathsToRefine = int.MaxValue;
			var hierarchicalSearch = new HierarchicalSearch();
			var hierarchicalmap = (HierarchicalMap)abstractMap;
			var abstractPath = hierarchicalSearch.DoHierarchicalSearch(hierarchicalmap, startAbsNode, targetAbsNode, maxLevel, maxPathsToRefine);
			var path = hierarchicalSearch.AbstractPathToLowLevelPath(hierarchicalmap, abstractPath, abstractMap.Width, maxPathsToRefine);
			abstractMap.RemoveStal(targetAbsNode, 1);
			abstractMap.RemoveStal(startAbsNode, 0);

			return path.Select(n => n.Level == 0 ? concreteMap.Graph.GetNodeInfo(n.Id).Position : abstractMap.Graph.GetNodeInfo(n.Id).Position).ToList();
		}
	}
}
