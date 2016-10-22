using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Search;

namespace HPASharp.Factories
{
    public enum EntranceStyle
    {
        MiddleEntrance, EndEntrance
    }
    
    public class AbstractMapFactory
    {
        const int MAX_ENTRANCE_WIDTH = 6;

        public HierarchicalMap HierarchicalMap { get; set; }
        public ConcreteMap ConcreteMap { get; set; }
        public EntranceStyle EntranceStyle { get; set; }
        public int ClusterSize { get; set; }
		public int MaxLevel { get; set; }

        // Store the location of created entrances. This will help creating nodes and edges        
        public Dictionary<int, AbstractNodeInfo> AbstractNodes { get; set; }

		int[] m_stalLevel = new int[2];
		bool[] m_stalUsed = new bool[2];
		List<Graph<AbstractNodeInfo, AbtractEdgeInfo>.Edge>[] m_stalEdges = new List<Graph<AbstractNodeInfo, AbtractEdgeInfo>.Edge>[2];

		public void CreateHierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel, EntranceStyle style)
        {
            this.ClusterSize = clusterSize;
            this.EntranceStyle = style;
            MaxLevel = maxLevel;
            ConcreteMap = concreteMap;
            HierarchicalMap = new HierarchicalMap(concreteMap, clusterSize, maxLevel);

            List<Entrance> entrances;
            List<Cluster> clusters; 
            CreateEntrancesAndClusters(out entrances, out clusters);
            HierarchicalMap.Clusters = clusters;
			
            CreateAbstractNodes(entrances, clusters);
            CreateEdges(entrances, clusters);
        }
		
		public void RemoveAbstractNode(HierarchicalMap map, int nodeId, int stal)
		{
			var abstractGraph = map.AbstractGraph;

			if (m_stalUsed[stal])
			{
				// The node was an existing entrance point in the graph. Restore it with
				// the information we kept when inserting
				var nodeInfo = abstractGraph.GetNodeInfo(nodeId);
				nodeInfo.Level = m_stalLevel[stal];
				abstractGraph.RemoveEdgesFromNode(nodeId);
				abstractGraph.AddNode(nodeId, nodeInfo);
				foreach (var edge in m_stalEdges[stal])
				{
					var targetNodeId = edge.TargetNodeId;

					map.AddEdge(nodeId, targetNodeId, edge.Info.Cost,
							   edge.Info.Level, edge.Info.IsInterEdge);
					map.AddEdge(targetNodeId, nodeId, edge.Info.Cost,
							   edge.Info.Level, edge.Info.IsInterEdge);
				}
			}
			else
			{
				// Just delete the node from the graph
				var currentNodeInfo = abstractGraph.GetNodeInfo(nodeId);
				var clusterId = currentNodeInfo.ClusterId;
				var cluster = map.Clusters[clusterId];
				cluster.RemoveLastEntranceRecord();
				map.AbsNodeIds[currentNodeInfo.CenterId] = Constants.NO_NODE;
				abstractGraph.RemoveEdgesFromNode(nodeId);
				abstractGraph.RemoveLastNode();
			}
		}

		public int InsertAbstractNode(HierarchicalMap map, Position pos, int start)
		{
			var nodeId = pos.Y * map.Width + pos.X;
			var result = InsertStal(map, nodeId, pos, start);
			InsertStalHEdges(map, nodeId);
			return result;
		}

		// TODO: This can become a HUGE refactor. Basically what this code does is creating entrances
		// abstract nodes and edges like in the previous case where we created entrances and all that kind of stuff.
		// We could leverage this new domain knowledge into the code and get rid of this shit with 
		// a way better design (for instance creating multilevel clusters could be a good approach)!!!!!!!
		private void CreateHierarchicalEdges()
		{
			// Starting from level 2 denotes a serious mess on design, because lvl 1 is
			// used by the clusters.
			for (var level = 2; level <= MaxLevel; level++)
			{
				HierarchicalMap.SetCurrentLevel(level - 1);

				int n = 1 << (level - 1);
				// Group clusters by their level. Each subsequent level doubles the amount of clusters in each group
				var clusterGroups = HierarchicalMap.Clusters.GroupBy(cl => $"{cl.ClusterX / n}_{cl.ClusterY / n}");

				foreach (var clusterGroup in clusterGroups)
				{
					// Fromeach cluster group, only pick those entrances whose level is
					// greater or equal than the current level (e.g. level 4 entrances
					// account for lvl 3, lvl 2 and lvl 1)
					var entrances = clusterGroup.SelectMany(cl =>
							cl.EntrancePoints.Where(ep => GetEntrancePointLevel(ep) >= level)).ToList();
					
					// NOTE: The way we are setting the current cluster is kind of hacky...
					var firstEntrance = entrances.First();
					HierarchicalMap.SetCurrentCluster(
						new Position(firstEntrance.RelativePosition.X + clusterGroup.First().Origin.X, firstEntrance.RelativePosition.Y + clusterGroup.First().Origin.Y),
						level);
					
					foreach (var point1 in entrances)
					foreach (var point2 in entrances)
					{
						if (point1 == point2) continue;
						AddEdgesBetweenAbstractNodes(HierarchicalMap, point1.AbstractNodeId, point2.AbstractNodeId, level);
					}
				}
			}
		}

