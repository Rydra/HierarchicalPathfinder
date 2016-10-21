using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Infrastructure;

namespace HPASharp
{
    #region Abstract ConcreteMap support classes

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

    #endregion

    /// <summary>
    /// Abstract maps represent, as the name implies, an abstraction
    /// built over the concrete map.
    /// </summary>
    public abstract class AbstractMap : IMap
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo> AbstractGraph { get; set; }
        public int ClusterSize { get; set; }
        public int MaxLevel { get; set; }
        public List<Cluster> Clusters { get; set; }
	    public int NrNodes { get { return AbstractGraph.Nodes.Count; } }

        // This list, indexed by a node id from the low level, 
        // indicates to which abstract node id it maps. It is a sparse
        // array for quick access. For saving memory space, this could be implemented as a dictionary
        // NOTE: It is currently just used for insert and remove STAL
        public int[] AbsNodeIds { get; set; }
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

        protected AbstractMap(ConcreteMap concreteMap, int clusterSize, int maxLevel)
        {
            ClusterSize = clusterSize;
            MaxLevel = maxLevel;
            
            SetType(concreteMap.TileType);
            this.Height = concreteMap.Height;
            this.Width = concreteMap.Width;
            AbsNodeIds = new int[this.Height * this.Width];
            for (var i = 0; i < this.Height * this.Width; i++)
                AbsNodeIds[i] = -1;

            Clusters = new List<Cluster>();
            AbstractGraph = new Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>();
        }

        public int GetHeuristic(int startNodeId, int targetNodeId)
        {
            var startPos = AbstractGraph.GetNodeInfo(startNodeId).Position;
            var targetPos = AbstractGraph.GetNodeInfo(targetNodeId).Position;
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

        public abstract IEnumerable<Neighbour> GetNeighbours(int nodeId);
		
	    public abstract void CreateHierarchicalEdges();

        #region Stal Operations - SHOULD EXPORT IT TO THE FACTORY PROBABLY

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
            // If the node already existed (for instance, it was the an entrance point already
            // existing in the graph, we need to keep track of the previous status in order
            // to be able to restore it once we delete this STAL
            if (AbsNodeIds[nodeId] != Constants.NO_NODE)
            {
                m_stalLevel[start] = AbstractGraph.GetNodeInfo(AbsNodeIds[nodeId]).Level;
                m_stalEdges[start] = GetNodeEdges(nodeId);
                m_stalUsed[start] = true;
                return AbsNodeIds[nodeId];
            }

            m_stalUsed[start] = false;
            
            var cluster = FindClusterForPosition(pos);

            // create global entrance
            var absNodeId = NrNodes;
            var localEntranceIdx = cluster.AddEntrance(absNodeId, new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y));
            cluster.UpdatePaths(localEntranceIdx);

            AbsNodeIds[nodeId] = absNodeId;

            var info = new AbsTilingNodeInfo(
                absNodeId,
                1,
                cluster.Id,
                pos, 
                nodeId,
                localEntranceIdx);

            AbstractGraph.AddNode(absNodeId, info);

            // add new edges to the abstract graph
            for (var k = 0; k < cluster.GetNrEntrances() - 1; k++)
            {
                if (cluster.AreConnected(localEntranceIdx, k))
                {
                    this.AddEdge(
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetGlobalAbsNodeId(localEntranceIdx),
                        cluster.GetDistance(localEntranceIdx, k));
                    this.AddEdge(
                        cluster.GetGlobalAbsNodeId(localEntranceIdx),
                        cluster.GetGlobalAbsNodeId(k),
                        cluster.GetDistance(k, localEntranceIdx));
                }
            }

            return absNodeId;
        }

        private Cluster FindClusterForPosition(Position pos)
        {
            var cluster = this.Clusters
                .First(cl =>
                    cl.Origin.Y <= pos.Y &&
                    pos.Y < cl.Origin.Y + cl.Size.Height &&
                    cl.Origin.X <= pos.X &&
                    pos.X < cl.Origin.X + cl.Size.Width);
            return cluster;
        }

        public void RemoveStal(int nodeId, int stal)
        {
            if (m_stalUsed[stal])
            {
				// The node was an existing entrance point in the graph. Restore it with
				// the information we kept when inserting
                var nodeInfo = AbstractGraph.GetNodeInfo(nodeId);
                nodeInfo.Level = m_stalLevel[stal];
                AbstractGraph.RemoveNodeEdges(nodeId);
                AbstractGraph.AddNode(nodeId, nodeInfo);
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
                var currentNodeInfo = AbstractGraph.GetNodeInfo(nodeId);
                var clusterId = currentNodeInfo.ClusterId;
                var cluster = Clusters[clusterId];
                cluster.RemoveLastEntranceRecord();
                AbsNodeIds[currentNodeInfo.CenterId] = Constants.NO_NODE;
                AbstractGraph.RemoveNodeEdges(nodeId);
                AbstractGraph.RemoveLastNode();
            }
        }

        public void AddEdge(int sourceNodeId, int destNodeId, int cost, int level = 1, bool inter = false)
        {
            AbstractGraph.AddEdge(sourceNodeId, destNodeId, new AbsTilingEdgeInfo(cost, level, inter));
        }

        public List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge> GetNodeEdges(int nodeId)
        {
            var node = AbstractGraph.GetNode(AbsNodeIds[nodeId]);
            return node.Edges;
        }

        public Cluster GetCluster(int id)
        {
            return Clusters[id];
        }

	    public abstract void SetCurrentCluster(Position pos, int level);

	    public abstract void SetCurrentCluster(int x, int y, int offset);

	    #endregion

	    public abstract void SetCurrentLevel(int level);
	    public abstract Rectangle GetCurrentClusterRectangle();
    }
}
