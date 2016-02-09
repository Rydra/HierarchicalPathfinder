using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Infrastructure
{
    public struct Rectangle
    {
        public int Top;

        public int Bottom;

        public int Left;

        public int Right;

        public Rectangle(int top, int bottom, int left, int right)
        {
            Top = top;
            Bottom = bottom;
            Left = left;
            Right = right;
        }
    }
}
