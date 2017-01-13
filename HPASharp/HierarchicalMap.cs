using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Infrastructure;
using HPASharp.Search;

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
    public class AbtractEdgeInfo
    {
        public int Cost { get; set; }
        public int Level { get; set; }
        public bool IsInterEdge { get; set; }

        public AbtractEdgeInfo(int cost, int level = 1, bool inter = true)
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
    public class AbstractNodeInfo
    {
        public int Id { get; set; }
        public Position Position { get; set; }
        public int ClusterId { get; set; }
        public int CenterId { get; set; }
        public int Level { get; set; }
        public int LocalEntranceId { get; set; }

        public AbstractNodeInfo(int id, int level, int clId,
                    Position position, int centerId,
                    int localEntranceId)
        {
            Id = id;
            Level = level;
            ClusterId = clId;
            Position = position;
            CenterId = centerId;
            LocalEntranceId = localEntranceId;
        }

        public void PrintInfo()
        {
            Console.Write("id: " + Id);
            Console.Write("; level: " + Level);
            Console.Write("; cluster: " + ClusterId);
            Console.Write("; row: " + Position.Y);
            Console.Write("; col: " + Position.X);
            Console.Write("; center: " + CenterId);
            Console.Write("; local idx: " + LocalEntranceId);
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
    public class HierarchicalMap : IMap
    {
        public int Height { get; set; }
        public int Width { get; set; }
        public Graph<AbstractNodeInfo, AbtractEdgeInfo> AbstractGraph { get; set; }
        public int ClusterSize { get; set; }
        public int MaxLevel { get; set; }
        public List<Cluster> Clusters { get; set; }
	    public int NrNodes { get { return AbstractGraph.Nodes.Count; } }

        // This list, indexed by a node id from the low level, 
        // indicates to which abstract node id it maps. It is a sparse
        // array for quick access. For saving memory space, this could be implemented as a dictionary
        // NOTE: It is currently just used for insert and remove STAL
        public int[] ConcreteNodeIdToAbstractNodeIdMap { get; set; }
        public AbsType Type { get; set; }
		
		private int currentLevel;

		private int currentClusterY0;

		private int currentClusterY1;

		private int currentClusterX0;

		private int currentClusterX1;

		public void SetType(TileType tileType)
        {
            switch(tileType)
            {
                case TileType.Tile:
                    Type = AbsType.ABSTRACT_TILE;
                    break;
                case TileType.Octile:
                    Type = AbsType.ABSTRACT_OCTILE;
                    break;
                case TileType.OctileUnicost:
                    Type = AbsType.ABSTRACT_OCTILE_UNICOST;
                    break;
            }
        }

        public HierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel)
        {
            ClusterSize = clusterSize;
            MaxLevel = maxLevel;
            
            SetType(concreteMap.TileType);
            this.Height = concreteMap.Height;
            this.Width = concreteMap.Width;
            ConcreteNodeIdToAbstractNodeIdMap = new int[this.Height * this.Width];
            for (var i = 0; i < this.Height * this.Width; i++)
                ConcreteNodeIdToAbstractNodeIdMap[i] = -1;

            Clusters = new List<Cluster>();
            AbstractGraph = new Graph<AbstractNodeInfo, AbtractEdgeInfo>();
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
		
        #region Stal Operations - SHOULD EXPORT IT TO THE FACTORY PROBABLY
		
		public Cluster FindClusterForPosition(Position pos)
        {
            var cluster = this.Clusters
                .First(cl =>
                    cl.Origin.Y <= pos.Y &&
                    pos.Y < cl.Origin.Y + cl.Size.Height &&
                    cl.Origin.X <= pos.X &&
                    pos.X < cl.Origin.X + cl.Size.Width);
            return cluster;
        }

        public void AddEdge(int sourceNodeId, int destNodeId, int cost, int level = 1, bool inter = false)
        {
            AbstractGraph.AddEdge(sourceNodeId, destNodeId, new AbtractEdgeInfo(cost, level, inter));
        }

        public List<Graph<AbstractNodeInfo, AbtractEdgeInfo>.Edge> GetNodeEdges(int nodeId)
        {
            var node = AbstractGraph.GetNode(ConcreteNodeIdToAbstractNodeIdMap[nodeId]);
            return node.Edges;
        }

        public Cluster GetCluster(int id)
        {
            return Clusters[id];
        }

	    #endregion

		/// <summary>
		/// Gets the neighbours(successors) of the nodeId for the level set in the currentLevel
		/// </summary>
		public IEnumerable<Neighbour> GetNeighbours(int nodeId)
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
		public void SetCurrentClusterByPositionAndLevel(Position pos, int level)
		{
			// if the level surpasses the MaxLevel, just set the whole map as a cluster
			if (level > MaxLevel)
			{
				currentClusterY0 = 0;
				currentClusterY1 = Height - 1;
				currentClusterX0 = 0;
				currentClusterX1 = Width - 1;
				return;
			}

			var offset = GetOffset(level);
			var nodeY = pos.Y; // nodeId / this.Width;
			var nodeX = pos.X; // nodeId % this.Width;
			currentClusterY0 = nodeY - (nodeY % offset);
			currentClusterY1 = Math.Min(this.Height - 1, this.currentClusterY0 + offset - 1);
			currentClusterX0 = nodeX - (nodeX % offset);
			currentClusterX1 = Math.Min(this.Width - 1, this.currentClusterX0 + offset - 1);
		}

		/// <summary>
		/// Defines the bounding box of the cluster we want to process
		/// </summary>
		public void SetCurrentCluster(int x, int y, int offset)
		{
			currentClusterY0 = y;
			currentClusterX0 = x;
			currentClusterY1 = Math.Min(this.Height - 1, y + offset - 1);
			currentClusterX1 = Math.Min(this.Width - 1, x + offset - 1);
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

        private bool IsValidAbstractNodeForLevel(int abstractNodeId, int level)
        {
            if (abstractNodeId == Constants.NO_NODE)
                return false;

            var nodeInfo1 = AbstractGraph.GetNodeInfo(abstractNodeId);
            return nodeInfo1.Level >= level;
        }

        public void AddEdgesBetweenAbstractNodes(int srcAbstractNodeId, int destAbstractNodeId, int level)
        {
            if (srcAbstractNodeId == destAbstractNodeId || !IsValidAbstractNodeForLevel(destAbstractNodeId, level))
                return;

            var search = new AStar();
            var path = search.FindPath(this, srcAbstractNodeId, destAbstractNodeId);
            if (path.PathCost >= 0)
            {
                AddEdge(srcAbstractNodeId, destAbstractNodeId, path.PathCost, level, false);
                AddEdge(destAbstractNodeId, srcAbstractNodeId, path.PathCost, level, false);
            }
        }

        public void AddEdgesToOtherEntrancesInCluster(AbstractNodeInfo abstractNodeInfo, int level)
        {
            SetCurrentLevel(level - 1);
            SetCurrentClusterByPositionAndLevel(abstractNodeInfo.Position, level);

            for (var y = currentClusterY0; y <= currentClusterY1; y++)
            for (var x = currentClusterX0; x <= currentClusterX1; x++)
            {
                var concreteNodeId = y * Width + x;
                var entranceAbstractNodeId = ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId];
                AddEdgesBetweenAbstractNodes(abstractNodeInfo.Id, entranceAbstractNodeId, level);
            }
        }
    }
}
