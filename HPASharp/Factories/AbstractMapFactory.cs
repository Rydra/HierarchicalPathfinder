using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Factories;
using HPASharp.Search;

namespace HPASharp
{
    public enum EntranceStyle
    {
        MIDDLE_ENTRANCE, END_ENTRANCE
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
        public Dictionary<int, AbsTilingNodeInfo> AbstractNodes { get; set; }

		int[] m_stalLevel = new int[2];
		bool[] m_stalUsed = new bool[2];
		List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge>[] m_stalEdges = new List<Graph<AbsTilingNodeInfo, AbsTilingEdgeInfo>.Edge>[2];

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

            // TODO: It would be preferrable to build the nodes and the edges and then build up the graph
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
				// The offset determines the distances that will separate clusters in this new level
				int offset = GetOffset(level);
				HierarchicalMap.SetCurrentLevel(level - 1);

				// for each cluster
				// TODO: Maybe we could refactor this so that instead of having to deal with levels,
				// offsets and all this mess... we could create multiple clusters and each cluster have a level.
				// PD: How amazing it is to pick an old project after leaving it in the shelf for some time,
				// you think extremely different in terms of design and see things from another perspective
				for (var top = 0; top < HierarchicalMap.Height; top += offset)
					for (var left = 0; left < HierarchicalMap.Width; left += offset)
					{
						// define the bounding box of the current cluster we want to analize to create HEdges
						HierarchicalMap.SetCurrentCluster(left, top, offset);
						this.ConstructVerticalToVerticalEdges(level);
						this.ConstructHorizontalToHorizontalEdges(level);
						this.ConstructHorizontalToVerticalEdges(level);
					}
			}
		}

		private void CreateEntrancesAndClusters(out List<Entrance> entrances, out List<Cluster> clusters)
        {
            var clusterId = 0;
            var entranceId = 0;
            
            entrances = new List<Entrance>();
			clusters = new List<Cluster>();

			// NOTE: Here we create bottom-level cluster instances. Maybe I could deal here with the levels
			// And create multi-level clusters
			for (int top = 0, clusterY = 0; top < ConcreteMap.Height; top += ClusterSize, clusterY++)
            for (int left = 0, clusterX = 0; left < ConcreteMap.Width; left += ClusterSize, clusterX++)
            {
                var horizSize = Math.Min(ClusterSize, ConcreteMap.Width - left);
                var vertSize = Math.Min(ClusterSize, ConcreteMap.Height - top);
                var cluster = new Cluster(ConcreteMap, clusterId++, clusterX, clusterY, new Position(left, top), new Size(horizSize, vertSize));
				clusters.Add(cluster);

                // add inter-cluster entrances. Obviously we should not add entrances on leftmost clusters when adding vertical entrances
				// nor on topmost clusters when adding horizontal entrances.
	            if (top > 0)
	            {
                    // We know getting the cluster above works because since we are generating
                    // them from top to bottom, left to right, we know there must be some
                    // cluster above. If we could not guarantee this at this point, we could
                    // have null/out of bounds exceptions
	                var clusterAbove = GetCluster(clusters, clusterX, clusterY - 1);
                    int lastEntranceId;
		            var hEntrances = CreateHorizEntrances(
                        left, 
                        left + horizSize - 1, 
                        top - 1,
                        clusterAbove.Id, 
                        cluster.Id, 
                        entranceId, 
                        out lastEntranceId);
		            entranceId = lastEntranceId;
					entrances.AddRange(hEntrances);
				}

	            if (left > 0)
	            {
					int lastEntranceId;
	                var clusterOnLeft = GetCluster(clusters, clusterX - 1, clusterY);
                    var vEntrances = CreateVertEntrances(
                        top, 
                        top + vertSize - 1, 
                        left - 1,
                        clusterOnLeft.Id, 
                        cluster.Id, 
                        entranceId, 
                        out lastEntranceId);
					entranceId = lastEntranceId;
					entrances.AddRange(vEntrances);
				}
            }
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

        private void CreateEntranceEdges(Entrance entrance, AbsType type, Dictionary<int, AbsTilingNodeInfo> absNodes)
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

            var abstractNodeId1 = absNodes[entrance.Coord1Id].Id;
            var abstractNodeId2 = absNodes[entrance.Coord2Id].Id;

            switch (type)
            {
                case AbsType.ABSTRACT_TILE:
                case AbsType.ABSTRACT_OCTILE_UNICOST:
                    // Inter-edges: cost 1
                    var absTilingEdgeInfo1 = new AbsTilingEdgeInfo(Constants.COST_ONE, level, true);
                    var absTilingEdgeInfo2 = new AbsTilingEdgeInfo(Constants.COST_ONE, level, true);
                    HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo1);
                    HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo2);
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
                                unitCost = (Constants.COST_ONE * 34) / 24;
                                break;
                            default:
                                unitCost = -1;
                                break;
                        }

                        var absTilingEdgeInfo3 = new AbsTilingEdgeInfo(unitCost, level, true);
                        var absTilingEdgeInfo4 = new AbsTilingEdgeInfo(unitCost, level, true);
                        HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo3);
                        HierarchicalMap.AbstractGraph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo4);
                    }
                    break;
                default:
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
        private Dictionary<int, AbsTilingNodeInfo> GenerateAbstractNodes(List<Entrance> entrances, List<Cluster> clusters)
        {
            var abstractNodeId = 0;
            var absNodes = new Dictionary<int, AbsTilingNodeInfo>();
            foreach (var entrance in entrances)
            {
                var cluster1 = clusters[entrance.Cluster1Id];
                var cluster2 = clusters[entrance.Cluster2Id];

                // Determine the level of this entrance. It is given
                // by its orientation and its coordinates
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
                    var localEntranceIdx = cluster1.AddEntrance(
                        abstractNodeId, 
                        new Position(entrance.Coord1.X - cluster1.Origin.X, entrance.Coord1.Y - cluster1.Origin.Y));
                    
                    var node = new AbsTilingNodeInfo(
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

                    var node = new AbsTilingNodeInfo(
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
            // Iterate over every pair of cluster entrances and if a path exists, create an edge between them
            // in the graph
            for (var k = 0; k < cluster.GetNrEntrances(); k++)
                for (var l = k + 1; l < cluster.GetNrEntrances(); l++)
                {
                    if (cluster.AreConnected(l, k))
                    {
	                    var absTilingEdgeInfo1 = new AbsTilingEdgeInfo(cluster.GetDistance(l, k), 1, false);
						HierarchicalMap.AbstractGraph.AddEdge(
							cluster.GetGlobalAbsNodeId(k), 
							cluster.GetGlobalAbsNodeId(l),
							absTilingEdgeInfo1);

						var absTilingEdgeInfo2 = new AbsTilingEdgeInfo(cluster.GetDistance(k, l), 1, false);
						HierarchicalMap.AbstractGraph.AddEdge(
							cluster.GetGlobalAbsNodeId(l), 
							cluster.GetGlobalAbsNodeId(k),
							absTilingEdgeInfo2);
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
                // Computes the paths that lie inside every cluster, 
                // connecting the several entrances among them
                cluster.ComputeInternalPaths();
                CreateInterClusterEdges(cluster);
	        }
			
            // TODO: Review this one!
            CreateHierarchicalEdges();
        }

        // TODO: Together with Vert Entrances, refactor the code, they are too similar!
        /// <summary>
        /// Creates the horizontal entrances between the two clusters, and returns the last entrance id
        /// </summary>
        private List<Entrance> CreateHorizEntrances(
            int x0,
            int x1,
            int y,
            int clusterid1,
            int clusterid2,
            int currId,
			out int nextId)
        {
            var currentIdCounter = currId;
            var orientation = Orientation.HORIZONTAL;
            
            var tilingGraph = ConcreteMap.Graph;
			Func<int, int, Graph<TilingNodeInfo, TilingEdgeInfo>.Node> getNode =
				(top, left) => tilingGraph.GetNode(ConcreteMap.GetNodeIdFromPos(top, left));

			List<Entrance> entrances = new List<Entrance>();

			// rolls over the horizontal edge between x0 and x1 in order to find edges between
			// the top cluster (latitude marks the other cluster entrance line)
			for (var i = x0; i <= x1; i++)
            {
                var node1isObstacle = getNode(i, y).Info.IsObstacle;
                var node2isObstacle = getNode(i, y + 1).Info.IsObstacle;
                // get the next communication spot
                if (node1isObstacle || node2isObstacle)
                    continue;

                // start building and tracking the entrance
                var entranceStart = i;
                while (true)
                {
                    i++;
                    if (i >= x1)
                        break;
                    node1isObstacle = getNode(i, y).Info.IsObstacle;
                    node2isObstacle = getNode(i, y + 1).Info.IsObstacle;
                    if (node1isObstacle || node2isObstacle || i >= x1)
                        break;
                }

                if (EntranceStyle == EntranceStyle.END_ENTRANCE && i - entranceStart > MAX_ENTRANCE_WIDTH)
                {
                    // If the tracked entrance is big, create 2 entrance points at the edges of the entrance.
                    // create two new entrances, one for each end
                    var entrance1 = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, entranceStart,
									   getNode(entranceStart, y).NodeId,
									   getNode(entranceStart, y + 1).NodeId, orientation);

                    var entrance2 = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, (i - 1),
									   getNode(i - 1, y).NodeId,
									   getNode(i - 1, y + 1).NodeId, orientation);

					entrances.Add(entrance1);
					entrances.Add(entrance2);
				}
                else
                {
                    // if it is small, create one entrance in the middle 
                    var entrance = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, ((i - 1) + entranceStart) / 2,
									  getNode(((i - 1) + entranceStart) / 2, y).NodeId,
									  getNode(((i - 1) + entranceStart) / 2, y + 1).NodeId, orientation);

					entrances.Add(entrance);
                }
            }

	        nextId = currentIdCounter;
			
			return entrances;
        }

        private List<Entrance> CreateVertEntrances(int y0, int y1, int x, int clusterid1,
            int clusterid2, int currId, out int lastEntraceId)
        {
            var currentIdCounter = currId;
			var tilingGraph = ConcreteMap.Graph;
	        Func<int, int, Graph<TilingNodeInfo, TilingEdgeInfo>.Node> getNode =
		        (top, left) => tilingGraph.GetNode(ConcreteMap.GetNodeIdFromPos(top, left));

			List<Entrance> entrances = new List<Entrance>();

			for (var i = y0; i <= y1; i++)
            {
                var node1isObstacle = getNode(x, i).Info.IsObstacle;
                var node2isObstacle = getNode(x + 1, i).Info.IsObstacle;
                // get the next communication spot
                if (node1isObstacle || node2isObstacle)
                    continue;

                // start building the entrance
                var entranceStart = i;
                while (true)
                {
                    i++;
                    if (i >= y1)
                        break;
                    node1isObstacle = getNode(x, i).Info.IsObstacle;
                    node2isObstacle = getNode(x + 1, i).Info.IsObstacle;
                    if (node1isObstacle || node2isObstacle || i >= y1)
                        break;
                }

                if (EntranceStyle == EntranceStyle.END_ENTRANCE && (i - entranceStart) > MAX_ENTRANCE_WIDTH)
                {
                    // create two entrances, one for each end
                    var entrance1 = new Entrance(currentIdCounter++, clusterid1, clusterid2, entranceStart, x,
									   getNode(x, entranceStart).NodeId,
									   getNode(x + 1, entranceStart).NodeId, Orientation.VERTICAL);

                    // BEWARE! We are getting the tileNode for position i - 1. If clustersize was 8
                    // for example, and end would had finished at 7, you would set the entrance at 6.
                    // This seems to be intended.
                    var entrance2 = new Entrance(currentIdCounter++, clusterid1, clusterid2, (i - 1), x,
									   getNode(x, i - 1).NodeId,
                                       getNode(x + 1, i - 1).NodeId, Orientation.VERTICAL);

					entrances.Add(entrance1);
					entrances.Add(entrance2);
				}
                else
                {
                    // create one entrance
                    var entrance = new Entrance(currentIdCounter++, clusterid1, clusterid2, ((i - 1) + entranceStart) / 2, x,
									  getNode(x, (i - 1 + entranceStart) / 2).NodeId,
                                      getNode(x + 1, (i - 1 + entranceStart) / 2).NodeId, Orientation.VERTICAL);
					entrances.Add(entrance);
				}
            }

            lastEntraceId = currentIdCounter;
	        return entrances;
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
			var localEntranceIdx = cluster.AddEntrance(absNodeId, new Position(pos.X - cluster.Origin.X, pos.Y - cluster.Origin.Y));
			cluster.UpdatePaths(localEntranceIdx);

			map.AbsNodeIds[nodeId] = absNodeId;

			var info = new AbsTilingNodeInfo(
				absNodeId,
				1,
				cluster.Id,
				pos,
				nodeId,
				localEntranceIdx);

			map.AbstractGraph.AddNode(absNodeId, info);

			// add new edges to the abstract graph
			for (var k = 0; k < cluster.GetNrEntrances() - 1; k++)
			{
				if (cluster.AreConnected(localEntranceIdx, k))
				{
					map.AddEdge(
						cluster.GetGlobalAbsNodeId(k),
						cluster.GetGlobalAbsNodeId(localEntranceIdx),
						cluster.GetDistance(localEntranceIdx, k));
					map.AddEdge(
						cluster.GetGlobalAbsNodeId(localEntranceIdx),
						cluster.GetGlobalAbsNodeId(k),
						cluster.GetDistance(k, localEntranceIdx));
				}
			}

			return absNodeId;
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

		private void ConstructHorizontalToVerticalEdges(int level)
		{
			// combine nodes on horizontal and vertical edges:
			// This runs over each cell of the 2 horizontal edges against the vertical edges
			var clusterRectangle = HierarchicalMap.GetCurrentClusterRectangle();
			var height = clusterRectangle.Size.Height;
			var width = clusterRectangle.Size.Width;
			var currentClusterY0 = clusterRectangle.Origin.Y;
			var currentClusterY1 = clusterRectangle.Origin.Y + clusterRectangle.Size.Height;
			var currentClusterX0 = clusterRectangle.Origin.X;
			var currentClusterX1 = clusterRectangle.Origin.X + clusterRectangle.Size.Width;

			for (var y1 = currentClusterY0; y1 <= currentClusterY1; y1 += height)
				for (var x1 = currentClusterX0 + 1; x1 < currentClusterX1; x1++)
				{
					var nodeId1 = y1 * HierarchicalMap.Width + x1;
					var absNodeId1 = HierarchicalMap.AbsNodeIds[nodeId1];
					if (!IsValidAbstractNode(HierarchicalMap, absNodeId1, level))
						continue;

					for (var y2 = currentClusterY0 + 1; y2 < currentClusterY1; y2++)
						for (var x2 = currentClusterX0; x2 <= currentClusterX1; x2 += width)
						{
							var nodeId2 = y2 * HierarchicalMap.Width + x2;
							var absNodeId2 = HierarchicalMap.AbsNodeIds[nodeId2];
							AddEdgesBetweenAbstractNodes(HierarchicalMap, absNodeId1, absNodeId2, level);
						}
				}
		}

		private void ConstructHorizontalToHorizontalEdges(int level)
		{
			// combine nodes on horizontal edges:
			// This runs over each cell of the 2 horizontal edges against itself (therefore trying to establish
			// edges on only horizontal edges)
			
			var clusterRectangle = HierarchicalMap.GetCurrentClusterRectangle();
			var height = clusterRectangle.Size.Height;
			var currentClusterY0 = clusterRectangle.Origin.Y;
			var currentClusterY1 = clusterRectangle.Origin.Y + clusterRectangle.Size.Height;
			var currentClusterX0 = clusterRectangle.Origin.X;
			var currentClusterX1 = clusterRectangle.Origin.X + clusterRectangle.Size.Width;
			
			for (var y1 = currentClusterY0; y1 <= currentClusterY1; y1 += height)
				for (var x1 = currentClusterX0; x1 <= currentClusterX1; x1++)
				{
					var nodeId1 = y1 * HierarchicalMap.Width + x1;
					var absNodeId1 = HierarchicalMap.AbsNodeIds[nodeId1];
					if (!IsValidAbstractNode(HierarchicalMap, absNodeId1, level))
						continue;

					for (var y2 = currentClusterY0; y2 <= currentClusterY1; y2 += height)
						for (var x2 = currentClusterX0; x2 <= currentClusterX1; x2++)
						{
							var nodeId2 = y2 * HierarchicalMap.Width + x2;
							if (nodeId1 >= nodeId2)
								continue;

							var absNodeId2 = HierarchicalMap.AbsNodeIds[nodeId2];
							AddEdgesBetweenAbstractNodes(HierarchicalMap, absNodeId1, absNodeId2, level);
						}
				}
		}

		private void ConstructVerticalToVerticalEdges(int level)
		{
			// combine nodes on vertical edges:
			// This runs over each cell of the 2 vertical edges

			var clusterRectangle = HierarchicalMap.GetCurrentClusterRectangle();
			var width = clusterRectangle.Size.Width;
			var currentClusterY0 = clusterRectangle.Origin.Y;
			var currentClusterY1 = clusterRectangle.Origin.Y + clusterRectangle.Size.Height;
			var currentClusterX0 = clusterRectangle.Origin.X;
			var currentClusterX1 = clusterRectangle.Origin.X + clusterRectangle.Size.Width;

			for (var y1 = currentClusterY0; y1 <= currentClusterY1; y1++)
				for (var x1 = currentClusterX0; x1 <= currentClusterX1; x1 += width)
				{
					var nodeId1 = y1 * HierarchicalMap.Width + x1;
					var absNodeId1 = HierarchicalMap.AbsNodeIds[nodeId1];
					if (!IsValidAbstractNode(HierarchicalMap, absNodeId1, level))
						continue;

					for (var y2 = currentClusterY0; y2 <= currentClusterY1; y2++)
						for (var x2 = currentClusterX0; x2 <= currentClusterX1; x2 += width)
						{
							// Only analize the points that lie forward to the current point we are analizing (in front of y1,x1)
							var nodeId2 = y2 * HierarchicalMap.Width + x2;
							if (nodeId1 >= nodeId2)
								continue;

							var absNodeId2 = HierarchicalMap.AbsNodeIds[nodeId2];
							AddEdgesBetweenAbstractNodes(HierarchicalMap, absNodeId1, absNodeId2, level);
						}
				}
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

		private int GetOffset(int level)
		{
			return ClusterSize * (1 << (level - 1));
		}
	}
}
