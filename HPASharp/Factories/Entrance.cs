using HPASharp.Graph;
using HPASharp.Infrastructure;

namespace HPASharp.Factories
{
    public enum EntranceStyle
    {
        MiddleEntrance, EndEntrance
    }

    public class Entrance
    {
        public Id<Entrance> Id { get; set; }
        public Cluster Cluster1 { get; set; }
        public Cluster Cluster2 { get; set; }

		public ConcreteNode SrcNode { get; set; }
		public ConcreteNode DestNode { get; set; }
		public Orientation Orientation { get; set; }
		
        
        public Entrance(Id<Entrance> id, Cluster cluster1, Cluster cluster2, ConcreteNode srcNode, ConcreteNode destNode, Orientation orientation)
        {
            Id = id;
            Cluster1 = cluster1;
            Cluster2 = cluster2;

	        SrcNode = srcNode;
	        DestNode = destNode;
			
            Orientation = orientation;
        }
		
		public int GetEntranceLevel(int clusterSize, int maxLevel)
		{
			int level;
			switch (Orientation)
			{
				case Orientation.Horizontal:
					level = DetermineLevel(clusterSize, maxLevel, SrcNode.Info.Position.X);
					break;
				case Orientation.Vertical:
					level = DetermineLevel(clusterSize, maxLevel, SrcNode.Info.Position.Y);
					break;
				default:
					level = -1;
					break;
			}
			return level;
		}

		private int DetermineLevel(int clusterSize, int maxLevel, int y)
		{
			int level = 1;

			if (y % clusterSize != 0)
				y++;

			if (y < clusterSize)
			{
				return 1;
			}

			int clusterY = y / clusterSize;
			while (clusterY % 2 == 0 && level < maxLevel)
			{
				clusterY /= 2;
				level++;
			}

			return level > maxLevel ? maxLevel : level;
		}
	}
}
