using HPASharp;
using HPASharp.Factories;
using Moq;
using NUnit.Framework;

namespace HPAsharp.Tests
{
	[TestFixture]
    public class AbstractMapFactoryTests
    {
		[Test]
		public void CreateTilingTest()
		{
			var abstractMapFactory = new AbstractMapFactory();
			var passability = new Mock<IPassability>();
			var tiling = new ConcreteMap(TileType.Octile, 10, 10, passability.Object);
			
		}
    }
}
