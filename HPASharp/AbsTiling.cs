using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public class Successor
    {
        public int Target { get; set; }
        public int Cost { get; set; }

        public Successor() { }

        public Successor(int target, int cost)
        {
            Target = target;
            Cost = cost;
        }
    }
    
    public enum AbsType {
        ABSTRACT_TILE,
        ABSTRACT_OCTILE,
        ABSTRACT_OCTILE_UNICOST
    }

    // implements an abstract maze decomposition
    // the ultimate abstract representation is a weighted graph of
    // locations connected by precomputed paths
    public class AbsTiling : Environment
    {
        public int Rows { get; set; }
        public int Columns { get; set; }
        public int ClusterSize { get; set; }
        protected Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo> Graph { get; set; }
        protected List<Cluster> Clusters { get; set; }  // used to build the m_graph member
        protected List<Entrance> Entrances { get; set; }  // same
        public int NrAbsNodes { get; set; }
        protected int MaxLevel { get; set; }
        protected int[] AbsNodeIds { get; set; }
        public AbsType Type { get; set; }

        public void SetType(TileType tileType)
        {
            switch(tileType)
            {
                case TileType.TILE:
                    Type = AbsType.ABSTRACT_TILE;
                    break;
                case TileType.OCTILE:
                    Type = AbsType.ABSTRACT_OCTILE;
                    break;
                case TileType.OCTILE_UNICOST:
                    Type = AbsType.ABSTRACT_OCTILE_UNICOST;
                    break;
            }
        }

        public AbsTiling(int clusterSize, int maxLevel, int rows, int columns)
        {
            ClusterSize = clusterSize;
            MaxLevel = maxLevel;

            Type = AbsType.ABSTRACT_OCTILE;
            Rows = rows;
            Columns = columns;
            AbsNodeIds = new int[rows * columns];
            for (int i = 0; i < rows * columns; i++)
                AbsNodeIds[i] = -1;

            Clusters = new List<Cluster>();
            Entrances = new List<Entrance>();
            Graph = new Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>();
        }

        public int getHeuristic(int start, int target)
        {
            var startNodeInfo = Graph.GetNodeInfo(start);
            var targetNodeInfo = Graph.GetNodeInfo(target);
            int colStart = startNodeInfo.Position.X;
            int colTarget = targetNodeInfo.Position.X;
            int rowStart = startNodeInfo.Position.Y;
            int rowTarget = targetNodeInfo.Position.Y;
            int diffCol = Math.Abs(colTarget - colStart);
            int diffRow = Math.Abs(rowTarget - rowStart);

            switch (Type)
            {
                case AbsType.ABSTRACT_TILE:
                    return (diffCol + diffRow) * 1;
                case AbsType.ABSTRACT_OCTILE:
                    {
                        int diagonal = Math.Min(diffCol, diffRow);
                        int straight = Math.Max(diffCol, diffRow) - diagonal;
                        return straight * Constants.COST_ONE + diagonal * Constants.SQRT2; // The 2 should be 1.41...
                    }
                default:
                    //assert(false);
                    return 0;
            }
        }

        public int getMinCost()
        {
            return 0;
        }

        public virtual List<Successor> getSuccessors(int nodeId, int lastNodeId)
        {
            return new List<Successor>();
            //     assert(false);
            //     result.reserve(getMaxEdges());
            //     result.clear();
            //     const AbsTilingNode& node = m_graph.getNode(nodeId);
            //     const vector<AbsTilingEdge>& edges = node.getOutEdges();
            //     for (vector<AbsTilingEdge>::const_iterator i = edges.begin();
            //          i != edges.end(); ++i)
            //     {
            //         int targetNodeId = i->getTargetNodeId();
            //         assert(isValidNodeId(targetNodeId));
            //         if (lastNodeId != NO_NODE)
            //             if (pruneNode(targetNodeId, nodeId, lastNodeId))
            //                 continue;
            //         result.push_back(Successor(targetNodeId, i->getInfo().getCost()));
            //     }
        }

        public bool isValidNodeId(int nodeId)
        {
            return nodeId >= 0 && nodeId < NrAbsNodes;
        }

        public void addCluster(Cluster cluster)
        {
            Clusters.Add(cluster);
        }

        public void addEntrance(Entrance entrance)
        {
            Entrances.Add(entrance);
        }

        public void createGraph()
        {
            createNodes();
            createEdges();
        }

        protected void createNodes()
        {
            //     for (map<int,AbsNode>::const_iterator i = m_absNodes.begin();
            //          i != m_absNodes.end(); ++i)
            //     {
            //         AbsTilingNodeInfo node(i->second.getNodeId(), 1, i->second.getClusterId(),
            //                                i->second.getRow(), i->second.getColumn(), i->second.getCenter(), -1);
            //         m_graph.addNode(i->second.getNodeId(), node);
            //     }
        }

        /// <summary>
        /// Gets the cluster Id, determined by its row and column
        /// </summary>
        public int GetClusterId(int row, int col)
        {
            int cols = (Columns / ClusterSize);
            if (Columns % ClusterSize > 0)
                cols++;
            return row * cols + col;
        }

        public int determineLevel(int row)
        {
            int level = 1;
            if (row % ClusterSize != 0)
                row++;

            int clusterRow = row / ClusterSize;
            while (clusterRow % 2 == 0 && level < MaxLevel)
            {
                clusterRow /= 2;
                level++;
            }
            if (level > MaxLevel)
                level = MaxLevel;
            return level;
        }

        /// <summary>
        /// Create the asbtract nodes of this graph (composed by the centers of
        /// the entrances between clusters)
        /// </summary>
        public void addAbstractNodes()
        {
            int nodeId = 0;
            var absNodes = new Dictionary<int, AbsNode>();
            NrAbsNodes = 0;
            foreach (var entrance in Entrances)
            {
                var cluster1 = Clusters[entrance.Cluster1Id];
                var cluster2 = Clusters[entrance.Cluster2Id];

                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.HORIZONTAL:
                        level = determineLevel(entrance.Center1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = determineLevel(entrance.Center1.X);
                        break;
                    default:
                        level = -1;
                        // assert(false);
                        break;
                }

                // use absNodes as a local var to check quickly if a node with the same centerId
                // has been created before
                AbsNode absNode;
                var found = absNodes.TryGetValue(entrance.Center1Id, out absNode);
                if (!found)
                {
                    AbsNodeIds[entrance.Center1Id] = nodeId;
                    var node = new AbsNode(nodeId,
                                 entrance.Cluster1Id,
                                 new Position(entrance.Center1.X, entrance.Center1.Y),
                                 entrance.Center1Id);
                    node.Level = level;
                    absNodes[entrance.Center1Id] = node;
                    cluster1.addEntrance(new LocalEntrance(entrance.Center1Id,
                                               nodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Center1.X - cluster1.Origin.X, entrance.Center1.Y - cluster1.Origin.Y)));
                    absNodes[entrance.Center1Id].LocalIdxCluster = cluster1.getNrEntrances() - 1;
                    nodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                        absNode.Level = level;
                }

                found = absNodes.TryGetValue(entrance.Center2Id, out absNode);
                if (!found)
                {
                    AbsNodeIds[entrance.Center2Id] = nodeId;
                    var node = new AbsNode(nodeId,
                                 entrance.Cluster2Id,
                                 new Position(entrance.Center2.X, entrance.Center2.Y),
                                 entrance.Center2Id);
                    node.Level = level;
                    absNodes[entrance.Center2Id] = node;
                    cluster2.addEntrance(new LocalEntrance(entrance.Center2Id,
                                               nodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Center2.X - cluster2.Origin.X, entrance.Center2.Y - cluster2.Origin.Y)));
                    absNodes[entrance.Center2Id].LocalIdxCluster = cluster2.getNrEntrances() - 1;
                    nodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                    {
                        absNode.Level = level;
                    }
                }
            }

            // add nodes to the graph
            foreach (var absNode in absNodes.Select(kvp => kvp.Value))
            {
                var n = new AbsTilingNodeInfo(absNode.Id, absNode.Level, absNode.ClusterId,
                               absNode.Position, absNode.Center,
                               absNode.LocalIdxCluster);
                Graph.addNode(absNode.Id, n);
                NrAbsNodes++;
            }
        }

        /// <summary>
        /// Computes the paths that lie inside every cluster, 
        /// connecting the several entrances among them
        /// </summary>
        public void computeClusterPaths()
        {
            foreach (var cluster in Clusters)
                cluster.computePaths();
        }

        public virtual List<char> getCharVector()
        {
              var result = new List<char>();
              return result;
        }

        /// <summary>
        /// Converts an abstract path into a low level path
        /// </summary>
        public void absPath2llPath2(List<int> absPath, out List<int> result, int cols)
        {
            result = new List<int>();
            if (absPath.Count > 0)
            {
                int lastAbsNodeId = -1;
                for (int j = 0; j < absPath.Count; j++)
                {
                    var i = absPath[j];
                    if (j == 0)
                    {
                        lastAbsNodeId = i;
                        continue;
                    }

                    int currentAbsNodeId = i;
                    var currentNodeInfo = Graph.GetNodeInfo(currentAbsNodeId);
                    var lastNodeInfo = Graph.GetNodeInfo(lastAbsNodeId);

                    int eClusterId = currentNodeInfo.ClusterId;
                    int leClusterId = lastNodeInfo.ClusterId;
                    int index2 = currentNodeInfo.LocalIdxCluster;
                    int index1 = lastNodeInfo.LocalIdxCluster;
                    if (eClusterId == leClusterId)
                    {
                        // insert the local solution into the global one
                        var cluster = getCluster(eClusterId);
                        if (cluster.getLocalCenter(index1) != cluster.getLocalCenter(index2))
                        {
                            var localPath =
                                cluster.computePath(cluster.getLocalCenter(index1),
                                                  cluster.getLocalCenter(index2));
                            //assert(localPath.size() > 1);
                            foreach (var localPoint in localPath)
                            {
                                int val = localId2GlobalId(localPoint, cluster, cols);
                                if (result[result.Count - 1] == val)
                                {
                                    continue;
                                }

                                result.Add(val);
                            }
                        }
                    }
                    else
                    {
                        int lastVal = lastNodeInfo.CenterId;
                        int currentVal = currentNodeInfo.CenterId;
                        if (result[result.Count] != lastVal)
                            result.Add(lastVal);
                        result.Add(currentVal);
                    }

                    lastAbsNodeId = currentAbsNodeId;
                }
            }
        }

        public void convertVisitedNodes(List<char> absNodes, List<char> llVisitedNodes, int size)
        {
            for (int i = 0; i < llVisitedNodes.Count; i++)
            {
                llVisitedNodes[i] = ' ';
            }
            
            for (int i = 0; i < absNodes.Count; i++)
                if (absNodes[i] != ' ')
                {
                    var currentNodeInfo = Graph.GetNodeInfo(i);
                    int currentAbsNodeId = currentNodeInfo.CenterId;
                    llVisitedNodes[currentAbsNodeId] = '+';
                }
        }

        int[] m_stalLevel = new int[2];
        bool[] m_stalUsed = new bool[2];
        List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge>[] m_stalEdges = new List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge>[2];

        // insert a new node, such as start or target, to the abstract graph and
        // returns the id of the newly created node in the abstract graph
        // nodeRow and nodeCol are the positions where I want to put the node
        public int insertStal(int nodeId, int nodeRow, int nodeCol, int start)
        {
            int absNodeId = AbsNodeIds[nodeId];
            if (absNodeId != Constants.NO_NODE)
            {
                m_stalLevel[start] = Graph.GetNodeInfo(AbsNodeIds[nodeId]).Level;
                m_stalEdges[start] = getNodeOutEdges(nodeId);
                m_stalUsed[start] = true;
                return absNodeId;
            }

            m_stalUsed[start] = false;

            // identify the cluster
            var cluster = this.Clusters
                .FirstOrDefault(cl => 
                    cl.Origin.Y <= nodeRow && 
                    nodeRow < cl.Origin.Y + cl.Size.Height && 
                    cl.Origin.X <= nodeCol && 
                    nodeCol < cl.Origin.X + cl.Size.Width);

            // create global entrance
            absNodeId = NrAbsNodes;

            // insert local entrance to cluster and updatePaths(cluster.getNrEntrances() - 1)
            cluster.addEntrance(new LocalEntrance(nodeId, absNodeId, -1, new Position(nodeCol - cluster.Origin.X, nodeRow - cluster.Origin.Y)));
            cluster.updatePaths(cluster.getNrEntrances() - 1);

            // create new node to the abstract graph (to the level 1)
            Graph.addNode(absNodeId,
                new AbsTilingNodeInfo(absNodeId, 1,
                                    cluster.Id,
                                    new Position(nodeCol, nodeRow), nodeId,
                                    cluster.getNrEntrances() - 1));

            // add new edges to the abstract graph
            var l = cluster.getNrEntrances() - 1;
            for (var k = 0; k < cluster.getNrEntrances() - 1; k++)
            {
                if (cluster.areConnected(l, k))
                {
                    addOutEdge(
                        cluster.getGlobalAbsNodeId(k),
                        cluster.getGlobalAbsNodeId(l),
                        cluster.getDistance(l, k),
                        1,
                        false);
                    addOutEdge(
                        cluster.getGlobalAbsNodeId(l),
                        cluster.getGlobalAbsNodeId(k),
                        cluster.getDistance(k, l),
                        1,
                        false);
                }
            }

            AbsNodeIds[nodeId] = NrAbsNodes;
            NrAbsNodes++;
            return absNodeId;
        }

        public void removeStal(int nodeId, int stal)
        {
            if (m_stalUsed[stal])
            {
                var nodeInfo = Graph.GetNodeInfo(nodeId);
                nodeInfo.Level = m_stalLevel[stal];
                Graph.removeNodeEdges(nodeId);
                Graph.addNode(nodeId, nodeInfo);
                foreach (var edge in m_stalEdges[stal])
                {
                    int targetNodeId = edge.TargetNodeId;

                    addOutEdge(nodeId, targetNodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                    addOutEdge(targetNodeId, nodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                }
            }
            else
            {
                var currentNodeInfo = Graph.GetNodeInfo(nodeId);
                var clusterId = currentNodeInfo.ClusterId;
                var cluster = Clusters[clusterId];
                cluster.removeLastEntranceRecord();
                AbsNodeIds[currentNodeInfo.CenterId] = Constants.NO_NODE;
                Graph.removeNodeEdges(nodeId);
                Graph.removeLastNode();
                NrAbsNodes--;
            }
        }

        protected void addOutEdge(int initNodeId, int destNodeId, int cost, int level = 1, bool inter = false)
        {
            Graph.addOutEdge(initNodeId, destNodeId, new AbsTilingEdgeInfo(cost, level, inter));
        }

        protected void createEdges()
        {
            // add cluster edges
            foreach (var cluster in Clusters)
            {
                for (int k = 0; k < cluster.getNrEntrances(); k++)
                    for (int l = k + 1; l < cluster.getNrEntrances(); l++)
                    {
                        if (cluster.areConnected(l, k))
                        {
                            addOutEdge(cluster.getGlobalAbsNodeId(k), cluster.getGlobalAbsNodeId(l), cluster.getDistance(l, k), 1, false);
                            addOutEdge(cluster.getGlobalAbsNodeId(l), cluster.getGlobalAbsNodeId(k), cluster.getDistance(k, l), 1, false);
                        }
                    }
            }

            // add transition edges
            foreach (var entrance in Entrances)
            {
                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.HORIZONTAL:
                        level = determineLevel(entrance.Center1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = determineLevel(entrance.Center1.X);
                        break;
                    default:
                        level = -1;
                        break;
                }

                switch (Type)
                {
                    case AbsType.ABSTRACT_TILE:
                    case AbsType.ABSTRACT_OCTILE_UNICOST:
                        // Inter-edges: cost 1
                        addOutEdge(AbsNodeIds[entrance.Center1Id],
                                   AbsNodeIds[entrance.Center2Id], Constants.COST_ONE, level, true);
                        addOutEdge(AbsNodeIds[entrance.Center2Id],
                                   AbsNodeIds[entrance.Center1Id], Constants.COST_ONE, level, true);
                        break;
                    case AbsType.ABSTRACT_OCTILE:
                        {
                            int unit_cost;
                            switch (entrance.Orientation)
                            {
                                case Orientation.HORIZONTAL:
                                case Orientation.VERTICAL:
                                    unit_cost = Constants.COST_ONE;
                                    break;
                                case Orientation.HDIAG2:
                                case Orientation.HDIAG1:
                                case Orientation.VDIAG1:
                                case Orientation.VDIAG2:
                                    unit_cost = Constants.SQRT2; // This should be SQRT(2). I should use doubles instead.
                                    break;
                                default:
                                    unit_cost = -1;
                                    //assert(false);
                                    break;
                            }

                            addOutEdge(AbsNodeIds[entrance.Center1Id],
                                       AbsNodeIds[entrance.Center2Id], unit_cost, level, true);
                            addOutEdge(AbsNodeIds[entrance.Center2Id],
                                       AbsNodeIds[entrance.Center1Id], unit_cost, level, true);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge> getNodeOutEdges(int nodeId)
        {
            var node = Graph.GetNode(AbsNodeIds[nodeId]);
            return node.OutEdges;
        }

        public bool pruneNode(int targetNodeId, int nodeId, int lastNodeId)
        {
            var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);
            var lastNodeInfo = Graph.GetNodeInfo(lastNodeId);
            int targetClId = targetNodeInfo.ClusterId;
            int lastClId = lastNodeInfo.ClusterId;
            // if target node is in the same cluster as last node
            if (targetClId == lastClId)
            {
                return true;
            }
            return false;
        }

        public Cluster getCluster(int id)
        {
            return Clusters[id];
        }

        private int localId2GlobalId(int localId, Cluster cluster, int cols)
        {
            int result;
            int localRow = localId / cluster.Size.Width;
            int localCol = localId % cluster.Size.Width;
            result = (localRow + cluster.Origin.Y) * cols +
                (localCol + cluster.Origin.X);
            return result;
        }

        private int globalId2LocalId(int globalId, Cluster cluster, int cols)
        {
            int globalRow = globalId / cols;
            int globalCol = globalId % cols;
            return (globalRow - cluster.Origin.Y) * cluster.Size.Width +
                (globalCol - cluster.Origin.X);
        }
    }
}
