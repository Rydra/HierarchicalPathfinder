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
		public void CreateHierarchicalMap_WhenCreating_Return()
		{
			var abstractMapFactory = new HierarchicalMapFactory();

			var passability = new Program.ExamplePassability();
			var concreteMap = ConcreteMapFactory.CreateConcreteMap(40, 40, passability);
			var hierarchicalMap = abstractMapFactory.CreateHierarchicalMap(concreteMap, 10, 2, EntranceStyle.EndEntrance);

            Assert.AreEqual(16, hierarchicalMap.Clusters.Count);
		    Assert.AreEqual(10, hierarchicalMap.ClusterSize);
            Assert.AreEqual(40, hierarchicalMap.Height);
            Assert.AreEqual(40, hierarchicalMap.Width);
            Assert.AreEqual(2, hierarchicalMap.MaxLevel);
            Assert.AreEqual(AbsType.ABSTRACT_OCTILE, hierarchicalMap.Type);
            Assert.NotNull(hierarchicalMap.AbstractGraph);
        }
    }
}
