﻿namespace HPASharp.Factories
{
    public class Entrance
    {
        public int Id { get; set; }
        public Cluster Cluster1 { get; set; }
        public Cluster Cluster2 { get; set; }

		public Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node SrcNode { get; set; }
		public Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node DestNode { get; set; }
		public Orientation Orientation { get; set; }
		
        
        public Entrance(int id, Cluster cluster1, Cluster cluster2, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node srcNode, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node destNode, Orientation orientation)
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
					level = DetermineLevel(clusterSize, maxLevel, SrcNode.Info.Position.Y);
					break;
				case Orientation.Vertical:
					level = DetermineLevel(clusterSize, maxLevel, SrcNode.Info.Position.X);
					break;
				default:
					level = -1;
					break;
			}
			return level;
		}

		private int DetermineLevel(int clusterSize, int maxLevel, int y)
		{
			var level = 1;
			if (y % clusterSize != 0)
				y++;

			var clusterY = y / clusterSize;
			while (clusterY % 2 == 0 && level < maxLevel)
			{
				clusterY /= 2;
				level++;
			}

			if (level > maxLevel)
				level = maxLevel;
			return level;
		}
	}
}
