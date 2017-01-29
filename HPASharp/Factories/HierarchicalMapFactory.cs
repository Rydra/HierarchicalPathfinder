using System;
using System.Collections.Generic;
using HPASharp.Graph;
using HPASharp.Infrastructure;

namespace HPASharp.Factories
{
    public class HierarchicalMapFactory
    {
        private const int MAX_ENTRANCE_WIDTH = 6;

	    private HierarchicalMap _hierarchicalMap;
	    private ConcreteMap _concreteMap;
	    private EntranceStyle _entranceStyle;
	    private int _clusterSize;
	    private int _maxLevel;

	    readonly Dictionary<Id<AbstractNode>, NodeBackup> nodeBackups = new Dictionary<Id<AbstractNode>, NodeBackup>();

		public HierarchicalMap CreateHierarchicalMap(ConcreteMap concreteMap, int clusterSize, int maxLevel, EntranceStyle style)
        {
            _clusterSize = clusterSize;
            _entranceStyle = style;
            _maxLevel = maxLevel;
            _concreteMap = concreteMap;
            _hierarchicalMap = new HierarchicalMap(concreteMap, clusterSize, maxLevel);

            List<Entrance> entrances;
            List<Cluster> clusters; 
            CreateEntrancesAndClusters(out entrances, out clusters);
            _hierarchicalMap.Clusters = clusters;
			
            CreateAbstractNodes(entrances);
            CreateEdges(entrances, clusters);

	        return _hierarchicalMap;
        }

		#region Graph manipulation
		public void RemoveAbstractNode(HierarchicalMap map, Id<AbstractNode> nodeId)
		{
			if (nodeBackups.ContainsKey(nodeId))
			{
				RestoreNodeBackup(map, nodeId);
			}
			else
			{
				map.RemoveAbstractNode(nodeId);
			}
		}
		
	    public Id<AbstractNode> InsertAbstractNode(HierarchicalMap map, Position pos)
		{
			var nodeId = Id<ConcreteNode>.From(pos.Y * map.Width + pos.X);
			var abstractNodeId = InsertNodeIntoHierarchicalMap(map, nodeId, pos);
			map.AddHierarchicalEdgesForAbstractNode(abstractNodeId);
			return abstractNodeId;
		}

	    private class NodeBackup
	    {
			public int Level { get; private set; }
			public List<AbstractEdge> Edges { get; private set; }

		    public NodeBackup(int level, List<AbstractEdge> edges)
		    {
			    Level = level;
			    Edges = edges;
		    }
	    }

	    // insert a new node, such as start or target, to the abstract graph and
		// returns the id of the newly created node in the abstract graph
		// x and y are the positions where I want to put the node
		private Id<AbstractNode> InsertNodeIntoHierarchicalMap(HierarchicalMap map, Id<ConcreteNode> concreteNodeId, Position pos)
		{
			// If the node already existed (for instance, it was the an entrance point already
			// existing in the graph, we need to keep track of the previous status in order
			// to be able to restore it once we delete this STAL
			if (map.ConcreteNodeIdToAbstractNodeIdMap.ContainsKey(concreteNodeId))
			{
				var existingAbstractNodeId = map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId];
				var nodeBackup = new NodeBackup(
					map.AbstractGraph.GetNodeInfo(existingAbstractNodeId).Level, 
					map.GetNodeEdges(concreteNodeId));
				nodeBackups[existingAbstractNodeId] = nodeBackup;
				return map.ConcreteNodeIdToAbstractNodeIdMap[concreteNodeId];
			}
			
			var cluster = map.FindClusterForPosition(pos);

			// create global entrance
			var abstractNodeId = Id<AbstractNode>.From(map.NrNodes);
			
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

			return abstractNodeId;
		}
		#endregion
		
		private void RestoreNodeBackup(HierarchicalMap map, Id<AbstractNode> nodeId)
		{
			var abstractGraph = map.AbstractGraph;
			var nodeBackup = nodeBackups[nodeId];
			var nodeInfo = abstractGraph.GetNodeInfo(nodeId);
			nodeInfo.Level = nodeBackup.Level;
			abstractGraph.RemoveEdgesFromAndToNode(nodeId);
			abstractGraph.AddNode(nodeId, nodeInfo);
			foreach (var edge in nodeBackup.Edges)
			{
				var targetNodeId = edge.TargetNodeId;

				map.AddEdge(nodeId, targetNodeId, edge.Info.Cost,
					edge.Info.Level, edge.Info.IsInterClusterEdge,
					edge.Info.InnerLowerLevelPath != null ? new List<Id<AbstractNode>>(edge.Info.InnerLowerLevelPath) : null);

				edge.Info.InnerLowerLevelPath?.Reverse();

				map.AddEdge(targetNodeId, nodeId, edge.Info.Cost,
					edge.Info.Level, edge.Info.IsInterClusterEdge, edge.Info.InnerLowerLevelPath);
			}

			nodeBackups.Remove(nodeId);
		}

