using HPASharp;
using HPASharp.Factories;
using HPASharp.Passabilities;
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

			var passability = new FakePassability(10, 10);
			var concreteMap = ConcreteMapFactory.CreateConcreteMap(10, 10, passability);
			var hierarchicalMap = abstractMapFactory.CreateHierarchicalMap(concreteMap, 10, 2, EntranceStyle.EndEntrance);
		}
    }
}
