using System;
using HPASharp.Infrastructure;

namespace HPASharp.Passabilities
{
	public class FakePassability : IPassability
	{
		float obstaclePercentage = 0.20f;

		private bool[,] obstacles;

		public FakePassability(int width, int height)
		{
			obstacles = new bool[width, height];
			CreateObstacles(obstaclePercentage, width, height, true);
		}

		private Random random = new Random(1000);

		public bool CanEnter(Position pos, out int cost)
		{
			cost = Constants.COST_ONE;
			return !obstacles[pos.Y, pos.X];
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

        public Position GetRandomFreePosition()
        {
            var x = random.Next(40);
            var y = random.Next(40);
            while (obstacles[x, y])
            {
                x = random.Next(40);
                y = random.Next(40);
            }

            return new Position(x, y);
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
}