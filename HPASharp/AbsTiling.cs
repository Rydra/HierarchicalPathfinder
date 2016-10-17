using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    #region Abstract Tiling support classes

    public struct Neighbour
    {
        public int Target;
        public int Cost;

        public Neighbour(int target, int cost)
        {
            Target = target;
            Cost = cost;
        }
    }

    // implements edges in the abstract graph
    public class AbsTilingEdgeInfo
    {
        public int Cost { get; set; }
        public int Level { get; set; }
        public bool IsInterEdge { get; set; }

        public AbsTilingEdgeInfo(int cost, int level = 1, bool inter = true)
        {
            Cost = cost;
            Level = level;
            IsInterEdge = inter;
        }
        public override string ToString()
        {
            return ("cost: " + Cost + "; level: " + Level + "; inter: " + IsInterEdge);
        }

        public void PrintInfo()
        {
            Console.WriteLine(this.ToString());
        }
    }

    // implements nodes in the abstract graph
    public class AbsTilingNodeInfo
    {
        public int Id { get; set; }
        public Position Position { get; set; }
        public int ClusterId { get; set; }
        public int CenterId { get; set; }
        public int Level { get; set; }
        public int LocalIdxCluster { get; set; }

        public AbsTilingNodeInfo(int id, int level, int clId,
                    Position position, int centerId,
                    int localIdxCluster)
        {
            Id = id;
            Level = level;
            ClusterId = clId;
            Position = position;
            CenterId = centerId;
            LocalIdxCluster = localIdxCluster;
        }

        public void PrintInfo()
        {
            Console.Write("id: " + Id);
            Console.Write("; level: " + Level);
            Console.Write("; cluster: " + ClusterId);
            Console.Write("; row: " + Position.Y);
            Console.Write("; col: " + Position.X);
            Console.Write("; center: " + CenterId);
            Console.Write("; local idx: " + LocalIdxCluster);
            Console.WriteLine();
        }
    }
    
    public enum AbsType {
        ABSTRACT_TILE,
        ABSTRACT_OCTILE,
        ABSTRACT_OCTILE_UNICOST
    }

	/// <summary>
	/// Represents an entrance point between 2 clusters
	/// </summary>
	public class Entrance
	{
		public int Id { get; set; }
		public int Cluster1Id { get; set; }
		public int Cluster2Id { get; set; }
		public int Coord1Id { get; set; }
		public int Coord2Id { get; set; }
		public Orientation Orientation { get; set; }
		public Position Coord1 { get; set; }

		public Position Coord2
		{
			get
			{
				int x;
				switch (Orientation)
				{
					case Orientation.HORIZONTAL:
					case Orientation.HDIAG2:
						x = this.Coord1.X;
						break;
					case Orientation.VERTICAL:
					case Orientation.VDIAG2:
					case Orientation.VDIAG1:
					case Orientation.HDIAG1:
						x = this.Coord1.X + 1;
						break;
					default:
						//assert(false);
						x = -1;
						break;
				}

				int y;
				switch (Orientation)
				{
					case Orientation.HORIZONTAL:
					case Orientation.HDIAG1:
					case Orientation.HDIAG2:
					case Orientation.VDIAG1:
						y = this.Coord1.Y + 1;
						break;
					case Orientation.VERTICAL:
					case Orientation.VDIAG2:
						y = this.Coord1.Y;
						break;
					default:
						//assert(false);
						y = -1;
						break;
				}

				return new Position(x, y);
			}
		}

		public Entrance(int id, int cl1Id, int cl2Id, int center1Row, int center1Col, int coord1Id, int coord2Id, Orientation orientation)
		{
			Id = id;
			Cluster1Id = cl1Id;
			Cluster2Id = cl2Id;

			int center1y, center1x;
			if (orientation == Orientation.HDIAG2)
				center1x = center1Col + 1;
			else
				center1x = center1Col;

			if (orientation == Orientation.VDIAG2)
				center1y = center1Row + 1;
			else
				center1y = center1Row;

			this.Coord1 = new Position(center1x, center1y);
			this.Coord1Id = coord1Id;
			this.Coord2Id = coord2Id;
			Orientation = orientation;
		}
	}

    #endregion

    // implements an abstract maze decomposition
    // the ultimate abstract representation is a weighted graph of
    // locations connected by precomputed paths
    public abstract class AbsTiling : IMap
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo> Graph { get; set; }
        public int ClusterSize { get; set; }
        protected int MaxLevel { get; set; }
        protected List<Cluster> Clusters { get; set; }
        protected List<Entrance> Entrances { get; set; }
	    public int NrNodes { get { return Graph.Nodes.Count; } }

        // This list, indexed by a node id from the low level, 
        // indicates to which abstract node id it maps. It is a sparse
        // array for quick access. For saving memory space, this could be implemented as a dictionary
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
            for (var i = 0; i < height * width; i++)
                AbsNodeIds[i] = -1;

            Clusters = new List<Cluster>();
            Entrances = new List<Entrance>();
            Graph = new Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>();
        }

        public int GetHeuristic(int start, int target)
        {
            var startPos = Graph.GetNodeInfo(start).Position;
            var targetPos = Graph.GetNodeInfo(target).Position;
            var diffY = Math.Abs(startPos.Y - targetPos.Y);
            var diffX = Math.Abs(startPos.X - targetPos.X);

            switch (Type)
            {
                case AbsType.ABSTRACT_TILE:
					// Manhattan distance
                    return (diffY + diffX) * Constants.COST_ONE;
                case AbsType.ABSTRACT_OCTILE:
					// Diagonal distance
                    {
                        var diag = Math.Min(diffX, diffY);
                        var straight = diffX + diffY;

                        // According to the information link, this is the shape of the function.
                        // We just extract factors to simplify.
                        // Possible simplification: var h = Constants.CellCost * (straight + (Constants.Sqrt2 - 2) * diag);
                        return Constants.COST_ONE * straight + (Constants.COST_ONE * 34 / 24 - 2 * Constants.COST_ONE) * diag;
                    }
                default:
                    return 0;
            }
        }

        public abstract List<PathNode> DoHierarchicalSearch(int startNodeId, int targetNodeId, int maxSearchLevel, int maxPathsToRefine = int.MaxValue);

        public abstract List<PathNode> RefineAbstractPath(List<PathNode> path, int level, int maxPathsToRefine = int.MaxValue);

        public abstract IEnumerable<Neighbour> GetNeighbours(int nodeId);

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
            CreateInterClusterEdges();
        }

        /// <summary>
        /// Gets the cluster Id, determined by its row and column
        /// </summary>
        public Cluster GetCluster(int x, int y)
        {
            var clustersW = Width / ClusterSize;
            if (Width % ClusterSize > 0)
                clustersW++;

            return Clusters[y * clustersW + x];
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
        public IEnumerable<AbsTilingNodeInfo> GenerateAbstractNodes()
        {
            var abstractNodeId = 0;
            var absNodes = new Dictionary<int, AbsTilingNodeInfo>();
            foreach (var entrance in Entrances)
            {
                var cluster1 = Clusters[entrance.Cluster1Id];
                var cluster2 = Clusters[entrance.Cluster2Id];

                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.HORIZONTAL:
                        level = DetermineLevel(entrance.Coord1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = DetermineLevel(entrance.Coord1.X);
                        break;
                    default:
                        level = -1;
                        break;
                }

                // use absNodes as a local var to check quickly if a node with the same centerId
                // has been created before
                AbsTilingNodeInfo absNode;
                if (!absNodes.TryGetValue(entrance.Coord1Id, out absNode))
                {
                    // NOTE: Violation of SRP...
                    cluster1.AddEntrance(new LocalEntrance(
                                               abstractNodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Coord1.X - cluster1.Origin.X, entrance.Coord1.Y - cluster1.Origin.Y)));

                    // NOTE: Provoking a side-effect... not good
                    AbsNodeIds[entrance.Coord1Id] = abstractNodeId;
                    var node = new AbsTilingNodeInfo(abstractNodeId, level,
                                 entrance.Cluster1Id,
                                 new Position(entrance.Coord1.X, entrance.Coord1.Y),
                                 entrance.Coord1Id, cluster1.GetNrEntrances() - 1);
                    absNodes[entrance.Coord1Id] = node;
                    abstractNodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                        absNode.Level = level;
                }
                
                if (!absNodes.TryGetValue(entrance.Coord2Id, out absNode))
                {
                    cluster2.AddEntrance(new LocalEntrance(
                                               abstractNodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Coord2.X - cluster2.Origin.X, entrance.Coord2.Y - cluster2.Origin.Y)));

                    AbsNodeIds[entrance.Coord2Id] = abstractNodeId;
                    var node = new AbsTilingNodeInfo(abstractNodeId, level,
                                 entrance.Cluster2Id,
                                 new Position(entrance.Coord2.X, entrance.Coord2.Y),
                                 entrance.Coord2Id, cluster2.GetNrEntrances() - 1);
                    absNodes[entrance.Coord2Id] = node;
                    abstractNodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                        absNode.Level = level;
                }
            }

            return absNodes.Values;
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

        public List<PathNode> AbstractPathToLowLevelPath(List<PathNode> absPath, int width, int maxPathsToCalculate = int.MaxValue)
        {
            var result = new List<PathNode>(absPath.Count * 10);
            if (absPath.Count == 0) return result;

            var calculatedPaths = 0;
            var lastAbsNodeId = absPath[0].Id;

            for (var j = 1; j < absPath.Count; j++)
            {
                var currentAbsNodeId = absPath[j].Id;
                var currentNodeInfo = Graph.GetNodeInfo(currentAbsNodeId);
                var lastNodeInfo = Graph.GetNodeInfo(lastAbsNodeId);

                // We cannot compute a low level path from a level which is higher than lvl 1
                // (obvious...) therefore, ignore any non-refined path
                if (absPath[j].Level > 1)
                {
                    result.Add(absPath[j]);
                    continue;
                }

                var eClusterId = currentNodeInfo.ClusterId;
                var leClusterId = lastNodeInfo.ClusterId;
                
                if (eClusterId == leClusterId && calculatedPaths < maxPathsToCalculate)
                {
                    // insert the local solution into the global one
                    var cluster = this.GetCluster(eClusterId);
                    var localpos1 = cluster.GetLocalPosition(lastNodeInfo.LocalIdxCluster);
                    var localpos2 = cluster.GetLocalPosition(currentNodeInfo.LocalIdxCluster);
                    if (localpos1 != localpos2)
                    {
                        var localPath = cluster.ComputePath(localpos1, localpos2)
                            .Select(
                                lp =>
                                    {
                                        var localPoint = LocalClusterId2GlobalId(lp, cluster, width);
                                        return new PathNode(localPoint, 0);
                                    });

                        result.AddRange(localPath);

                        calculatedPaths++;
                    }
                }
                else
                {
                    var lastVal = lastNodeInfo.CenterId;
                    var currentVal = currentNodeInfo.CenterId;
                    if (result[result.Count - 1].Id != lastVal)
                        result.Add(new PathNode(lastVal, 0));

                    result.Add(new PathNode(currentVal, 0));
                }

                lastAbsNodeId = currentAbsNodeId;
            }

            return result;
        }

        public void ConvertVisitedNodes(List<char> absNodes, List<char> llVisitedNodes, int size)
        {
            for (var i = 0; i < llVisitedNodes.Count; i++)
            {
                llVisitedNodes[i] = ' ';
            }
            
            for (var i = 0; i < absNodes.Count; i++)
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

        /// <summary>
        /// Inserts an abstract node in the position pos and returns 
        /// the Abstract node id of that inserted node
        /// </summary>
	    public abstract int InsertSTAL(Position pos, int start);

        // insert a new node, such as start or target, to the abstract graph and
        // returns the id of the newly created node in the abstract graph
        // x and y are the positions where I want to put the node
        public int InsertStal(int nodeId, Position pos, int start)
        {
            if (AbsNodeIds[nodeId] != Constants.NO_NODE)
            {
                // If the node already existed (for instance, it was the an entrance point already
				// existing in the graph, we need to keep track of the previous status in order
				// to be able to restore it once we delete this STAL
                m_stalLevel[start] = Graph.GetNodeInfo(AbsNodeIds[nodeId]).Level;
                m_stalEdges[start] = GetNodeEdges(nodeId);
                m_stalUsed[start] = true;
                return AbsNodeIds[nodeId];
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
            var absNodeId = NrNodes;

            // insert local entrance to cluster and updatePaths(cluster.getNrEntrances() - 1)
            var localEntrance = new LocalEntrance(
                absNodeId,
                -1,
                new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y));
            cluster.AddEntrance(localEntrance);
            cluster.UpdatePaths(localEntrance.EntranceLocalIdx);

            AbsNodeIds[nodeId] = absNodeId;

            // create new node to the abstract graph (to the level 1)
            Graph.AddNode(absNodeId,
                new AbsTilingNodeInfo(absNodeId, 1,
                                    cluster.Id,
                                    pos, nodeId,
                                    cluster.GetNrEntrances() - 1));

            // add new edges to the abstract graph
            var entranceLocalIdx = localEntrance.EntranceLocalIdx;
            for (var k = 0; k < cluster.GetNrEntrances() - 1; k++)
            {
                if (cluster.AreConnected(entranceLocalIdx, k))
                {
                    this.AddEdge(
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetGlobalAbsNodeId(entranceLocalIdx),
                        cluster.GetDistance(entranceLocalIdx, k));
                    this.AddEdge(
                        cluster.GetGlobalAbsNodeId(entranceLocalIdx),
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetDistance(k, entranceLocalIdx));
                }
            }

            return absNodeId;
        }

        public void RemoveStal(int nodeId, int stal)
        {
            if (m_stalUsed[stal])
            {
				// The node was an existing entrance point in the graph. Restore it with
				// the information we kept when inserting
                var nodeInfo = Graph.GetNodeInfo(nodeId);
                nodeInfo.Level = m_stalLevel[stal];
                Graph.RemoveNodeEdges(nodeId);
                Graph.AddNode(nodeId, nodeInfo);
                foreach (var edge in m_stalEdges[stal])
                {
                    var targetNodeId = edge.TargetNodeId;

                    this.AddEdge(nodeId, targetNodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                    this.AddEdge(targetNodeId, nodeId, edge.Info.Cost,
                               edge.Info.Level, edge.Info.IsInterEdge);
                }
            }
            else
            {
				// Just delete the node from the graph
                var currentNodeInfo = Graph.GetNodeInfo(nodeId);
                var clusterId = currentNodeInfo.ClusterId;
                var cluster = Clusters[clusterId];
                cluster.RemoveLastEntranceRecord();
                AbsNodeIds[currentNodeInfo.CenterId] = Constants.NO_NODE;
                Graph.RemoveNodeEdges(nodeId);
                Graph.RemoveLastNode();
            }
        }

        public void AddEdge(int sourceNodeId, int destNodeId, int cost, int level = 1, bool inter = false)
        {
            Graph.AddEdge(sourceNodeId, destNodeId, new AbsTilingEdgeInfo(cost, level, inter));
        }

        protected void CreateInterClusterEdges()
        {
            // add cluster edges
            foreach (var cluster in Clusters)
            {
                for (var k = 0; k < cluster.GetNrEntrances(); k++)
                    for (var l = k + 1; l < cluster.GetNrEntrances(); l++)
                    {
                        if (cluster.AreConnected(l, k))
                        {
                            this.AddEdge(cluster.GetGlobalAbsNodeId(k), cluster.GetGlobalAbsNodeId(l), cluster.GetDistance(l, k), 1, false);
                            this.AddEdge(cluster.GetGlobalAbsNodeId(l), cluster.GetGlobalAbsNodeId(k), cluster.GetDistance(k, l), 1, false);
                        }
                    }
            }

            foreach (var entrance in Entrances)
            {
                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.HORIZONTAL:
                        level = DetermineLevel(entrance.Coord1.Y);
                        break;
                    case Orientation.VERTICAL:
                        level = DetermineLevel(entrance.Coord1.X);
                        break;
                    default:
                        level = -1;
                        break;
                }

                var abstractNodeId1 = AbsNodeIds[entrance.Coord1Id];
                var abstractNodeId2 = AbsNodeIds[entrance.Coord2Id];

                switch (Type)
                {
                    case AbsType.ABSTRACT_TILE:
                    case AbsType.ABSTRACT_OCTILE_UNICOST:
                        // Inter-edges: cost 1
                        this.AddEdge(abstractNodeId1, abstractNodeId2, Constants.COST_ONE, level, true);
                        this.AddEdge(abstractNodeId2, abstractNodeId1, Constants.COST_ONE, level, true);
                        break;
                    case AbsType.ABSTRACT_OCTILE:
                    {
                        int unitCost;
                        switch (entrance.Orientation)
                        {
                            case Orientation.HORIZONTAL:
                            case Orientation.VERTICAL:
                                unitCost = Constants.COST_ONE;
                                break;
                            case Orientation.HDIAG2:
                            case Orientation.HDIAG1:
                            case Orientation.VDIAG1:
                            case Orientation.VDIAG2:
                                unitCost = (Constants.COST_ONE*34)/24;
                                break;
                            default:
                                unitCost = -1;
                                break;
                        }

                        this.AddEdge(abstractNodeId1, abstractNodeId2, unitCost, level, true);
                        this.AddEdge(abstractNodeId2, abstractNodeId1, unitCost, level, true);
                    }
                        break;
                    default:
                        break;
                }
            }
        }

        public List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge> GetNodeEdges(int nodeId)
        {
            var node = Graph.GetNode(AbsNodeIds[nodeId]);
            return node.Edges;
        }

        public virtual bool PruneNode(int targetNodeId, int lastNodeId)
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

        private static int LocalClusterId2GlobalId(int localId, Cluster cluster, int width)
        {
            var localX = localId % cluster.Size.Width;
            var localY = localId / cluster.Size.Width;
            var result = (localY + cluster.Origin.Y) * width +
                         (localX + cluster.Origin.X);
            return result;
        }

        private static int GlobalId2LocalId(int globalId, Cluster cluster, int width)
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
