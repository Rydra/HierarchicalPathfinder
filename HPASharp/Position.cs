namespace HPASharp
{
    public struct Position
    {
        public Position(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return "(" + X + "," + Y + ")";
        }

        public int X;
        public int Y;
    }
}