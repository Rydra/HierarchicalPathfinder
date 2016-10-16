using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp;
using Moq;
using NUnit.Framework;

namespace HPAsharp.Tests
{
	[TestFixture]
    public class TilingTests
    {
		[Test]
		public void CreateTilingTest()
		{
			var passability = new Mock<IPassability>();
			var tiling = new Tiling(TileType.OCTILE, 10, 10, passability.Object);
			
		}
    }
}
