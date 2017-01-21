using HPASharp;
using HPASharp.Factories;
using HPASharp.Graph;
using HPASharp.Infrastructure;
using NUnit.Framework;

namespace HPAsharp.Tests
{
	[TestFixture]
	public class EntranceTests
	{
		[TestCase(Orientation.Vertical, 1, 18, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 9, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 10, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 19, 2, 2)]
		[TestCase(Orientation.Vertical, 1, 0, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 1, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 2, 2, 1)]
		[TestCase(Orientation.Vertical, 1, 39, 2, 2)]
		[TestCase(Orientation.Vertical, 1, 60, 2, 2)]
		[TestCase(Orientation.Vertical, 1, 59, 2, 2)]

		[TestCase(Orientation.Vertical, 1, 9, 3, 1)]
		[TestCase(Orientation.Vertical, 1, 19, 3, 2)]
		[TestCase(Orientation.Vertical, 1, 29, 3, 1)]
		[TestCase(Orientation.Vertical, 1, 38, 3, 1)]
		[TestCase(Orientation.Vertical, 1, 39, 3, 3)]
		[TestCase(Orientation.Vertical, 1, 40, 3, 3)]
		[TestCase(Orientation.Vertical, 1, 49, 3, 1)]
		[TestCase(Orientation.Vertical, 1, 59, 3, 2)]
		[TestCase(Orientation.Vertical, 1, 69, 3, 1)]
		[TestCase(Orientation.Vertical, 1, 79, 3, 3)]

		[TestCase(Orientation.Horizontal, 18, 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 9 , 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 10, 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 19, 1, 2, 2)]
		[TestCase(Orientation.Horizontal, 0 , 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 1 , 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 2 , 1, 2, 1)]
		[TestCase(Orientation.Horizontal, 39, 1, 2, 2)]
		[TestCase(Orientation.Horizontal, 60, 1, 2, 2)]
		[TestCase(Orientation.Horizontal, 59, 1, 2, 2)]

		[TestCase(Orientation.Horizontal, 9 , 1, 3, 1)]
		[TestCase(Orientation.Horizontal, 19, 1, 3, 2)]
		[TestCase(Orientation.Horizontal, 29, 1, 3, 1)]
		[TestCase(Orientation.Horizontal, 38, 1, 3, 1)]
		[TestCase(Orientation.Horizontal, 39, 1, 3, 3)]
		[TestCase(Orientation.Horizontal, 40, 1, 3, 3)]
		[TestCase(Orientation.Horizontal, 49, 1, 3, 1)]
		[TestCase(Orientation.Horizontal, 59, 1, 3, 2)]
		[TestCase(Orientation.Horizontal, 69, 1, 3, 1)]
		[TestCase(Orientation.Horizontal, 79, 1, 3, 3)]
		public void DetermineLevel_WhenOnEdgeOfLevel2Cluster_ReturnLevel2(Orientation orientation, int x, int y, int maxLevel, int expectedLevel)
		{
			var srcNode = new ConcreteNode(Id<ConcreteNode>.From(1), new ConcreteNodeInfo(false, 1, new Position(x, y)));
			var entrance = new Entrance(Id<Entrance>.From(1), null, null, srcNode, null, orientation);
			Assert.AreEqual(expectedLevel, entrance.GetEntranceLevel(10, maxLevel));
		}
	}
}
