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
        public int CurrentLevel { get; set; }
        public int CurrentClusterY0 { get; set; }
        public int CurrentClusterY1 { get; set; }
        public int CurrentClusterX0 { get; set; }
        public int CurrentClusterX1 { get; set; }

        const int NO_NODE = -1;

        public HTiling(int clusterSize, int maxLevel, int height, int width) : base(clusterSize, maxLevel, height, width)
        {
        }

        /// <summary>
        /// Gets the neighbours(successors) of the nodeId for the level set in the CurrentLevel
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
                    if (edge.Info.Level < CurrentLevel) continue;
                }
                else
                {
                    // If it is NOT an interCluster edge (local edge for example) but that edge belongs to another level... ignore it
                    if (edge.Info.Level != CurrentLevel) continue;
                }

                var targetNodeId = edge.TargetNodeId;
                var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);

                // NOTE: Sure this if happens? Previous validations should ensure that the edge is connected to
                // a node of the same level. Also... why are we checking if the target node is in the current Cluster?
                // We should be able to navigate to that edge!
                if (targetNodeInfo.Level < this.CurrentLevel || !this.NodeInCurrentCluster(targetNodeInfo))
                    continue;

                if (lastNodeId != NO_NODE)
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
        public void InsertStalHEdges(int nodeId)
        {
            var search = new AStar(false);
            var nodeInfo = Graph.GetNodeInfo(AbsNodeIds[nodeId]);
            int oldLevel = nodeInfo.Level;
            nodeInfo.Level = MaxLevel;
            for (int level = oldLevel + 1; level <= MaxLevel; level++)
            {
                this.CurrentLevel = level - 1;
                this.SetCurrentCluster(nodeId, level);
                for (int i2 = this.CurrentClusterY0; i2 <= this.CurrentClusterY1; i2++)
                for (int j2 = this.CurrentClusterX0; j2 <= this.CurrentClusterX1; j2++)
                {
                    if (AbsNodeIds[i2*this.Width+j2] == NO_NODE)
                        continue;
                    if (nodeId == i2*this.Width+j2)
                        continue;
                    var nodeInfo2 = Graph.GetNodeInfo(AbsNodeIds[i2*this.Width+j2]);
                    if (nodeInfo2.Level < level)
                        continue;
                    {
                        search.FindPath(this, AbsNodeIds[nodeId], AbsNodeIds[i2*this.Width+j2]);
                        if (search.PathCost >= 0)
                        {
                            AddOutEdge(AbsNodeIds[nodeId],
                                       AbsNodeIds[i2*this.Width+j2],
                                       search.PathCost, level, false);
                            AddOutEdge(AbsNodeIds[i2*this.Width+j2],
                               AbsNodeIds[nodeId],
                               search.PathCost, level, false);
                        }
                    }
                }
            }
        }

        public int InsertSTAL(int nodeId, Position pos, int start)
        {
            int result = InsertStal(nodeId, pos, start);
            InsertStalHEdges(nodeId);
            return result;
        }

        public void DoHierarchicalSearch(int startNodeId, int targetNodeId, out List<int> result, int maxSearchLevel)
        {
            var path = this.PerformSearch(startNodeId, targetNodeId, maxSearchLevel, true);
            for (int level = maxSearchLevel; level > 1; level--)
            {
                path = this.RefineAbstractPath(path, level);
            }

            result = path;
        }

        public bool NodeInCurrentCluster(AbsTilingNodeInfo nodeInfo)
        {
            var y = nodeInfo.Position.Y;
            var x = nodeInfo.Position.X;
            return y >= CurrentClusterY0 && y <= CurrentClusterY1 && x >= CurrentClusterX0 && x <= CurrentClusterX1;
        }

        public override bool PruneNode(int targetNodeId, int nodeId, int lastNodeId)
        {
            // if target node is in the same cluster as last node
            return this.BelongToSameCluster(targetNodeId, lastNodeId, this.CurrentLevel);
        }

        public int GetOffset(int level)
        {
            return ClusterSize*(1 << (level - 1));
        }

        public void SetCurrentCluster(int nodeId, int level)
        {
            if (level > MaxLevel)
            {
                CurrentClusterY0 = 0;
                CurrentClusterY1 = this.Height - 1;
                CurrentClusterX0 = 0;
                CurrentClusterX1 = this.Width - 1;
                return;
            }

            int offset = GetOffset(level);
            int nodeRow = nodeId / this.Width;
            int nodeCol = nodeId % this.Width;
            CurrentClusterY0 = nodeRow - (nodeRow % offset);
            CurrentClusterY1 = Math.Min(this.Height - 1, CurrentClusterY0 + offset - 1);
            CurrentClusterX0 = nodeCol - (nodeCol % offset);
            CurrentClusterX1 = Math.Min(this.Width - 1, CurrentClusterX0 + offset - 1);
        }

        /// <summary>
        /// Defines the bounding box of the cluster we want to process
        /// </summary>
        public void SetCurrentCluster(int x, int y, int offset)
        {
            CurrentClusterY0 = y;
            CurrentClusterX0 = x;
            CurrentClusterY1 = Math.Min(this.Height - 1, y + offset - 1);
            CurrentClusterX1 = Math.Min(this.Width - 1, x + offset - 1);
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
            var node1Info = Graph.GetNodeInfo(node1Id);
            var node2Info = Graph.GetNodeInfo(node2Id);
            int offset = GetOffset(level);
            int node1Y = node1Info.Position.Y;
            int node1X = node1Info.Position.X;
            int node2Y = node2Info.Position.Y;
            int node2X = node2Info.Position.X;
            int currentRow1 = node1Y - (node1Y%offset);
            int currentRow2 = node2Y - (node2Y%offset);
            int currentCol1 = node1X - (node1X%offset);
            int currentCol2 = node2X - (node2X%offset);

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

        public List<int> PerformSearch(int startNodeId, int targetNodeId, int level, bool mainSearch)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(mainSearch));
            CurrentLevel = level;
            var nodeInfo = Graph.GetNodeInfo(startNodeId);
            if (mainSearch)
            {
                this.SetCurrentCluster(nodeInfo.CenterId, MaxLevel + 1);
            }
            else
                this.SetCurrentCluster(nodeInfo.CenterId, level + 1);

            search.findPath(this, startNodeId, targetNodeId);
            if (search.getPathCost() == -1)
            {
                return new List<int>();
                //cerr << "oops, no path found\n";
                //assert (false);
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
                    for (int k = 0; k < tmp.Count; k++)
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
                this.CurrentLevel = level - 1;

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
            for (int i1 = this.CurrentClusterY0; i1 <= this.CurrentClusterY1; i1 += (this.CurrentClusterY1 - this.CurrentClusterY0))
            {
                for (int j1 = this.CurrentClusterX0 + 1; j1 < this.CurrentClusterX1; j1++)
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
                    for (int i2 = this.CurrentClusterY0 + 1; i2 < this.CurrentClusterY1; i2++)
                    {
                        for (int j2 = this.CurrentClusterX0; j2 <= this.CurrentClusterX1; j2 += (this.CurrentClusterX1 - this.CurrentClusterX0))
                        {
                            this.AddOutEdgesBetween(j1, i1, j2, i2, level);
                        }
                    }
                }
            }
        }

        private void ConstructHorizontalToHorizontalEdges(int level)
        {
            // combine nodes on horizontal edges:
            // This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
            // edges on only horizontal edges)
            for (int i1 = this.CurrentClusterY0; i1 <= this.CurrentClusterY1; i1 += (this.CurrentClusterY1 - this.CurrentClusterY0))
            {
                for (int j1 = this.CurrentClusterX0; j1 <= this.CurrentClusterX1; j1++)
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
                    for (int i2 = this.CurrentClusterY0; i2 <= this.CurrentClusterY1; i2 += (this.CurrentClusterY1 - this.CurrentClusterY0))
                    {
                        for (int j2 = this.CurrentClusterX0; j2 <= this.CurrentClusterX1; j2++)
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
            for (int i1 = this.CurrentClusterY0; i1 <= this.CurrentClusterY1; i1++)
            {
                for (int j1 = this.CurrentClusterX0; j1 <= this.CurrentClusterX1; j1 += (this.CurrentClusterX1 - this.CurrentClusterX0))
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

                    for (int i2 = this.CurrentClusterY0; i2 <= this.CurrentClusterY1; i2++)
                    {
                        for (int j2 = this.CurrentClusterX0; j2 <= this.CurrentClusterX1; j2 += (this.CurrentClusterX1 - this.CurrentClusterX0))
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