		private void CreateEdges(List<Entrance> entrances, List<Cluster> clusters)
		{
			foreach (var entrance in entrances)
			{
				CreateEntranceEdges(entrance, _hierarchicalMap.Type);
			}

			foreach (var cluster in clusters)
			{
				cluster.CreateIntraClusterEdges();
				CreateIntraClusterEdges(cluster);
			}

			_hierarchicalMap.CreateHierarchicalEdges();
		}

		private void CreateEntranceEdges(Entrance entrance, AbsType type)
		{
			var level = entrance.GetEntranceLevel(_clusterSize, _maxLevel);

			var srcAbstractNodeId = _hierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[entrance.SrcNode.NodeId];
			var destAbstractNodeId = _hierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[entrance.DestNode.NodeId];
			
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
			
			_hierarchicalMap.AbstractGraph.AddEdge(srcAbstractNodeId, destAbstractNodeId, new AbstractEdgeInfo(cost, level, true));
			_hierarchicalMap.AbstractGraph.AddEdge(destAbstractNodeId, srcAbstractNodeId, new AbstractEdgeInfo(cost, level, true));
		}
        
		private void CreateIntraClusterEdges(Cluster cluster)
		{
			foreach (var point1 in cluster.EntrancePoints)
			foreach (var point2 in cluster.EntrancePoints)
			{
				if (point1 != point2 && cluster.AreConnected(point1.AbstractNodeId, point2.AbstractNodeId))
				{
					var abstractEdgeInfo = new AbstractEdgeInfo(cluster.GetDistance(point1.AbstractNodeId, point2.AbstractNodeId), 1, false);
					_hierarchicalMap.AbstractGraph.AddEdge(
						point1.AbstractNodeId,
						point2.AbstractNodeId,
						abstractEdgeInfo);
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
            
			for (int top = 0, clusterY = 0; top < _concreteMap.Height; top += _clusterSize, clusterY++)
            for (int left = 0, clusterX = 0; left < _concreteMap.Width; left += _clusterSize, clusterX++)
            {
                var width = Math.Min(_clusterSize, _concreteMap.Width - left);
                var height = Math.Min(_clusterSize, _concreteMap.Height - top);
                var cluster = new Cluster(_concreteMap, Id<Cluster>.From(clusterId), clusterX, clusterY, new Position(left, top), new Size(width, height));
				clusters.Add(cluster);

                clusterId++;

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
                var entrancesOnTop = CreateEntrancesOnTop(
                    left,
                    left + cluster.Size.Width - 1,
                    top - 1,
                    clusterAbove,
                    cluster,
                    ref entranceId);

                entrances.AddRange(entrancesOnTop);
            }

            if (clusterOnLeft != null)
            {
                var entrancesOnLeft = CreateEntrancesOnLeft(
                    top,
                    top + cluster.Size.Height - 1,
                    left - 1,
                    clusterOnLeft,
                    cluster,
                    ref entranceId);

                entrances.AddRange(entrancesOnLeft);
            }

            return entrances;
        }

        private List<Entrance> CreateEntrancesOnLeft(
            int rowStart,
            int rowEnd,
            int column,
			Cluster clusterOnLeft,
			Cluster cluster,
            ref int currentEntranceId)
        {
            Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesForRow =
                row => Tuple.Create(GetNode(column, row), GetNode(column + 1, row));

            return CreateEntrancesAlongEdge(rowStart, rowEnd, clusterOnLeft, cluster, ref currentEntranceId, getNodesForRow, Orientation.Horizontal);
        }

        private List<Entrance> CreateEntrancesOnTop(
            int colStart,
            int colEnd,
            int row,
            Cluster clusterOnTop,
			Cluster cluster,
            ref int currentEntranceId)
        {
            Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesForColumn =
                column => Tuple.Create(GetNode(column, row), GetNode(column, row + 1));

            return CreateEntrancesAlongEdge(colStart, colEnd, clusterOnTop, cluster, ref currentEntranceId, getNodesForColumn, Orientation.Vertical);
        }

        private List<Entrance> CreateEntrancesAlongEdge(
            int startPoint,
            int endPoint,
            Cluster precedentCluster,
			Cluster currentCluster,
            ref int currentEntranceId,
            Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesInEdge,
            Orientation orientation)
        {
            List<Entrance> entrances = new List<Entrance>();

            for (var entranceStart = startPoint; entranceStart <= endPoint; entranceStart++)
            {
                var size = GetEntranceSize(entranceStart, endPoint, getNodesInEdge);

                var entranceEnd = entranceStart + size - 1;
                if (size == 0)
                    continue;

                if (_entranceStyle == EntranceStyle.EndEntrance && size > MAX_ENTRANCE_WIDTH)
                {
                    var nodes = getNodesInEdge(entranceStart);
                    var srcNode = nodes.Item1;
                    var destNode = nodes.Item2;

                    var entrance1 = new Entrance(Id<Entrance>.From(currentEntranceId), precedentCluster, currentCluster, srcNode, destNode, orientation);

                    currentEntranceId++;
					
                    nodes = getNodesInEdge(entranceEnd);
                    srcNode = nodes.Item1;
                    destNode = nodes.Item2;

                    var entrance2 = new Entrance(Id<Entrance>.From(currentEntranceId), precedentCluster, currentCluster, srcNode, destNode, orientation);

                    currentEntranceId++;

                    entrances.Add(entrance1);
                    entrances.Add(entrance2);
                }
                else
                {
                    var nodes = getNodesInEdge((entranceEnd + entranceStart) / 2);
                    var srcNode = nodes.Item1;
                    var destNode = nodes.Item2;

                    var entrance = new Entrance(Id<Entrance>.From(currentEntranceId), precedentCluster, currentCluster, srcNode, destNode, orientation);

                    currentEntranceId++;
                    entrances.Add(entrance);
                }

                entranceStart = entranceEnd;
            }

            return entrances;
        }

        private int GetEntranceSize(int entranceStart, int end, Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesInEdge)
        {
            var size = 0;
            while (entranceStart + size <= end && !EntranceIsBlocked(entranceStart + size, getNodesInEdge))
            {
                size++;
            }

            return size;
        }

        private ConcreteNode GetNode(int left, int top)
        {
            return _concreteMap.Graph.GetNode(_concreteMap.GetNodeIdFromPos(left, top));
        }

        private bool EntranceIsBlocked(int entrancePoint, Func<int, Tuple<ConcreteNode, ConcreteNode>> getNodesInEdge)
        {
			var nodes = getNodesInEdge(entrancePoint);
			return nodes.Item1.Info.IsObstacle || nodes.Item2.Info.IsObstacle;
        }
		
		private Cluster GetCluster(List<Cluster> clusters, int left, int top)
		{
			var clustersW = _hierarchicalMap.Width / _clusterSize;
			if (_hierarchicalMap.Width % _clusterSize > 0)
				clustersW++;

			return clusters[top * clustersW + left];
		}
		#endregion
        
		#region Generate abstract nodes
		private void CreateAbstractNodes(List<Entrance> entrancesList)
		{
			foreach (var abstractNode in GenerateAbstractNodes(entrancesList))
			{
				_hierarchicalMap.ConcreteNodeIdToAbstractNodeIdMap[abstractNode.ConcreteNodeId] = abstractNode.Id;
				_hierarchicalMap.AbstractGraph.AddNode(abstractNode.Id, abstractNode);
			}
		}

		private IEnumerable<AbstractNodeInfo> GenerateAbstractNodes(List<Entrance> entrances)
		{
			var abstractNodeId = 0;
			var abstractNodesDict = new Dictionary<Id<ConcreteNode>, AbstractNodeInfo>();
			foreach (var entrance in entrances)
			{
				var level = entrance.GetEntranceLevel(_clusterSize, _maxLevel);

				CreateOrUpdateAbstractNodeFromConcreteNode(entrance.SrcNode, entrance.Cluster1, ref abstractNodeId, level, abstractNodesDict);
				CreateOrUpdateAbstractNodeFromConcreteNode(entrance.DestNode, entrance.Cluster2, ref abstractNodeId, level, abstractNodesDict);
			}

			return abstractNodesDict.Values;
		}

		private static void CreateOrUpdateAbstractNodeFromConcreteNode(
			ConcreteNode srcNode,
			Cluster cluster,
			ref int abstractNodeId,
			int level,
			Dictionary<Id<ConcreteNode>, AbstractNodeInfo> abstractNodes)
		{
			AbstractNodeInfo abstractNodeInfo;
			if (!abstractNodes.TryGetValue(srcNode.NodeId, out abstractNodeInfo))
			{
				cluster.AddEntrance(
					Id<AbstractNode>.From(abstractNodeId),
					new Position(srcNode.Info.Position.X - cluster.Origin.X, srcNode.Info.Position.Y - cluster.Origin.Y));

				abstractNodeInfo = new AbstractNodeInfo(
					Id<AbstractNode>.From(abstractNodeId),
					level,
					cluster.Id,
					new Position(srcNode.Info.Position.X, srcNode.Info.Position.Y),
					srcNode.NodeId);
				abstractNodes[srcNode.NodeId] = abstractNodeInfo;

				abstractNodeId++;
			}
			else if (level > abstractNodeInfo.Level)
			{
				abstractNodeInfo.Level = level;
			}
		}
		#endregion

	}
}
