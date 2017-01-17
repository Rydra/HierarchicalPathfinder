using HPASharp;
using HPASharp.Factories;
using Moq;
using NUnit.Framework;
using System.Linq;
using HPASharp.Infrastructure;

namespace HPAsharp.Tests
{
	[TestFixture]
	public class GraphTests
	{
		[Test]
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
	}
}
