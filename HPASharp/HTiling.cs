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
        public int CurrentRow1 { get; set; }
        public int CurrentRow2 { get; set; }
        public int CurrentCol1 { get; set; }
        public int CurrentCol2 { get; set; }

        const int NO_NODE = -1;

        public HTiling(int clusterSize, int maxLevel, int rows, int columns) : base(clusterSize, maxLevel, rows, columns)
        {
        }

        /// <summary>
        /// Gets the neighbours(successors) of the nodeId for the level set in the CurrentLevel
        /// </summary>
        public override List<Successor> getSuccessors(int nodeId, int lastNodeId)
        {
            var result = new List<Successor>();
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
                if (targetNodeInfo.Level < this.CurrentLevel || !this.nodeInCurrentCluster(targetNodeInfo))
                    continue;

                if (lastNodeId != NO_NODE)
                {
                    if (pruneNode(targetNodeId, nodeId, lastNodeId))
                    {
                        continue;
                    }
                }

                result.Add(new Successor(targetNodeId, edge.Info.Cost));
            }

            return result;
        }

        public void insertStalHEdges(int nodeId, int nodeRow, int nodeCol)
        {
            var search = new AStar(false);
            var nodeInfo = Graph.GetNodeInfo(AbsNodeIds[nodeId]);
            int oldLevel = nodeInfo.Level;
            nodeInfo.Level = MaxLevel;
            for (int level = oldLevel + 1; level <= MaxLevel; level++)
            {
                this.CurrentLevel = level - 1;
                setCurrentCluster(nodeId, level);
                for (int i2 = this.CurrentRow1; i2 <= this.CurrentRow2; i2++)
                for (int j2 = this.CurrentCol1; j2 <= this.CurrentCol2; j2++)
                {
                    if (AbsNodeIds[i2*Columns+j2] == NO_NODE)
                        continue;
                    if (nodeId == i2*Columns+j2)
                        continue;
                    var nodeInfo2 = Graph.GetNodeInfo(AbsNodeIds[i2*Columns+j2]);
                    if (nodeInfo2.Level < level)
                        continue;
                    {
                        search.FindPath(this, AbsNodeIds[nodeId], AbsNodeIds[i2*Columns+j2]);
                        if (search.PathCost >= 0)
                        {
                            addOutEdge(AbsNodeIds[nodeId],
                                       AbsNodeIds[i2*Columns+j2],
                                       search.PathCost, level, false);
                            addOutEdge(AbsNodeIds[i2*Columns+j2],
                               AbsNodeIds[nodeId],
                               search.PathCost, level, false);
                        }
                    }
                }
            }
        }

        public int insertSTAL(int nodeId, int nodeRow, int nodeCol, int start)
        {
            int result = insertStal(nodeId, nodeRow, nodeCol, start);
            insertStalHEdges(nodeId, nodeRow, nodeCol);
            return result;
        }

        public void createGraph()
        {
            createNodes();
            createEdges();
            createHEdges();
        }

        public void doHierarchicalSearch(int startNodeId, int targetNodeId, out List<int> result, int maxSearchLevel)
        {
            List<int> tmppath, path;
            path = doSearch(startNodeId, targetNodeId, maxSearchLevel, true);
            for (int level = maxSearchLevel; level > 1; level--)
            {
                refineAbsPath(path, level, out tmppath);
                path = tmppath;
            }

            result = path;
        }

        public bool nodeInCurrentCluster(AbsTilingNodeInfo nodeInfo)
        {
            int nodeRow = nodeInfo.Position.Y;
            int nodeCol = nodeInfo.Position.X;
            if (nodeRow < CurrentRow1 || nodeRow > CurrentRow2)
            {
                return false;
            }

            return nodeCol >= this.CurrentCol1 && nodeCol <= this.CurrentCol2;
        }

        public bool pruneNode(int targetNodeId, int nodeId, int lastNodeId)
        {
            // if target node is in the same cluster as last node
            return this.sameCluster(targetNodeId, lastNodeId, this.CurrentLevel);
        }

        public int getOffset(int level)
        {
            return ClusterSize*(1 << (level - 1));
        }

        public void setCurrentCluster(int nodeId, int level)
        {
            if (level > MaxLevel)
            {
                CurrentRow1 = 0;
                CurrentRow2 = Rows - 1;
                CurrentCol1 = 0;
                CurrentCol2 = Columns - 1;
                return;
            }
            int offset = getOffset(level);
            int nodeRow = nodeId / Columns;
            int nodeCol = nodeId % Columns;
            CurrentRow1 = nodeRow - (nodeRow % offset);
            CurrentRow2 = Math.Min(Rows - 1, CurrentRow1 + offset - 1);
            CurrentCol1 = nodeCol - (nodeCol % offset);
            CurrentCol2 = Math.Min(Columns - 1, CurrentCol1 + offset - 1);
        }

        /// <summary>
        /// Defines the bounding box of the cluster we want to process
        /// </summary>
        public void setCurrentCluster(int row, int col, int offset)
        {
            CurrentRow1 = row;
            CurrentCol1 = col;
            CurrentRow2 = Math.Min(Rows - 1, row + offset - 1);
            CurrentCol2 = Math.Min(Columns - 1, col + offset - 1);
        }

        public int getHWidth(int level)
        {
            int result;
            int offset = getOffset(level);
            result = Columns / offset;
            if (Columns % offset > 0)
                result++;
            return result;
        }

        public int getHHeight(int level)
        {
            int result;
            int offset = getOffset(level);
            result = Rows / offset;
            if (Rows % offset > 0)
                result++;
            return result;
        }

        public bool sameCluster(int node1Id, int node2Id, int level)
        {
            var node1Info = Graph.GetNodeInfo(node1Id);
            var node2Info = Graph.GetNodeInfo(node2Id);
            int offset = getOffset(level);
            int node1Row = node1Info.Position.Y;
            int node1Col = node1Info.Position.X;
            int node2Row = node2Info.Position.Y;
            int node2Col = node2Info.Position.X;
            int currentRow1 = node1Row - (node1Row%offset);
            int currentRow2 = node2Row - (node2Row%offset);
            int currentCol1 = node1Col - (node1Col%offset);
            int currentCol2 = node2Col - (node2Col%offset);

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

        public List<int> doSearch(int startNodeId, int targetNodeId, int level, bool mainSearch)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(mainSearch));
            CurrentLevel = level;
            var nodeInfo = Graph.GetNodeInfo(startNodeId);
            if (mainSearch)
            {
                setCurrentCluster(nodeInfo.CenterId, MaxLevel + 1);
            }
            else
                setCurrentCluster(nodeInfo.CenterId, level + 1);
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

        public void refineAbsPath(List<int> path, int level, out List<int> result)
        {
            result = new List<int>();

            // add first elem
            result.Add(path[0]);

            for (int i = 0; i < path.Count - 1; i++)
            {
                // if the two consecutive points belong to the same cluster, compute the path between them and
                // add the resulting nodes of that path to the list
                if (sameCluster(path[i], path[i+1], level))
                {
                    var tmp = doSearch(path[i], path[i+1], level - 1, false);
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
        }

        public void createHEdges()
        {
            for (int level = 2; level <= MaxLevel; level++)
            {
                // The offset determines the distances that will separate clusters in this new level
                int offset = getOffset(level);
                this.CurrentLevel = level - 1;

                // for each cluster
                for (int row = 0; row < Rows; row += offset)
                    for (int col = 0; col < Columns; col += offset)
                    {
                        // define the bounding box of the current cluster we want to analize to create HEdges
                        setCurrentCluster(row, col, offset);

                        // combine nodes on vertical edges:
                        // This runs over each cell of the 2 vertical edges
                        for (int i1 = this.CurrentRow1; i1 <= this.CurrentRow2; i1++)
                        for (int j1 = this.CurrentCol1; j1 <= this.CurrentCol2; j1 += (this.CurrentCol2 - this.CurrentCol1))
                        {
                            if (AbsNodeIds[i1 * Columns + j1] == Constants.NO_NODE)
                                continue;

                            var nodeInfo1 = Graph.GetNodeInfo(AbsNodeIds[i1 * Columns + j1]);
                            if (nodeInfo1.Level < level)
                                continue;

                            for (int i2 = this.CurrentRow1; i2 <= this.CurrentRow2; i2++)
                            for (int j2 = this.CurrentCol1; j2 <= this.CurrentCol2; j2 += (this.CurrentCol2 - this.CurrentCol1))
                            {
                                // Only analize the points that lie forward to the current point we are analizing (in front of i1,j1)
                                if (i1 * Columns + j1 >= i2 * Columns + j2)
                                    continue;
                                this.addOutEdgesBetween(i1, j1, i2, j2, level);
                            }
                        }

                        // combine nodes on horizontal edges:
                        // This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
                        // edges on only horizontal edges)
                        for (int i1 = this.CurrentRow1; i1 <= this.CurrentRow2; i1 += (this.CurrentRow2 - this.CurrentRow1))
                        for (int j1 = this.CurrentCol1; j1 <= this.CurrentCol2; j1++)
                        {
                            if (AbsNodeIds[i1 * Columns + j1] == Constants.NO_NODE)
                                continue;
                            var nodeInfo1 = Graph.GetNodeInfo(AbsNodeIds[i1 * Columns + j1]);
                            if (nodeInfo1.Level < level)
                                continue;
                            for (int i2 = this.CurrentRow1; i2 <= this.CurrentRow2; i2 += (this.CurrentRow2 - this.CurrentRow1))
                            for (int j2 = this.CurrentCol1; j2 <= this.CurrentCol2; j2++)
                            {
                                if (i1 * Columns + j1 >= i2 * Columns + j2)
                                    continue;
                                this.addOutEdgesBetween(i1, j1, i2, j2, level);
                            }
                        }

                        // combine nodes on horizontal and vertical edges:
                        // This runs over each cell of the 2 horizontal edges against the vertical edges
                        for (int i1 = this.CurrentRow1; i1 <= this.CurrentRow2; i1 += (this.CurrentRow2 - this.CurrentRow1))
                        for (int j1 = this.CurrentCol1 + 1; j1 < this.CurrentCol2; j1++)
                        {
                            if (AbsNodeIds[i1 * Columns + j1] == Constants.NO_NODE)
                                continue;
                            var nodeInfo1 = Graph.GetNodeInfo(AbsNodeIds[i1 * Columns + j1]);
                            if (nodeInfo1.Level < level)
                                continue;
                            for (int i2 = this.CurrentRow1 + 1; i2 < this.CurrentRow2; i2++)
                            for (int j2 = this.CurrentCol1; j2 <= this.CurrentCol2; j2 += (this.CurrentCol2 - this.CurrentCol1))
                            {
                                this.addOutEdgesBetween(i1, j1, i2, j2, level);
                            }
                        }
                    }
            }
        }

        private void addOutEdgesBetween(int i1, int j1, int i2, int j2, int level)
        {
            if (this.AbsNodeIds[i2 * this.Columns + j2] == Constants.NO_NODE)
            {
                return;
            }

            var nodeInfo2 = this.Graph.GetNodeInfo(this.AbsNodeIds[i2 * this.Columns + j2]);
            if (nodeInfo2.Level < level)
            {
                return;
            }

            var search = new AStar(false);
            search.FindPath(this, this.AbsNodeIds[i1 * this.Columns + j1], this.AbsNodeIds[i2 * this.Columns + j2]);
            if (search.PathCost >= 0)
            {
                this.addOutEdge(this.AbsNodeIds[i1 * this.Columns + j1], this.AbsNodeIds[i2 * this.Columns + j2], search.PathCost, level, false);
                this.addOutEdge(this.AbsNodeIds[i2 * this.Columns + j2], this.AbsNodeIds[i1 * this.Columns + j1], search.PathCost, level, false);
            }
        }

        #region Printing

        public void printGraph()
        {
            Console.WriteLine("Printing abstract graph:");
            for (int id = 0; id < NrAbsNodes; id++)
            {
                var edges = Graph.getOutEdges(id);
                Console.WriteLine("Node " + id + "; BF "+ edges.Count);
                var nodeInfo = Graph.GetNodeInfo(id);
                nodeInfo.printInfo();
                foreach (var edge in edges)
                {
                    Console.Write("Edge to node " + edge.TargetNodeId + ": ");
                    edge.Info.printInfo();
                }
                
                Console.WriteLine();
            }
        }
        

        #endregion
    }
}
