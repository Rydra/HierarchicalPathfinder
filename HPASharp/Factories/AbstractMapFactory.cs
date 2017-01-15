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
		
		int[] m_stalLevel = new int[2];
		bool[] m_stalUsed = new bool[2];
		List<Graph<AbstractNodeInfo, AbtractEdgeInfo>.Edge>[] m_stalEdges = new List<Graph<AbstractNodeInfo, AbtractEdgeInfo>.Edge>[2];

		public void CreateHierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel, EntranceStyle style)
        {
            ClusterSize = clusterSize;
            EntranceStyle = style;
            MaxLevel = maxLevel;
            ConcreteMap = concreteMap;
            HierarchicalMap = new HierarchicalMap(concreteMap, clusterSize, maxLevel);

            List<Entrance> entrances;
            List<Cluster> clusters; 
            CreateEntrancesAndClusters(out entrances, out clusters);
            HierarchicalMap.Clusters = clusters;
			
            CreateAbstractNodes(entrances);
            CreateEdges(entrances, clusters);
        }

		#region Graph manipulation
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
				map.ConcreteNodeIdToAbstractNodeIdMap[currentNodeInfo.ConcreteNodeId] = Constants.NO_NODE;
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
		
		/// <summary>
		/// Inserts a node and creates edges around the local points of the cluster it the
		/// node we try to insert belongs to at each level
		/// </summary>  
		private static void InsertStalHEdges(HierarchicalMap map, int concreteNodeId)
		{
			var abstractNodeId = map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId];
			var abstractNodeInfo = map.AbstractGraph.GetNodeInfo(abstractNodeId);
			var oldLevel = abstractNodeInfo.Level;
			abstractNodeInfo.Level = map.MaxLevel;
			for (var level = oldLevel + 1; level <= map.MaxLevel; level++)
			{
				map.AddEdgesToOtherEntrancesInCluster(abstractNodeInfo, level);
			}
		}

        // insert a new node, such as start or target, to the abstract graph and
		// returns the id of the newly created node in the abstract graph
		// x and y are the positions where I want to put the node
		private int InsertStal(HierarchicalMap map, int concreteNodeId, Position pos, int start)
		{
			// If the node already existed (for instance, it was the an entrance point already
			// existing in the graph, we need to keep track of the previous status in order
			// to be able to restore it once we delete this STAL
			if (map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId] != Constants.NO_NODE)
			{
				m_stalLevel[start] = map.AbstractGraph.GetNodeInfo(map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId]).Level;
				m_stalEdges[start] = map.GetNodeEdges(concreteNodeId);
				m_stalUsed[start] = true;
				return map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId];
			}

			m_stalUsed[start] = false;

			var cluster = map.FindClusterForPosition(pos);

			// create global entrance
			var abstractNodeId = map.NrNodes;
			var entrance = cluster.AddEntrance(abstractNodeId, new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y));
			cluster.UpdatePathsForLocalEntrance(entrance);

			map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId] = abstractNodeId;

			var info = new AbstractNodeInfo(
				abstractNodeId,
				1,
				cluster.Id,
				pos,
				concreteNodeId);

			map.AbstractGraph.AddNode(abstractNodeId, info);

			foreach (var entrancePoint in cluster.EntrancePoints)
			{
				if (cluster.AreConnected(abstractNodeId, entrancePoint.AbstractNodeId))
				{
					map.AddEdge(
						entrancePoint.AbstractNodeId,
						abstractNodeId,
						cluster.GetDistance(entrancePoint.AbstractNodeId, abstractNodeId));
					map.AddEdge(
						abstractNodeId,
						entrancePoint.AbstractNodeId,
						cluster.GetDistance(abstractNodeId, entrancePoint.AbstractNodeId));
				}

			}

			// add new edges to the abstract graph
			for (var localEntranceIdx = 0; localEntranceIdx < cluster.NumberOfEntrances - 1; localEntranceIdx++)
			{
				
			}

			return abstractNodeId;
		}
		#endregion


		private void CreateEdges(List<Entrance> entrances, List<Cluster> clusters)
		{
			foreach (var entrance in entrances)
			{
				CreateEntranceEdges(entrance, HierarchicalMap.Type);
			}

			foreach (var cluster in clusters)
			{
				CreateInterClusterEdges(cluster);
			}

			HierarchicalMap.CreateHierarchicalEdges();
		}

		private void CreateEntranceEdges(Entrance entrance, AbsType type)
		{
			var level = entrance.GetEntranceLevel(ClusterSize, MaxLevel);

			var srcAbstractNodeId = HierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[entrance.SrcNode.NodeId];
			var destAbstractNodeId = HierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[entrance.DestNode.NodeId];
			
			var orientation = entrance.Orientation;
			int cost = Constants.COST_ONE;
			switch (type)
			{
				case AbsType.ABSTRACT_TILE:
				case AbsType.ABSTRACT_OCTILE_UNICOST:
					// Inter-edges: cost 1
					cost = Constants.COST_ONE;
					break;
				case AbsType.ABSTRACT_OCTILE:
					{
						int unitCost;
						switch (orientation)
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

						cost = unitCost;
					}
					break;
			}
			
			HierarchicalMap.AbstractGraph.AddEdge(srcAbstractNodeId, destAbstractNodeId, new AbtractEdgeInfo(cost, level, true));
			HierarchicalMap.AbstractGraph.AddEdge(destAbstractNodeId, srcAbstractNodeId, new AbtractEdgeInfo(cost, level, true));
		}
        
		private void CreateInterClusterEdges(Cluster cluster)
		{
			cluster.ComputeInternalPaths();

			foreach (var point1 in cluster.EntrancePoints)
			foreach (var point2 in cluster.EntrancePoints)
			{
				if (point1 != point2 && cluster.AreConnected(point1.AbstractNodeId, point2.AbstractNodeId))
				{
					var abtractEdgeInfo = new AbtractEdgeInfo(cluster.GetDistance(point1.AbstractNodeId, point2.AbstractNodeId), 1, false);
					HierarchicalMap.AbstractGraph.AddEdge(
						point1.AbstractNodeId,
						point2.AbstractNodeId,
						abtractEdgeInfo);
				}
			}
		}

		#region Entrances and clusters
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
            var entrances = new List<Entrance>();
            int top = cluster.Origin.Y;
            int left = cluster.Origin.X;
            
            if (clusterAbove != null)
            {
                var hEntrances = CreateHorizontalEntrances(
                    left,
                    left + cluster.Size.Width - 1,
                    top - 1,
                    clusterAbove,
                    cluster,
                    ref entranceId);

                entrances.AddRange(hEntrances);
            }

            if (clusterOnLeft != null)
            {
                var vEntrances = CreateVerticalEntrances(
                    top,
                    top + cluster.Size.Height - 1,
                    left - 1,
                    clusterOnLeft,
                    cluster,
                    ref entranceId);

                entrances.AddRange(vEntrances);
            }

            return entrances;
        }

        private List<Entrance> CreateHorizontalEntrances(
            int rowStart,
            int rowEnd,
            int column,
			Cluster clusterAbove,
			Cluster cluster,
            ref int currentEntranceId)
        {
            Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesForRow =
                row => Tuple.Create(GetNode(row, column), GetNode(row, column + 1));

            return CreateEntrancesAlongEdge(rowStart, rowEnd, clusterAbove, cluster, ref currentEntranceId, getNodesForRow, Orientation.Horizontal);
        }

        private List<Entrance> CreateVerticalEntrances(
            int colStart,
            int colEnd,
            int row,
            Cluster clusterOnLeft,
			Cluster cluster,
            ref int currentEntranceId)
        {
            Func<int, Tuple<Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node, Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node>> getNodesForColumn =
                column => Tuple.Create(GetNode(row, column), GetNode(row + 1, column));

            return CreateEntrancesAlongEdge(colStart, colEnd, clusterOnLeft, cluster, ref currentEntranceId, getNodesForColumn, Orientation.Vertical);
        }

        private List<Entrance> CreateEntrancesAlongEdge(
            int startPoint,
            int endPoint,
            Cluster precedentCluster,
			Cluster currentCluster,
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

                    var entrance1 = new Entrance(currentEntranceId, precedentCluster, currentCluster, srcNode, destNode, orientation);

                    currentEntranceId++;

                    nodes = getNodesInEdge(entranceEnd - 1);
                    srcNode = nodes.Item1;
                    destNode = nodes.Item2;

                    var entrance2 = new Entrance(currentEntranceId, precedentCluster, currentCluster, srcNode, destNode, orientation);

                    currentEntranceId++;

                    entrances.Add(entrance1);
                    entrances.Add(entrance2);
                }
                else
                {
                    var nodes = getNodesInEdge(((entranceEnd - 1) + entranceStart) / 2);
                    var srcNode = nodes.Item1;
                    var destNode = nodes.Item2;

                    var entrance = new Entrance(currentEntranceId, precedentCluster, currentCluster, srcNode, destNode, orientation);

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
		
		private Cluster GetCluster(List<Cluster> clusters, int left, int top)
		{
			var clustersW = HierarchicalMap.Width / ClusterSize;
			if (HierarchicalMap.Width % ClusterSize > 0)
				clustersW++;

			return clusters[top * clustersW + left];
		}
		#endregion
        
		#region Generate abstract nodes
		private void CreateAbstractNodes(List<Entrance> entrancesList)
		{
			foreach (var abstractNode in GenerateAbstractNodes(entrancesList))
			{
				HierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[abstractNode.ConcreteNodeId] = abstractNode.Id;
				HierarchicalMap.AbstractGraph.AddNode(abstractNode.Id, abstractNode);
			}
		}

		private IEnumerable<AbstractNodeInfo> GenerateAbstractNodes(List<Entrance> entrances)
		{
			var abstractNodeId = 0;
			var abstractNodesDict = new Dictionary<int, AbstractNodeInfo>();
			foreach (var entrance in entrances)
			{
				var level = entrance.GetEntranceLevel(ClusterSize, MaxLevel);

				CreateOrUpdateAbstractNodeFromConcreteNode(entrance.SrcNode, entrance.Cluster1, ref abstractNodeId, level, abstractNodesDict);
				CreateOrUpdateAbstractNodeFromConcreteNode(entrance.DestNode, entrance.Cluster2, ref abstractNodeId, level, abstractNodesDict);

			}

			return abstractNodesDict.Values;
		}

		private static void CreateOrUpdateAbstractNodeFromConcreteNode(
			Graph<ConcreteNodeInfo, ConcreteEdgeInfo>.Node srcNode,
			Cluster cluster,
			ref int abstractNodeId,
			int level,
			Dictionary<int, AbstractNodeInfo> abstractNodes)
		{
			AbstractNodeInfo abstractNodeInfo;
			if (!abstractNodes.TryGetValue(srcNode.NodeId, out abstractNodeInfo))
			{
				cluster.AddEntrance(
					abstractNodeId,
					new Position(srcNode.Info.Position.X - cluster.Origin.X, srcNode.Info.Position.Y - cluster.Origin.Y));

				abstractNodeInfo = new AbstractNodeInfo(
					abstractNodeId,
					level,
					cluster.Id,
					new Position(srcNode.Info.Position.X, srcNode.Info.Position.Y),
					srcNode.NodeId);
				abstractNodes[srcNode.NodeId] = abstractNodeInfo;

				abstractNodeId++;
			}
			else
			{
				if (level > abstractNodeInfo.Level)
					abstractNodeInfo.Level = level;
			}
		}
		#endregion

	}
}
