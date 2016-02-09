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
            var edges = node.OutEdges;
            foreach (var edge in edges)
            {
                if (edge.Info.IsInterEdge)
                {
                    // If the node is an interCluster edge and the edge is of a lower level than
                    // the current level, we have to ignore it
                    if (edge.Info.Level < this.currentLevel) continue;
                }
                else
                {
                    // If it is NOT an interCluster edge (local edge for example) but that edge belongs to another level... ignore it
                    if (edge.Info.Level != this.currentLevel) continue;
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

                result.Add(new Neighbour(targetNodeId, edge.Info.Cost));
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
            var search = new AStar(false);
            var nodeInfo = Graph.GetNodeInfo(AbsNodeIds[nodeId]);
            var oldLevel = nodeInfo.Level;
            nodeInfo.Level = MaxLevel;
            for (var level = oldLevel + 1; level <= MaxLevel; level++)
            {
                this.currentLevel = level - 1;
                this.SetCurrentCluster(nodeInfo.Position, level);
                for (var y = this.currentClusterY0; y <= this.currentClusterY1; y++)
                for (var x = this.currentClusterX0; x <= this.currentClusterX1; x++)
                {
                    var abstractNodeId = y * this.Width + x;
                    if (AbsNodeIds[abstractNodeId] == Constants.NO_NODE)
                        // No abstract node in this position
                        continue;
                    if (nodeId == abstractNodeId)
                        // Do not link oneself...
                        continue;
                    var nodeInfo2 = Graph.GetNodeInfo(AbsNodeIds[abstractNodeId]);
                    if (nodeInfo2.Level < level)
                        // Do not link with lower level nodes
                        continue;
                    {
                        search.FindPath(this, AbsNodeIds[nodeId], AbsNodeIds[abstractNodeId]);
                        if (search.PathCost >= 0)
                        {
                            AddOutEdge(AbsNodeIds[nodeId],
                                       AbsNodeIds[abstractNodeId],
                                       search.PathCost, level, false);
                            AddOutEdge(AbsNodeIds[abstractNodeId],
                               AbsNodeIds[nodeId],
                               search.PathCost, level, false);
                        }
                    }
                }
            }
        }

        public int InsertSTAL(int nodeId, Position pos, int start)
        {
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
            int offset = GetOffset(level);
            var result = this.Width / offset;
            if (this.Width % offset > 0)
                result++;
            return result;
        }

        public int GetHierarchicalHeight(int level)
        {
            int result;
            int offset = GetOffset(level);
            result = this.Height / offset;
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

        public void DoHierarchicalSearch(int startNodeId, int targetNodeId, out List<int> result, int maxSearchLevel)
        {
            var path = this.PerformSearch(startNodeId, targetNodeId, maxSearchLevel, true);
            for (int level = maxSearchLevel; level > 1; level--)
            {
                path = this.RefineAbstractPath(path, level);
            }

            result = path;
        }

        public List<int> PerformSearch(int startNodeId, int targetNodeId, int level, bool mainSearch)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(mainSearch));
            this.currentLevel = level;
            var nodeInfo = Graph.GetNodeInfo(startNodeId);
            if (mainSearch)
                this.SetCurrentCluster(nodeInfo.Position, MaxLevel + 1);
            else
                this.SetCurrentCluster(nodeInfo.Position, level + 1);

            search.findPath(this, startNodeId, targetNodeId);
            if (search.getPathCost() == -1)
            {
                // No path found
                return new List<int>();
            }
            else
            {
                var result = search.getPath();
                result.Reverse();
                return result;
            }
        }

        public List<int> RefineAbstractPath(List<int> path, int level)
        {
            var result = new List<int>();

            // add first elem
            result.Add(path[0]);

            for (int i = 0; i < path.Count - 1; i++)
            {
                // if the two consecutive points belong to the same cluster, compute the path between them and
                // add the resulting nodes of that path to the list
                if (this.BelongToSameCluster(path[i], path[i+1], level))
                {
                    var tmp = this.PerformSearch(path[i], path[i+1], level - 1, false);
                    for (var k = 0; k < tmp.Count; k++)
                    {
                        if (result[result.Count - 1] != tmp[k])
                            result.Add(tmp[k]);
                    }
                }
            }

            // make sure last elem is added
            if (result[result.Count - 1] != path[path.Count - 1])
                result.Add(path[path.Count - 1]);

            return result;
        }

        #endregion

        #region Edges

        public override void CreateEdges()
        {
            CreateClusterEdges();
            this.CreateHierarchicalEdges();
        }

        private void CreateHierarchicalEdges()
        {
            for (int level = 2; level <= MaxLevel; level++)
            {
                // The offset determines the distances that will separate clusters in this new level
                int offset = GetOffset(level);
                this.currentLevel = level - 1;

                // for each cluster
                for (int y = 0; y < this.Height; y += offset)
                    for (int x = 0; x < this.Width; x += offset)
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
                if (this.AbsNodeIds[i1 * this.Width + j1] == Constants.NO_NODE)
                {
                    continue;
                }

                var nodeInfo1 = this.Graph.GetNodeInfo(this.AbsNodeIds[i1 * this.Width + j1]);
                if (nodeInfo1.Level < level)
                {
                    continue;
                }
                for (int i2 = this.currentClusterY0 + 1; i2 < this.currentClusterY1; i2++)
                {
                    for (int j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2 += (this.currentClusterX1 - this.currentClusterX0))
                    {
                        this.AddOutEdgesBetween(j1, i1, j2, i2, level);
                    }
                }
            }
        }

        private void ConstructHorizontalToHorizontalEdges(int level)
        {
            // combine nodes on horizontal edges:
            // This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
            // edges on only horizontal edges)
            for (int i1 = this.currentClusterY0; i1 <= this.currentClusterY1; i1 += (this.currentClusterY1 - this.currentClusterY0))
            {
                for (int j1 = this.currentClusterX0; j1 <= this.currentClusterX1; j1++)
                {
                    if (this.AbsNodeIds[i1 * this.Width + j1] == Constants.NO_NODE)
                    {
                        continue;
                    }
                    var nodeInfo1 = this.Graph.GetNodeInfo(this.AbsNodeIds[i1 * this.Width + j1]);
                    if (nodeInfo1.Level < level)
                    {
                        continue;
                    }
                    for (int i2 = this.currentClusterY0; i2 <= this.currentClusterY1; i2 += (this.currentClusterY1 - this.currentClusterY0))
                    {
                        for (int j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2++)
                        {
                            if (i1 * this.Width + j1 >= i2 * this.Width + j2)
                            {
                                continue;
                            }
                            this.AddOutEdgesBetween(j1, i1, j2, i2, level);
                        }
                    }
                }
            }
        }

        private void ConstructVerticalToVerticalEdges(int level)
        {
            // combine nodes on vertical edges:
            // This runs over each cell of the 2 vertical edges
            for (int i1 = this.currentClusterY0; i1 <= this.currentClusterY1; i1++)
            {
                for (int j1 = this.currentClusterX0; j1 <= this.currentClusterX1; j1 += (this.currentClusterX1 - this.currentClusterX0))
                {
                    if (this.AbsNodeIds[i1 * this.Width + j1] == Constants.NO_NODE)
                    {
                        continue;
                    }

                    var nodeInfo1 = this.Graph.GetNodeInfo(this.AbsNodeIds[i1 * this.Width + j1]);
                    if (nodeInfo1.Level < level)
                    {
                        continue;
                    }

                    for (int i2 = this.currentClusterY0; i2 <= this.currentClusterY1; i2++)
                    {
                        for (int j2 = this.currentClusterX0; j2 <= this.currentClusterX1; j2 += (this.currentClusterX1 - this.currentClusterX0))
                        {
                            // Only analize the points that lie forward to the current point we are analizing (in front of y1,x1)
                            if (i1 * this.Width + j1 >= i2 * this.Width + j2)
                            {
                                continue;
                            }
                            this.AddOutEdgesBetween(j1, i1, j2, i2, level);
                        }
                    }
                }
            }
        }

        private void AddOutEdgesBetween(int x1, int y1, int x2, int y2, int level)
        {
            if (this.AbsNodeIds[y2 * this.Width + x2] == Constants.NO_NODE)
            {
                return;
            }

            var nodeInfo2 = this.Graph.GetNodeInfo(this.AbsNodeIds[y2 * this.Width + x2]);
            if (nodeInfo2.Level < level)
            {
                return;
            }

            var search = new AStar(false);
            search.FindPath(this, this.AbsNodeIds[y1 * this.Width + x1], this.AbsNodeIds[y2 * this.Width + x2]);
            if (search.PathCost >= 0)
            {
                this.AddOutEdge(this.AbsNodeIds[y1 * this.Width + x1], this.AbsNodeIds[y2 * this.Width + x2], search.PathCost, level, false);
                this.AddOutEdge(this.AbsNodeIds[y2 * this.Width + x2], this.AbsNodeIds[y1 * this.Width + x1], search.PathCost, level, false);
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