		private static bool IsValidAbstractNode(HierarchicalMap map, int abstractNode, int level)
		{
			if (abstractNode == Constants.NO_NODE)
				return false;

			var nodeInfo1 = map.AbstractGraph.GetNodeInfo(abstractNode);
			if (nodeInfo1.Level < level)
				return false;

			return true;
		}

		/// <summary>
		/// Adds an edge between two abstract nodes for a given level
		/// </summary>
		private static void AddEdgesBetweenAbstractNodes(HierarchicalMap map, int absNodeId1, int absNodeId2, int level)
		{
			if (absNodeId1 == absNodeId2 || !IsValidAbstractNode(map, absNodeId2, level))
				return;

			var search = new AStar();
			var path = search.FindPath(map, absNodeId1, absNodeId2);
			if (path.PathCost >= 0)
			{
				map.AddEdge(absNodeId1, absNodeId2, path.PathCost, level, false);
				map.AddEdge(absNodeId2, absNodeId1, path.PathCost, level, false);
			}
		}

		private int GetEntrancePointLevel(EntrancePoint entrancePoint)
	    {
		    return HierarchicalMap.AbstractGraph.GetNodeInfo(entrancePoint.AbstractNodeId).Level;
	    }

	    private void CreateEntrancesAndClusters(out List<Entrance> entrances, out List<Cluster> clusters)
        {
            var clusterId = 0;
            var entranceId = 0;
            
            entrances = new List<Entrance>();
			clusters = new List<Cluster>();
            
			for (int top = 0, clusterY = 0; top < ConcreteMap.Height; top += ClusterSize, clusterY++)
            for (int left = 0, clusterX = 0; left < ConcreteMap.Width; left += ClusterSize, clusterX++)
            {
                var width = Math.Min(ClusterSize, ConcreteMap.Width - left);
                var height = Math.Min(ClusterSize, ConcreteMap.Height - top);
                var cluster = new Cluster(ConcreteMap, clusterId++, clusterX, clusterY, new Position(left, top), new Size(width, height));
				clusters.Add(cluster);
                
                var clusterAbove = top > 0 ? GetCluster(clusters, clusterX, clusterY - 1) : null;
                var clusterOnLeft = left > 0 ? GetCluster(clusters, clusterX - 1, clusterY) : null;

                entrances.AddRange(CreateInterClusterEntrances(cluster, clusterAbove, clusterOnLeft, ref entranceId));
            }
        }

        private List<Entrance> CreateInterClusterEntrances(Cluster cluster, Cluster clusterAbove, Cluster clusterOnLeft, ref int entranceId)
        {
            List<Entrance> entrances = new List<Entrance>();
            int top = cluster.Origin.Y;
            int left = cluster.Origin.X;
            
            if (clusterAbove != null)
            {
                var hEntrances = CreateHorizontalEntrances(
                    left,
                    left + cluster.Size.Width - 1,
                    top - 1,
                    clusterAbove.Id,
                    cluster.Id,
                    ref entranceId);

                entrances.AddRange(hEntrances);
            }

            if (clusterOnLeft != null)
            {
                var vEntrances = CreateVerticalEntrances(
                    top,
                    top + cluster.Size.Height - 1,
                    left - 1,
                    clusterOnLeft.Id,
                    cluster.Id,
                    ref entranceId);

                entrances.AddRange(vEntrances);
            }

            return entrances;
        }

        private Cluster GetCluster(List<Cluster> clusters, int left, int top)
        {
            var clustersW = HierarchicalMap.Width / ClusterSize;
            if (HierarchicalMap.Width % ClusterSize > 0)
                clustersW++;

            return clusters[top * clustersW + left];
        }

