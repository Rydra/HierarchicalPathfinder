using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    using HPASharp.Search;

    // implements an abstract maze decomposition
    // the ultimate abstract representation is a weighted graph of
    // locations connected by precomputed paths
    public class HTiling : AbsTiling
    {
        private int currentLevel;

        private int currentClusterY0;

        private int currentClusterY1;

        private int currentClusterX0;

        private int currentClusterX1;

        public HTiling(int clusterSize, int maxLevel, int height, int width) : base(clusterSize, maxLevel, height, width)
        {
        }

        /// <summary>
        /// Gets the neighbours(successors) of the nodeId for the level set in the currentLevel
        /// </summary>
        public override List<Neighbour> GetNeighbours(int nodeId, int lastNodeId)
        {
            var result = new List<Neighbour>();
            var node = Graph.GetNode(nodeId);
            var edges = node.Edges;
            foreach (var edge in edges)
            {
                var edgeInfo = edge.Info;
                if (edgeInfo.IsInterEdge)
                {
                    // If the node is an interCluster edge and the edge is of a lower level than
                    // the current level, we have to ignore it
                    if (edgeInfo.Level < this.currentLevel) continue;
                }
                else
                {
                    // If it is NOT an interCluster edge (local edge for example) but that edge belongs to another level... ignore it
                    if (edgeInfo.Level != this.currentLevel) continue;
                }

                var targetNodeId = edge.TargetNodeId;
                var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);

                // NOTE: Sure this if happens? Previous validations should ensure that the edge is connected to
                // a node of the same level. Also... why are we checking if the target node is in the current Cluster?
                // We should be able to navigate to that edge!
                if (targetNodeInfo.Level < this.currentLevel || !this.NodeInCurrentCluster(targetNodeInfo))
                    continue;

                if (lastNodeId != Constants.NO_NODE)
                {
                    if (PruneNode(targetNodeId, nodeId, lastNodeId))
                    {
                        continue;
                    }
                }

                result.Add(new Neighbour(targetNodeId, edgeInfo.Cost));
            }

            return result;
        }

        /// <summary>
        /// Inserts a node and creates edges around the local points of the cluster it the
        /// node we try to insert belongs to at each level
        /// </summary>
        /// <param name="nodeId"></param>
        private void InsertStalHEdges(int nodeId)
        {
            var abstractNodeId = AbsNodeIds[nodeId];
            var nodeInfo = Graph.GetNodeInfo(abstractNodeId);
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
                    if (abstractNodeId2 == Constants.NO_NODE)
                        // No abstract node in this position
                        continue;
                    if (nodeId == nodeId2)
                        // Do not link oneself...
                        continue;
                    var nodeInfo2 = Graph.GetNodeInfo(abstractNodeId2);
                    if (nodeInfo2.Level < level)
                        // Do not link with lower level nodes
                        continue;

                    var search = new AStar();
                    search.FindPath(this, abstractNodeId, abstractNodeId2);
                    if (search.PathCost >= 0)
                    {
                        AddEdge(abstractNodeId,
                                    abstractNodeId2,
                                    search.PathCost, level, false);
                        AddEdge(abstractNodeId2,
                            abstractNodeId,
                            search.PathCost, level, false);
                    }
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

        public override bool PruneNode(int targetNodeId, int nodeId, int lastNodeId)
        {
            // if target node is in the same cluster as last node
            return this.BelongToSameCluster(targetNodeId, lastNodeId, this.currentLevel);
        }

        // Define the offset between two clusters in this level (each level doubles the previous one in size)
        public int GetOffset(int level)
        {
            return ClusterSize*(1 << (level - 1));
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
            var result = this.Width / offset;
            if (this.Width % offset > 0)
                result++;
            return result;
        }

        public int GetHierarchicalHeight(int level)
        {
	        var offset = GetOffset(level);
            var result = this.Height / offset;
            if (this.Height % offset > 0)
                result++;
            return result;
        }

        public bool BelongToSameCluster(int node1Id, int node2Id, int level)
        {
            var node1Pos = Graph.GetNodeInfo(node1Id).Position;
            var node2Pos = Graph.GetNodeInfo(node2Id).Position;
            var offset = GetOffset(level);
            var currentRow1 = node1Pos.Y - (node1Pos.Y % offset);
            var currentRow2 = node2Pos.Y - (node2Pos.Y % offset);
            var currentCol1 = node1Pos.X - (node1Pos.X % offset);
            var currentCol2 = node2Pos.X - (node2Pos.X % offset);

            if (currentRow1 != currentRow2)
            {
                return false;
            }

            if (currentCol1 != currentCol2)
            {
                return false;
            }

            return true;
        }

        #region Search

        public override List<Node> DoHierarchicalSearch(int startNodeId, int targetNodeId, int maxSearchLevel, int maxPathsToRefine = int.MaxValue)
        {
            var path = this.PerformSearch(startNodeId, targetNodeId, maxSearchLevel, true).Select(n => new Node(n, maxSearchLevel)).ToList();

            if (path.Count == 0) return path;

            for (var level = maxSearchLevel; level > 1; level--)
                path = this.RefineAbstractPath(path, level, maxPathsToRefine);

            return path;
        }

        public List<int> PerformSearch(int startNodeId, int targetNodeId, int level, bool mainSearch)
        {
            var search = new AStar();
            this.currentLevel = level;
            var nodeInfo = Graph.GetNodeInfo(startNodeId);
            if (mainSearch)
                this.SetCurrentCluster(nodeInfo.Position, MaxLevel + 1);
            else
                this.SetCurrentCluster(nodeInfo.Position, level + 1);

            search.FindPath(this, startNodeId, targetNodeId);
            if (search.PathCost == -1)
            {
                // No path found
                return new List<int>();
            }
            else
            {
                var result = search.Path;
                result.Reverse();
                return result;
            }
        }

        /// <summary>
        /// Refines all the nodes that belong to a certain level to a lower level
        /// </summary>
        public override List<Node> RefineAbstractPath(List<Node> path, int level, int maxPathsToRefine = int.MaxValue)
        {
            var result = new List<Node>();
            var calculatedPaths = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                // if the two consecutive points belong to the same cluster, compute the path between them and
                // add the resulting nodes of that path to the list
                if (path[i].Level == path[i + 1].Level && path[i].Level == level &&
                    this.BelongToSameCluster(path[i].Id, path[i + 1].Id, level) && calculatedPaths < maxPathsToRefine)
                {
                    var tmp = this.PerformSearch(path[i].Id, path[i + 1].Id, level - 1, false)
                        .Select(n => new Node(n, level - 1))
                        .ToList();
                    result.AddRange(tmp);

                    calculatedPaths++;

                    // When we have calculated a path between 2 nodes, the next path in the search
                    // will be an interEdge node. We can safely skip it
                    i++;
                }
                else
                    result.Add(path[i]);
                    
            }

            // make sure last elem is added
            if (result[result.Count - 1].Id != path[path.Count - 1].Id)
                result.Add(path[path.Count - 1]);

            return result;
        }

        #endregion

        #region Edges

        public override void CreateEdges()
        {
            CreateClusterEdges();
            CreateHierarchicalEdges();
        }

        private void CreateHierarchicalEdges()
        {
            for (var level = 2; level <= MaxLevel; level++)
            {
                // The offset determines the distances that will separate clusters in this new level
                int offset = GetOffset(level);
                this.currentLevel = level - 1;

                // for each cluster
                for (var y = 0; y < this.Height; y += offset)
                for (var x = 0; x < this.Width; x += offset)
                {
                    // define the bounding box of the current cluster we want to analize to create HEdges
                    this.SetCurrentCluster(x, y, offset);

                    this.ConstructVerticalToVerticalEdges(level);
                    this.ConstructHorizontalToHorizontalEdges(level);
                    this.ConstructHorizontalToVerticalEdges(level);
                }
            }
        }

        private void ConstructHorizontalToVerticalEdges(int level)
        {
            // combine nodes on horizontal and vertical edges:
            // This runs over each cell of the 2 horizontal edges against the vertical edges
            for (int i1 = this.currentClusterY0; i1 <= this.currentClusterY1; i1 += (this.currentClusterY1 - this.currentClusterY0))
            for (int j1 = this.currentClusterX0 + 1; j1 < this.currentClusterX1; j1++)
            {
                var absNodeId = this.AbsNodeIds[i1 * this.Width + j1];

                if (absNodeId == Constants.NO_NODE)
                {
                    continue;
                }

                var nodeInfo1 = this.Graph.GetNodeInfo(absNodeId);
                if (nodeInfo1.Level < level)
                {
                    continue;
                }

                for (int i2 = this.currentClusterY0 + 1; i2 < this.currentClusterY1; i2++)
                for (int j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2 += (this.currentClusterX1 - this.currentClusterX0))
                {
                    this.AddEdgesBetween(j1, i1, j2, i2, level);
                }
            }
        }

        private void ConstructHorizontalToHorizontalEdges(int level)
        {
            // combine nodes on horizontal edges:
            // This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
            // edges on only horizontal edges)
            for (int i1 = this.currentClusterY0; i1 <= this.currentClusterY1; i1 += (this.currentClusterY1 - this.currentClusterY0))
            for (int j1 = this.currentClusterX0; j1 <= this.currentClusterX1; j1++)
            {
                var absNodeId = this.AbsNodeIds[i1 * this.Width + j1];

                if (absNodeId == Constants.NO_NODE)
                {
                    continue;
                }
                var nodeInfo1 = this.Graph.GetNodeInfo(absNodeId);
                if (nodeInfo1.Level < level)
                {
                    continue;
                }

                for (int i2 = this.currentClusterY0; i2 <= this.currentClusterY1; i2 += (this.currentClusterY1 - this.currentClusterY0))
                for (int j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2++)
                {
                    if (i1 * this.Width + j1 >= i2 * this.Width + j2)
                    {
                        continue;
                    }
                    this.AddEdgesBetween(j1, i1, j2, i2, level);
                }
            }
        }

        private void ConstructVerticalToVerticalEdges(int level)
        {
            // combine nodes on vertical edges:
            // This runs over each cell of the 2 vertical edges
            for (int i1 = this.currentClusterY0; i1 <= this.currentClusterY1; i1++)
            for (int j1 = this.currentClusterX0; j1 <= this.currentClusterX1; j1 += (this.currentClusterX1 - this.currentClusterX0))
            {
                var absNodeId = this.AbsNodeIds[i1 * this.Width + j1];

                if (absNodeId == Constants.NO_NODE)
                {
                    continue;
                }

                var nodeInfo1 = this.Graph.GetNodeInfo(absNodeId);
                if (nodeInfo1.Level < level)
                {
                    continue;
                }

                for (var i2 = this.currentClusterY0; i2 <= this.currentClusterY1; i2++)
                for (var j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2 += (this.currentClusterX1 - this.currentClusterX0))
                {
                    // Only analize the points that lie forward to the current point we are analizing (in front of y1,x1)
                    if (i1 * this.Width + j1 >= i2 * this.Width + j2)
                    {
                        continue;
                    }

                    this.AddEdgesBetween(j1, i1, j2, i2, level);
                }
            }
        }

        private void AddEdgesBetween(int x1, int y1, int x2, int y2, int level)
        {
            var absNodeId2 = this.AbsNodeIds[y2 * this.Width + x2];
            var absNodeId1 = this.AbsNodeIds[y1 * this.Width + x1];

            if (absNodeId2 == Constants.NO_NODE)
                return;

            var nodeInfo2 = this.Graph.GetNodeInfo(absNodeId2);
            if (nodeInfo2.Level < level)
                return;

            var search = new AStar();
            search.FindPath(this, absNodeId1, absNodeId2);
            if (search.PathCost >= 0)
            {
                this.AddEdge(absNodeId1, absNodeId2, search.PathCost, level, false);
                this.AddEdge(absNodeId2, absNodeId1, search.PathCost, level, false);
            }
        }

        #endregion

        #region Printing

        public override void PrintGraph()
        {
            Console.WriteLine("Printing abstract graph:");
            for (int id = 0; id < NrNodes; id++)
            {
                var edges = Graph.GetOutEdges(id);
                Console.WriteLine("Node " + id + "; BF "+ edges.Count);
                var nodeInfo = Graph.GetNodeInfo(id);
                nodeInfo.PrintInfo();
                foreach (var edge in edges)
                {
                    Console.Write("Edge to node " + edge.TargetNodeId + ": ");
                    edge.Info.PrintInfo();
                }
                
                Console.WriteLine();
            }
        }
        

        #endregion
    }
}
