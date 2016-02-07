using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public class Neighbour
    {
        public int Target { get; set; }
        public int Cost { get; set; }

        public Neighbour(int target, int cost)
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
    public abstract class AbsTiling : IMap
    {
        public int Height { get; set; }
        public int Width { get; set; }
        protected Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo> Graph { get; set; }
        public int ClusterSize { get; set; }
        protected int MaxLevel { get; set; }
        protected List<Cluster> Clusters { get; set; }
        protected List<Entrance> Entrances { get; set; }

        public int NrNodes => Graph.Nodes.Count;

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

        protected AbsTiling(int clusterSize, int maxLevel, int height, int width)
        {
            ClusterSize = clusterSize;
            MaxLevel = maxLevel;

            Type = AbsType.ABSTRACT_OCTILE;
            this.Height = height;
            this.Width = width;
            AbsNodeIds = new int[height * width];
            for (int i = 0; i < height * width; i++)
                AbsNodeIds[i] = -1;

            Clusters = new List<Cluster>();
            Entrances = new List<Entrance>();
            Graph = new Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>();
        }

        public int GetHeuristic(int start, int target)
        {
            var startNodeInfo = Graph.GetNodeInfo(start);
            var targetNodeInfo = Graph.GetNodeInfo(target);
            var colStart = startNodeInfo.Position.X;
            var colTarget = targetNodeInfo.Position.X;
            var rowStart = startNodeInfo.Position.Y;
            var rowTarget = targetNodeInfo.Position.Y;
            var diffCol = Math.Abs(colTarget - colStart);
            var diffRow = Math.Abs(rowTarget - rowStart);

            switch (Type)
            {
                case AbsType.ABSTRACT_TILE:
                    return (diffCol + diffRow) * 1;
                case AbsType.ABSTRACT_OCTILE:
                    {
                        var diagonal = Math.Min(diffCol, diffRow);
                        var straight = Math.Max(diffCol, diffRow) - diagonal;
                        return straight * Constants.COST_ONE + diagonal * Constants.SQRT2; // The 2 should be 1.41...
                    }
                default:
                    //assert(false);
                    return 0;
            }
        }

        public int GetMinCost()
        {
            return 0;
        }

        public abstract List<Neighbour> GetNeighbours(int nodeId, int lastNodeId);

        public void AddCluster(Cluster cluster)
        {
            Clusters.Add(cluster);
        }

        public void AddEntrance(Entrance entrance)
        {
            Entrances.Add(entrance);
        }

        public virtual void CreateEdges()
        {
            CreateClusterEdges();
        }

        /// <summary>
        /// Gets the cluster Id, determined by its row and column
        /// </summary>
        public Cluster GetCluster(int x, int y)
        {
            var cols = Width / ClusterSize;
            if (Width % ClusterSize > 0)
                cols++;

            return Clusters[y * cols + x];
        }

        public int DetermineLevel(int y)
        {
            var level = 1;
            if (y % ClusterSize != 0)
                y++;

            var clusterY = y / ClusterSize;
            while (clusterY % 2 == 0 && level < MaxLevel)
            {
                clusterY /= 2;
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
        public void AddAbstractNodes()
        {
            var nodeId = 0;
            var absNodes = new Dictionary<int, AbsNode>();
            foreach (var entrance in Entrances)
            {
                var cluster1 = Clusters[entrance.Cluster1Id];
                var cluster2 = Clusters[entrance.Cluster2Id];

                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.HORIZONTAL:
                        level = DetermineLevel(entrance.Center1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = DetermineLevel(entrance.Center1.X);
                        break;
                    default:
                        level = -1;
                        break;
                }

                // use absNodes as a local var to check quickly if a node with the same centerId
                // has been created before
                AbsNode absNode;
                if (!absNodes.TryGetValue(entrance.Center1Id, out absNode))
                {
                    AbsNodeIds[entrance.Center1Id] = nodeId;
                    var node = new AbsNode(nodeId,
                                 entrance.Cluster1Id,
                                 new Position(entrance.Center1.X, entrance.Center1.Y),
                                 entrance.Center1Id);
                    node.Level = level;
                    absNodes[entrance.Center1Id] = node;
                    cluster1.AddEntrance(new LocalEntrance(entrance.Center1Id,
                                               nodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Center1.X - cluster1.Origin.X, entrance.Center1.Y - cluster1.Origin.Y)));
                    absNodes[entrance.Center1Id].LocalIdxCluster = cluster1.GetNrEntrances() - 1;
                    nodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                        absNode.Level = level;
                }
                
                if (!absNodes.TryGetValue(entrance.Center2Id, out absNode))
                {
                    AbsNodeIds[entrance.Center2Id] = nodeId;
                    var node = new AbsNode(nodeId,
                                 entrance.Cluster2Id,
                                 new Position(entrance.Center2.X, entrance.Center2.Y),
                                 entrance.Center2Id);
                    node.Level = level;
                    absNodes[entrance.Center2Id] = node;
                    cluster2.AddEntrance(new LocalEntrance(entrance.Center2Id,
                                               nodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Center2.X - cluster2.Origin.X, entrance.Center2.Y - cluster2.Origin.Y)));
                    absNodes[entrance.Center2Id].LocalIdxCluster = cluster2.GetNrEntrances() - 1;
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
                               absNode.Position, absNode.OriginNodeId,
                               absNode.LocalIdxCluster);
                Graph.AddNode(absNode.Id, n);
            }
        }

        /// <summary>
        /// Computes the paths that lie inside every cluster, 
        /// connecting the several entrances among them
        /// </summary>
        public void ComputeClusterPaths()
        {
            foreach (var cluster in Clusters)
                cluster.ComputePaths();
        }

        public List<int> AbstractPathToLowLevelPath(List<int> absPath, int width)
        {
            var result = new List<int>();
            if (absPath.Count == 0) return result;

            var lastAbsNodeId = absPath[0];

            for (var j = 1; j < absPath.Count; j++)
            {
                var i = absPath[j];

                var currentAbsNodeId = i;
                var currentNodeInfo = Graph.GetNodeInfo(currentAbsNodeId);
                var lastNodeInfo = Graph.GetNodeInfo(lastAbsNodeId);

                var eClusterId = currentNodeInfo.ClusterId;
                var leClusterId = lastNodeInfo.ClusterId;
                var index2 = currentNodeInfo.LocalIdxCluster;
                var index1 = lastNodeInfo.LocalIdxCluster;
                if (eClusterId == leClusterId)
                {
                    // insert the local solution into the global one
                    var cluster = this.GetCluster(eClusterId);
                    if (cluster.GetLocalCenter(index1) != cluster.GetLocalCenter(index2))
                    {
                        var localPath =
                            cluster.ComputePath(cluster.GetLocalCenter(index1),
                                cluster.GetLocalCenter(index2));

                        foreach (var localPoint in localPath)
                        {
                            int val = this.LocalId2GlobalId(localPoint, cluster, width);
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
                    var lastVal = lastNodeInfo.CenterId;
                    var currentVal = currentNodeInfo.CenterId;
                    if (result[result.Count] != lastVal)
                        result.Add(lastVal);
                    result.Add(currentVal);
                }

                lastAbsNodeId = currentAbsNodeId;
            }

            return result;
        }

        public void ConvertVisitedNodes(List<char> absNodes, List<char> llVisitedNodes, int size)
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
        // x and y are the positions where I want to put the node
        public int InsertStal(int nodeId, Position pos, int start)
        {
            var absNodeId = AbsNodeIds[nodeId];
            if (absNodeId != Constants.NO_NODE)
            {
                m_stalLevel[start] = Graph.GetNodeInfo(AbsNodeIds[nodeId]).Level;
                m_stalEdges[start] = GetNodeOutEdges(nodeId);
                m_stalUsed[start] = true;
                return absNodeId;
            }

            m_stalUsed[start] = false;

            // identify the cluster
            var cluster = this.Clusters
                .First(cl => 
                    cl.Origin.Y <= pos.Y && 
                    pos.Y < cl.Origin.Y + cl.Size.Height && 
                    cl.Origin.X <= pos.X && 
                    pos.X < cl.Origin.X + cl.Size.Width);

            // create global entrance
            absNodeId = NrNodes;

            // insert local entrance to cluster and updatePaths(cluster.getNrEntrances() - 1)
            cluster.AddEntrance(new LocalEntrance(nodeId, absNodeId, -1, new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y)));
            cluster.UpdatePaths(cluster.GetNrEntrances() - 1);

            // create new node to the abstract graph (to the level 1)
            Graph.AddNode(absNodeId,
                new AbsTilingNodeInfo(absNodeId, 1,
                                    cluster.Id,
                                    pos, nodeId,
                                    cluster.GetNrEntrances() - 1));

            // add new edges to the abstract graph
            var l = cluster.GetNrEntrances() - 1;
            for (var k = 0; k < cluster.GetNrEntrances() - 1; k++)
            {
                if (cluster.AreConnected(l, k))
                {
                    AddOutEdge(
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetGlobalAbsNodeId(l),
                        cluster.GetDistance(l, k),
                        1,
                        false);
                    AddOutEdge(
                        cluster.GetGlobalAbsNodeId(l),
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetDistance(k, l),
                        1,
                        false);
                }
            }

            AbsNodeIds[nodeId] = NrNodes;
            return absNodeId;
        }

        public void RemoveStal(int nodeId, int stal)
        {
            if (m_stalUsed[stal])
            {
                var nodeInfo = Graph.GetNodeInfo(nodeId);
                nodeInfo.Level = m_stalLevel[stal];
                Graph.RemoveNodeEdges(nodeId);
                Graph.AddNode(nodeId, nodeInfo);
                foreach (var edge in m_stalEdges[stal])
                {
                    int targetNodeId = edge.TargetNodeId;

                    AddOutEdge(nodeId, targetNodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                    AddOutEdge(targetNodeId, nodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                }
            }
            else
            {
                var currentNodeInfo = Graph.GetNodeInfo(nodeId);
                var clusterId = currentNodeInfo.ClusterId;
                var cluster = Clusters[clusterId];
                cluster.RemoveLastEntranceRecord();
                AbsNodeIds[currentNodeInfo.CenterId] = Constants.NO_NODE;
                Graph.RemoveNodeEdges(nodeId);
                Graph.RemoveLastNode();
            }
        }

        protected void AddOutEdge(int sourceNodeId, int destNodeId, int cost, int level = 1, bool inter = false)
        {
            Graph.AddOutEdge(sourceNodeId, destNodeId, new AbsTilingEdgeInfo(cost, level, inter));
        }

        protected void CreateClusterEdges()
        {
            // add cluster edges
            foreach (var cluster in Clusters)
            {
                for (var k = 0; k < cluster.GetNrEntrances(); k++)
                    for (var l = k + 1; l < cluster.GetNrEntrances(); l++)
                    {
                        if (cluster.AreConnected(l, k))
                        {
                            AddOutEdge(cluster.GetGlobalAbsNodeId(k), cluster.GetGlobalAbsNodeId(l), cluster.GetDistance(l, k), 1, false);
                            AddOutEdge(cluster.GetGlobalAbsNodeId(l), cluster.GetGlobalAbsNodeId(k), cluster.GetDistance(k, l), 1, false);
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
                        level = DetermineLevel(entrance.Center1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = DetermineLevel(entrance.Center1.X);
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
                        AddOutEdge(AbsNodeIds[entrance.Center1Id],
                                   AbsNodeIds[entrance.Center2Id], Constants.COST_ONE, level, true);
                        AddOutEdge(AbsNodeIds[entrance.Center2Id],
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

                            AddOutEdge(AbsNodeIds[entrance.Center1Id],
                                       AbsNodeIds[entrance.Center2Id], unit_cost, level, true);
                            AddOutEdge(AbsNodeIds[entrance.Center2Id],
                                       AbsNodeIds[entrance.Center1Id], unit_cost, level, true);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        public List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge> GetNodeOutEdges(int nodeId)
        {
            var node = Graph.GetNode(AbsNodeIds[nodeId]);
            return node.OutEdges;
        }

        public virtual bool PruneNode(int targetNodeId, int nodeId, int lastNodeId)
        {
            var targetNodeInfo = Graph.GetNodeInfo(targetNodeId);
            var lastNodeInfo = Graph.GetNodeInfo(lastNodeId);
            var targetClId = targetNodeInfo.ClusterId;
            var lastClId = lastNodeInfo.ClusterId;

            // if target node is in the same cluster as last node
            return targetClId == lastClId;
        }

        public Cluster GetCluster(int id)
        {
            return Clusters[id];
        }

        private int LocalId2GlobalId(int localId, Cluster cluster, int width)
        {
            var localX = localId % cluster.Size.Width;
            var localY = localId / cluster.Size.Width;
            var result = (localY + cluster.Origin.Y) * width +
                         (localX + cluster.Origin.X);
            return result;
        }

        private int GlobalId2LocalId(int globalId, Cluster cluster, int width)
        {
            var globalY = globalId / width;
            var globalX = globalId % width;
            return (globalY - cluster.Origin.Y) * cluster.Size.Width +
                (globalX - cluster.Origin.X);
        }

        public virtual List<char> GetCharVector()
        {
            var result = new List<char>();
            return result;
        }

        public abstract void PrintGraph();
    }
}
