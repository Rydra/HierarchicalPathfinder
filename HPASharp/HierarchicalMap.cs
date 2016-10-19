using System;
using System.Collections.Generic;
using System.Linq;

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

        public HierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel) : base(concreteMap, clusterSize, maxLevel)
        {
        }

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
                if (targetNodeInfo.Level < this.currentLevel || !this.NodeInCurrentCluster(targetNodeInfo))
                    continue;

                result.Add(new Neighbour(targetNodeId, edgeInfo.Cost));
            }

            return result;
        }

        /// <summary>
        /// Inserts a node and creates edges around the local points of the cluster it the
        /// node we try to insert belongs to at each level
        /// </summary>
        private void InsertStalHEdges(int nodeId)
        {
            var abstractNodeId = AbsNodeIds[nodeId];
            var nodeInfo = AbstractGraph.GetNodeInfo(abstractNodeId);
            var oldLevel = nodeInfo.Level;
            nodeInfo.Level = MaxLevel;
            for (var level = oldLevel + 1; level <= MaxLevel; level++)
            {
                this.currentLevel = level - 1;
                this.SetCurrentCluster(nodeInfo.Position, level);
                for (var y = this.currentClusterY0; y <= this.currentClusterY1; y++)
                for (var x = this.currentClusterX0; x <= this.currentClusterX1; x++)
                {
                    var nodeId2 = y * this.Width + x;
                    var abstractNodeId2 = AbsNodeIds[nodeId2];
                    this.AddEdgesBetweenAbstractNodes(abstractNodeId, abstractNodeId2, level);
                }
            }
        }

        public override int InsertSTAL(Position pos, int start)
        {
	        var nodeId = pos.Y * Width + pos.X;
            var result = InsertStal(nodeId, pos, start);
            InsertStalHEdges(nodeId);
            return result;
        }
        
        public bool NodeInCurrentCluster(AbsTilingNodeInfo nodeInfo)
        {
            var y = nodeInfo.Position.Y;
            var x = nodeInfo.Position.X;
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
        public void SetCurrentCluster(Position pos, int level)
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
        public void SetCurrentCluster(int x, int y, int offset)
        {
            this.currentClusterY0 = y;
            this.currentClusterX0 = x;
            this.currentClusterY1 = Math.Min(this.Height - 1, y + offset - 1);
            this.currentClusterX1 = Math.Min(this.Width - 1, x + offset - 1);
        }

        public int GetHierarchicalWidth(int level)
        {
            var offset = GetOffset(level);
            var width = this.Width / offset;
            if (this.Width % offset > 0)
                width++;
            return width;
        }

        public int GetHierarchicalHeight(int level)
        {
	        var offset = GetOffset(level);
            var height = this.Height / offset;
            if (this.Height % offset > 0)
                height++;
            return height;
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

        public void SetCurrentLevel(int level)
        {
            this.currentLevel = level;
        }

        #region Create Hierarchical Edges
        
        // TODO: This can become a HUGE refactor. Basically what this code does is creating entrances
        // abstract nodes and edges like in the previous case where we created entrances and all that kind of stuff.
        // We could leverage this new domain knowledge into the code and get rid of this shit with 
        // a way better design (for instance creating multilevel clusters could be a good approach)!!!!!!!
        public override void CreateHierarchicalEdges()
        {
            for (var level = 2; level <= MaxLevel; level++)
            {
                // The offset determines the distances that will separate clusters in this new level
                int offset = GetOffset(level);
                this.currentLevel = level - 1;

                // for each cluster
				// TODO: Maybe we could refactor this so that instead of having to deal with levels,
				// offsets and all this mess... we could create multiple clusters and each cluster have a level.
				// PD: How amazing it is to pick an old project after leaving it in the shelf for some time,
				// you think extremely different in terms of design and see things from another perspective
                for (var top = 0; top < this.Height; top += offset)
                for (var left = 0; left < this.Width; left += offset)
                {
                    // define the bounding box of the current cluster we want to analize to create HEdges
                    this.SetCurrentCluster(left, top, offset);
                    this.ConstructVerticalToVerticalEdges(level);
                    this.ConstructHorizontalToHorizontalEdges(level);
                    this.ConstructHorizontalToVerticalEdges(level);
                }
            }
        }

        private bool IsValidAbstractNode(int abstractNode, int level)
        {
            if (abstractNode == Constants.NO_NODE)
                return false;

            var nodeInfo1 = this.AbstractGraph.GetNodeInfo(abstractNode);
            if (nodeInfo1.Level < level)
                return false;

            return true;
        }

        private void ConstructHorizontalToVerticalEdges(int level)
        {
            // combine nodes on horizontal and vertical edges:
            // This runs over each cell of the 2 horizontal edges against the vertical edges
            var clusterHeight = this.currentClusterY1 - this.currentClusterY0;
            var clusterWidth = this.currentClusterX1 - this.currentClusterX0;
            for (var y1 = this.currentClusterY0; y1 <= this.currentClusterY1; y1 += clusterHeight)
            for (var x1 = this.currentClusterX0 + 1; x1 < this.currentClusterX1; x1++)
            {
                var nodeId1 = y1 * this.Width + x1;
                var absNodeId1 = this.AbsNodeIds[nodeId1];
                if (!this.IsValidAbstractNode(absNodeId1, level)) 
                    continue;

                for (var y2 = this.currentClusterY0 + 1; y2 < this.currentClusterY1; y2++)
                for (var x2 = this.currentClusterX0; x2 <= this.currentClusterX1; x2 += clusterWidth)
                {
                    var nodeId2 = y2 * this.Width + x2;
                    var absNodeId2 = this.AbsNodeIds[nodeId2];
                    this.AddEdgesBetweenAbstractNodes(absNodeId1, absNodeId2, level);
                }
            }
        }

        private void ConstructHorizontalToHorizontalEdges(int level)
        {
            // combine nodes on horizontal edges:
            // This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
            // edges on only horizontal edges)
            var clusterHeight = this.currentClusterY1 - this.currentClusterY0;
            for (var y1 = this.currentClusterY0; y1 <= this.currentClusterY1; y1 += clusterHeight)
            for (var x1 = this.currentClusterX0; x1 <= this.currentClusterX1; x1++)
            {
                var nodeId1 = y1 * this.Width + x1;
                var absNodeId1 = this.AbsNodeIds[nodeId1];
                if (!this.IsValidAbstractNode(absNodeId1, level))
                    continue;

                for (var y2 = this.currentClusterY0; y2 <= this.currentClusterY1; y2 += clusterHeight)
                for (var x2 = this.currentClusterX0; x2 <= this.currentClusterX1; x2++)
                {
                    var nodeId2 = y2 * this.Width + x2;
                    if (nodeId1 >= nodeId2)
                        continue;

                    var absNodeId2 = this.AbsNodeIds[nodeId2];
                    this.AddEdgesBetweenAbstractNodes(absNodeId1, absNodeId2, level);
                }
            }
        }

        private void ConstructVerticalToVerticalEdges(int level)
        {
            // combine nodes on vertical edges:
            // This runs over each cell of the 2 vertical edges
            var clusterWidth = this.currentClusterX1 - this.currentClusterX0;
            for (var y1 = this.currentClusterY0; y1 <= this.currentClusterY1; y1++)
            for (var x1 = this.currentClusterX0; x1 <= this.currentClusterX1; x1 += clusterWidth)
            {
                var nodeId1 = y1 * this.Width + x1;
                var absNodeId1 = this.AbsNodeIds[nodeId1];
                if (!this.IsValidAbstractNode(absNodeId1, level))
                    continue;

                for (var y2 = this.currentClusterY0; y2 <= this.currentClusterY1; y2++)
                for (var x2 = this.currentClusterX0; x2 <= this.currentClusterX1; x2 += clusterWidth)
                {
                    // Only analize the points that lie forward to the current point we are analizing (in front of y1,x1)
                    var nodeId2 = y2 * this.Width + x2;
                    if (nodeId1 >= nodeId2)
                        continue;

                    var absNodeId2 = this.AbsNodeIds[nodeId2];
                    this.AddEdgesBetweenAbstractNodes(absNodeId1, absNodeId2, level);
                }
            }
        }

        /// <summary>
        /// Adds an edge between two abstract nodes for a given level
        /// </summary>
        private void AddEdgesBetweenAbstractNodes(int absNodeId1, int absNodeId2, int level)
        {
            if (absNodeId1 == absNodeId2 || !this.IsValidAbstractNode(absNodeId2, level))
                return;

            var search = new AStar();
            var path = search.FindPath(this, absNodeId1, absNodeId2);
            if (path.PathCost >= 0)
            {
                this.AddEdge(absNodeId1, absNodeId2, path.PathCost, level, false);
                this.AddEdge(absNodeId2, absNodeId1, path.PathCost, level, false);
            }
        }

        #endregion
    }
}