        private void CreateAbstractNodes(List<Entrance> entrancesList, List<Cluster> clusters)
        {
            var abstractNodes = GenerateAbstractNodes(entrancesList, clusters);

            foreach (var kvp in abstractNodes)
            {
                // TODO: Maybe we can find a way to remove this line of AbsNodesIds
                HierarchicalMap.AbsNodeIds[kvp.Key] = kvp.Value.Id;
                HierarchicalMap.AbstractGraph.AddNode(kvp.Value.Id, kvp.Value);
            }

            AbstractNodes = abstractNodes;
        }

        private void CreateEntranceEdges(Entrance entrance, AbsType type, Dictionary<int, AbstractNodeInfo> absNodes)
        {
            int level;
            switch (entrance.Orientation)
            {
                case Orientation.Horizontal:
                    level = DetermineLevel(entrance.Coord1.Y);
                    break;
                case Orientation.Vertical:
                    level = DetermineLevel(entrance.Coord1.X);
                    break;
                default:
                    level = -1;
                    break;
            }

            var abstractNodeId1 = absNodes[entrance.Coord1Id].Id;
            var abstractNodeId2 = absNodes[entrance.Coord2Id].Id;

            switch (type)
            {
                case AbsType.ABSTRACT_TILE:
                case AbsType.ABSTRACT_OCTILE_UNICOST:
                    // Inter-edges: cost 1
                    var absTilingEdgeInfo1 = new AbtractEdgeInfo(Constants.COST_ONE, level, true);
                    var absTilingEdgeInfo2 = new AbtractEdgeInfo(Constants.COST_ONE, level, true);
                    HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo1);
                    HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo2);
                    break;
                case AbsType.ABSTRACT_OCTILE:
                    {
                        int unitCost;
                        switch (entrance.Orientation)
                        {
                            case Orientation.Horizontal:
                            case Orientation.Vertical:
                                unitCost = Constants.COST_ONE;
                                break;
                            case Orientation.Hdiag2:
                            case Orientation.Hdiag1:
                            case Orientation.Vdiag1:
                            case Orientation.Vdiag2:
                                unitCost = (Constants.COST_ONE * 34) / 24;
                                break;
                            default:
                                unitCost = -1;
                                break;
                        }

                        var absTilingEdgeInfo3 = new AbtractEdgeInfo(unitCost, level, true);
                        var absTilingEdgeInfo4 = new AbtractEdgeInfo(unitCost, level, true);
                        HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo3);
                        HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo4);
                    }
                    break;
            }
        }

        private int DetermineLevel(int y)
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
        private Dictionary<int, AbstractNodeInfo> GenerateAbstractNodes(List<Entrance> entrances, List<Cluster> clusters)
        {
            var abstractNodeId = 0;
            var absNodes = new Dictionary<int, AbstractNodeInfo>();
            foreach (var entrance in entrances)
            {
                var cluster1 = clusters[entrance.Cluster1Id];
                var cluster2 = clusters[entrance.Cluster2Id];

                // Determine the level of this entrance. It is given
                // by its orientation and its coordinates
                int level;
                switch (entrance.Orientation)
                {
                    case Orientation.Horizontal:
                        level = DetermineLevel(entrance.Coord1.Y);
                        break;
                    case Orientation.Vertical:
                        level = DetermineLevel(entrance.Coord1.X);
                        break;
                    default:
                        level = -1;
                        break;
                }

                // use absNodes as a local var to check quickly if a node with the same centerId
                // has been created before
                AbstractNodeInfo absNode;
                if (!absNodes.TryGetValue(entrance.Coord1Id, out absNode))
                {
                    var localEntranceIdx = cluster1.AddEntrance(
                        abstractNodeId, 
                        new Position(entrance.Coord1.X - cluster1.Origin.X, entrance.Coord1.Y - cluster1.Origin.Y));
                    
                    var node = new AbstractNodeInfo(
                        abstractNodeId, 
                        level,
                        entrance.Cluster1Id,
                        new Position(entrance.Coord1.X, entrance.Coord1.Y),
                        entrance.Coord1Id,
                        localEntranceIdx);
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
                    var localEntranceIdx = cluster2.AddEntrance(
                        abstractNodeId,
                        new Position(entrance.Coord2.X - cluster2.Origin.X, entrance.Coord2.Y - cluster2.Origin.Y));

                    var node = new AbstractNodeInfo(
                        abstractNodeId, 
                        level,	
                        entrance.Cluster2Id,
                        new Position(entrance.Coord2.X, entrance.Coord2.Y),
                        entrance.Coord2Id,
                        localEntranceIdx);
                    absNodes[entrance.Coord2Id] = node;

                    abstractNodeId++;
                }
                else
                {
                    if (level > absNode.Level)
                        absNode.Level = level;
                }
            }

            return absNodes;
        }

        private void CreateInterClusterEdges(Cluster cluster)
        {
            cluster.ComputeInternalPaths();

            foreach (var point1 in cluster.EntrancePoints)
			foreach (var point2 in cluster.EntrancePoints)
			{
				if (point1 != point2 && cluster.AreConnected(point1.EntranceEntranceId, point2.EntranceEntranceId))
				{
					var absTilingEdgeInfo1 = new AbtractEdgeInfo(cluster.GetDistance(point1.EntranceEntranceId, point2.EntranceEntranceId), 1, false);
					HierarchicalMap.AbstractGraph.AddEdge(
						point1.AbstractNodeId,
						point2.AbstractNodeId,
						absTilingEdgeInfo1);
				}
			}
        }

        private void CreateEdges(List<Entrance> entrances, List<Cluster> clusters)
        {
            foreach (var entrance in entrances)
            {
                CreateEntranceEdges(entrance, HierarchicalMap.Type, AbstractNodes);
            }

            foreach (var cluster in clusters)
	        {
                CreateInterClusterEdges(cluster);
	        }
			
            CreateHierarchicalEdges();
        }

        private List<Entrance> CreateHorizontalEntrances(
            int rowStart,
            int rowEnd,
            int column,
            int clusterid1,
            int clusterid2,
            ref int currentEntranceId)
        {
            Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesForRow =
                row => Tuple.Create(GetNode(row, column), GetNode(row, column + 1));

            return CreateEntrancesAlongEdge(rowStart, rowEnd, clusterid1, clusterid2, ref currentEntranceId, getNodesForRow, Orientation.Horizontal);
        }

        private List<Entrance> CreateVerticalEntrances(
            int colStart,
            int colEnd,
            int row,
            int clusterid1,
            int clusterid2,
            ref int currentEntranceId)
        {
            Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesForColumn =
                column => Tuple.Create(GetNode(row, column), GetNode(row + 1, column));

            return CreateEntrancesAlongEdge(colStart, colEnd, clusterid1, clusterid2, ref currentEntranceId, getNodesForColumn, Orientation.Vertical);
        }

        private List<Entrance> CreateEntrancesAlongEdge(
            int startPoint,
            int endPoint,
            int clusterid1,
            int clusterid2,
            ref int currentEntranceId,
            Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesInEdge,
            Orientation orientation)
        {
            List<Entrance> entrances = new List<Entrance>();

            for (var entranceStart = startPoint; entranceStart <= endPoint; entranceStart++)
            {
                var entranceEnd = GetEntranceEnd(entranceStart, endPoint, getNodesInEdge);

                var entranceWidth = entranceEnd - entranceStart;
                if (entranceWidth == 0)
                    continue;

                if (EntranceStyle == EntranceStyle.EndEntrance && entranceWidth > MAX_ENTRANCE_WIDTH)
                {
                    var nodes = getNodesInEdge(entranceStart);
                    var srcNode = nodes.Item1;
                    var destNode = nodes.Item2;

                    var entrance1 = new Entrance(currentEntranceId, clusterid1, clusterid2, srcNode, destNode, orientation);

                    currentEntranceId++;

                    nodes = getNodesInEdge(entranceEnd - 1);
                    srcNode = nodes.Item1;
                    destNode = nodes.Item2;

                    var entrance2 = new Entrance(currentEntranceId, clusterid1, clusterid2, srcNode, destNode, orientation);

                    currentEntranceId++;

                    entrances.Add(entrance1);
                    entrances.Add(entrance2);
                }
                else
                {
                    var nodes = getNodesInEdge(((entranceEnd - 1) + entranceStart) / 2);
                    var srcNode = nodes.Item1;
                    var destNode = nodes.Item2;

                    var entrance = new Entrance(currentEntranceId, clusterid1, clusterid2, srcNode, destNode, orientation);

                    currentEntranceId++;
                    entrances.Add(entrance);
                }

                entranceStart = entranceEnd;
            }

            return entrances;
        }

        private int GetEntranceEnd(int entranceStart, int end, Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesInEdge)
        {
            var entranceEnd = entranceStart;

            var nodes = getNodesInEdge(entranceEnd);
            if (NodesAreBlocked(nodes.Item1, nodes.Item2))
                return entranceEnd;

            while (true)
            {
                entranceEnd++;

                if (entranceEnd >= end)
                    break;

                nodes = getNodesInEdge(entranceEnd);
                if (NodesAreBlocked(nodes.Item1, nodes.Item2))
                    break;
            }

            return entranceEnd;
        }

        private Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node GetNode(int top, int left)
        {
            return ConcreteMap.Graph.GetNode(ConcreteMap.GetNodeIdFromPos(top, left));
        }

        private bool NodesAreBlocked(Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node node1, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node node2)
        {
            return node1.Info.IsObstacle || node2.Info.IsObstacle;
        }

        /// <summary>
        /// Inserts a node and creates edges around the local points of the cluster it the
        /// node we try to insert belongs to at each level
        /// </summary>
        private static void InsertStalHEdges(HierarchicalMap map, int nodeId)
		{
			var abstractNodeId = map.AbsNodeIds[nodeId];
			var nodeInfo = map.AbstractGraph.GetNodeInfo(abstractNodeId);
			var oldLevel = nodeInfo.Level;
			nodeInfo.Level = map.MaxLevel;
			for (var level = oldLevel + 1; level <= map.MaxLevel; level++)
			{
				map.SetCurrentLevel(level - 1);
				map.SetCurrentCluster(nodeInfo.Position, level);
				var clusterRectangle = map.GetCurrentClusterRectangle();
				var currentClusterY0 = clusterRectangle.Origin.Y;
				var currentClusterY1 = clusterRectangle.Origin.Y + clusterRectangle.Size.Height;
				var currentClusterX0 = clusterRectangle.Origin.X;
				var currentClusterX1 = clusterRectangle.Origin.X + clusterRectangle.Size.Width;
				for (var y = currentClusterY0; y <= currentClusterY1; y++)
					for (var x = currentClusterX0; x <= currentClusterX1; x++)
					{
						var nodeId2 = y * map.Width + x;
						var abstractNodeId2 = map.AbsNodeIds[nodeId2];
						AddEdgesBetweenAbstractNodes(map, abstractNodeId, abstractNodeId2, level);
					}
			}
		}

		// insert a new node, such as start or target, to the abstract graph and
		// returns the id of the newly created node in the abstract graph
		// x and y are the positions where I want to put the node
		private int InsertStal(HierarchicalMap map, int nodeId, Position pos, int start)
		{
			// If the node already existed (for instance, it was the an entrance point already
			// existing in the graph, we need to keep track of the previous status in order
			// to be able to restore it once we delete this STAL
			if (map.AbsNodeIds[nodeId] != Constants.NO_NODE)
			{
				m_stalLevel[start] = map.AbstractGraph.GetNodeInfo(map.AbsNodeIds[nodeId]).Level;
				m_stalEdges[start] = map.GetNodeEdges(nodeId);
				m_stalUsed[start] = true;
				return map.AbsNodeIds[nodeId];
			}

			m_stalUsed[start] = false;

			var cluster = map.FindClusterForPosition(pos);

			// create global entrance
			var absNodeId = map.NrNodes;
			var localEntranceStartIdx = cluster.AddEntrance(absNodeId, new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y));
			cluster.UpdatePathsForLocalEntrance(localEntranceStartIdx);

			map.AbsNodeIds[nodeId] = absNodeId;

			var info = new AbstractNodeInfo(
				absNodeId,
				1,
				cluster.Id,
				pos,
				nodeId,
				localEntranceStartIdx);

			map.AbstractGraph.AddNode(absNodeId, info);

			// add new edges to the abstract graph
			for (var localEntranceIdx = 0; localEntranceIdx < cluster.NumberOfEntrances - 1; localEntranceIdx++)
			{
				if (cluster.AreConnected(localEntranceStartIdx, localEntranceIdx))
				{
					map.AddEdge(
						cluster.GetAbstractNodeId(localEntranceIdx),
						cluster.GetAbstractNodeId(localEntranceStartIdx),
						cluster.GetDistance(localEntranceStartIdx, localEntranceIdx));
					map.AddEdge(
						cluster.GetAbstractNodeId(localEntranceStartIdx),
						cluster.GetAbstractNodeId(localEntranceIdx),
						cluster.GetDistance(localEntranceIdx, localEntranceStartIdx));
				}
			}

			return absNodeId;
		}
	}
}
