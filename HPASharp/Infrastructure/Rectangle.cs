using System;
using System.Net.NetworkInformation;

namespace HPASharp.Infrastructure
{
	public struct Rectangle
	{
		public Position Origin;
		public Size Size;

		public Rectangle(Position origin, Size size)
		{
			Origin = origin;
			Size = size;
		}

		public Rectangle(int x0, int x1, int y0, int y1)
		{
			Origin = new Position(x0, y0);
			Size = new Size(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
		}
	}
}
