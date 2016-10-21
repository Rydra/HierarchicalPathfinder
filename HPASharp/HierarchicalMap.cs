using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Infrastructure;

namespace HPASharp
{
    using Search;

	/// <summary>
	/// Implements an abstract maze decomposition.
	/// the ultimate abstract representation is a weighted graph of
	/// locations connected by precomputed paths
	/// </summary>
	public class HierarchicalMap : AbstractMap
    {
        private int currentLevel;

        private int currentClusterY0;

        private int currentClusterY1;

        private int currentClusterX0;

        private int currentClusterX1;

        public HierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel) : 
			base(concreteMap, clusterSize, maxLevel) { }

        /// <summary>
        /// Gets the neighbours(successors) of the nodeId for the level set in the currentLevel
        /// </summary>
        public override IEnumerable<Neighbour> GetNeighbours(int nodeId)
        {
            var node = AbstractGraph.GetNode(nodeId);
            var edges = node.Edges;
            var result = new List<Neighbour>(edges.Count);
            foreach (var edge in edges)
            {
                var edgeInfo = edge.Info;
                if (edgeInfo.IsInterEdge)
                {
                    // If the node is an interCluster edge and the edge is of a lower level than
                    // the current level, we have to ignore it
                    // This means we can use higher level interEdges.
                    if (edgeInfo.Level < this.currentLevel) continue;
                }
                else
                {
                    // If it is NOT an interCluster edge (local edge for example) but that edge belongs to another level... ignore it
                    if (edgeInfo.Level != this.currentLevel) continue;
                }

                var targetNodeId = edge.TargetNodeId;
                var targetNodeInfo = AbstractGraph.GetNodeInfo(targetNodeId);

                // NOTE: Sure this if happens? Previous validations should ensure that the edge is connected to
                // a node of the same level. Also... why are we checking if the target node is in the current Cluster?
                // We should be able to navigate to that edge!
                if (targetNodeInfo.Level < this.currentLevel || !this.PositionInCurrentCluster(targetNodeInfo.Position))
                    continue;

                result.Add(new Neighbour(targetNodeId, edgeInfo.Cost));
            }

            return result;
        }
        
        public bool PositionInCurrentCluster(Position position)
        {
            var y = position.Y;
            var x = position.X;
            return y >= this.currentClusterY0 && y <= this.currentClusterY1 && x >= this.currentClusterX0 && x <= this.currentClusterX1;
        }

        // Define the offset between two clusters in this level (each level doubles the cluster size)
        public int GetOffset(int level)
        {
            return ClusterSize * (1 << (level - 1));
        }

        /// <summary>
        /// Defines the bounding box of the cluster we want to process based on a given level and a position in the grid
        /// </summary>
        public override void SetCurrentCluster(Position pos, int level)
        {
            // if the level surpasses the MaxLevel, just set the whole map as a cluster
            if (level > MaxLevel)
            {
                this.currentClusterY0 = 0;
                this.currentClusterY1 = this.Height - 1;
                this.currentClusterX0 = 0;
                this.currentClusterX1 = this.Width - 1;
                return;
            }

            var offset = GetOffset(level);
            var nodeY = pos.Y; // nodeId / this.Width;
            var nodeX = pos.X; // nodeId % this.Width;
            this.currentClusterY0 = nodeY - (nodeY % offset);
            this.currentClusterY1 = Math.Min(this.Height - 1, this.currentClusterY0 + offset - 1);
            this.currentClusterX0 = nodeX - (nodeX % offset);
            this.currentClusterX1 = Math.Min(this.Width - 1, this.currentClusterX0 + offset - 1);
        }

        /// <summary>
        /// Defines the bounding box of the cluster we want to process
        /// </summary>
        public override void SetCurrentCluster(int x, int y, int offset)
        {
            this.currentClusterY0 = y;
            this.currentClusterX0 = x;
            this.currentClusterY1 = Math.Min(this.Height - 1, y + offset - 1);
            this.currentClusterX1 = Math.Min(this.Width - 1, x + offset - 1);
        }

		public override Rectangle GetCurrentClusterRectangle()
		{
			return new Rectangle(currentClusterX0, currentClusterX1, currentClusterY0, currentClusterY1);
		}

		public bool BelongToSameCluster(int node1Id, int node2Id, int level)
        {
            var node1Pos = AbstractGraph.GetNodeInfo(node1Id).Position;
            var node2Pos = AbstractGraph.GetNodeInfo(node2Id).Position;
            var offset = GetOffset(level);
            var currentRow1 = node1Pos.Y - (node1Pos.Y % offset);
            var currentRow2 = node2Pos.Y - (node2Pos.Y % offset);
            var currentCol1 = node1Pos.X - (node1Pos.X % offset);
            var currentCol2 = node2Pos.X - (node2Pos.X % offset);

            if (currentRow1 != currentRow2)
                return false;

            if (currentCol1 != currentCol2)
                return false;

            return true;
        }

        public override void SetCurrentLevel(int level)
        {
            this.currentLevel = level;
        }
    }
}
