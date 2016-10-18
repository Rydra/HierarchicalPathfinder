using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Factories;

namespace HPASharp
{
    public enum EntranceStyle
    {
        MIDDLE_ENTRANCE, END_ENTRANCE
    }
    
    public class AbstractMapFactory
    {
        const int MAX_ENTRANCE_WIDTH = 6;

        public AbstractMap AbstractMap { get; set; }
        public ConcreteMap ConcreteMap { get; set; }
        public EntranceStyle EntranceStyle { get; set; }
        public int AbstractionRate { get; set; }
        public int ClusterSize { get; set; }
		public int MaxLevel { get; set; }

        // Store the location of created entrances. This will help creating nodes and edges        
        public Dictionary<int, AbsTilingNodeInfo> AbstractNodes { get; set; }
        
        public void CreateAbstractMap(ConcreteMap concreteMap, int clusterSize, int maxLevel, EntranceStyle style)
        {
            this.ClusterSize = clusterSize;
            this.EntranceStyle = style;
            MaxLevel = maxLevel;
            ConcreteMap = concreteMap;
            AbstractMap = new HierarchicalMap(clusterSize, maxLevel, concreteMap.Height, concreteMap.Width);

            List<Entrance> entrances;
            List<Cluster> clusters; 
            CreateEntrancesAndClusters(out entrances, out clusters);
            CreateAbstractNodes(entrances, clusters);
            CreateEdges(entrances, clusters);
        }

        private void CreateEntrancesAndClusters(out List<Entrance> entrances, out List<Cluster> clusters)
        {
            var clusterId = 0;
            var entranceId = 0;
            
            AbstractMap.SetType(ConcreteMap.TileType);
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

            AbstractMap.Clusters = clusters;
        }
        
        /// <summary>
        /// Gets the cluster Id, determined by its row and column
        /// </summary>
        public Cluster GetCluster(List<Cluster> clusters, int left, int top)
        {
            var clustersW = AbstractMap.Width / ClusterSize;
            if (AbstractMap.Width % ClusterSize > 0)
                clustersW++;

            return clusters[top * clustersW + left];
        }

        private void CreateAbstractNodes(List<Entrance> entrancesList, List<Cluster> clusters)
        {
            var abstractNodes = GenerateAbstractNodes(entrancesList, clusters);

            foreach (var kvp in abstractNodes)
            {
                // TODO: Maybe we can find a way to remove this line of AbsNodesIds
                AbstractMap.AbsNodeIds[kvp.Key] = kvp.Value.Id;
                AbstractMap.Graph.AddNode(kvp.Value.Id, kvp.Value);
            }

            AbstractNodes = abstractNodes;
        }

        public void CreateEntranceEdges(Entrance entrance, AbsType type, Dictionary<int, AbsTilingNodeInfo> absNodes)
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
                    AbstractMap.Graph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo1);
                    AbstractMap.Graph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo2);
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
                        AbstractMap.Graph.AddEdge(abstractNodeId1, abstractNodeId2, absTilingEdgeInfo3);
                        AbstractMap.Graph.AddEdge(abstractNodeId2, abstractNodeId1, absTilingEdgeInfo4);
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
                    var localEntranceIdx = cluster1.AddEntrance(new EntrancePoint(
                                               abstractNodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Coord1.X - cluster1.Origin.X, entrance.Coord1.Y - cluster1.Origin.Y)));
                    
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
                    var localEntranceIdx = cluster2.AddEntrance(new EntrancePoint(
                                               abstractNodeId,
                                               -1, // real value set in addEntrance()
                                               new Position(entrance.Coord2.X - cluster2.Origin.X, entrance.Coord2.Y - cluster2.Origin.Y)));

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

        private void CreateIntraClusterEdges(Cluster cluster)
        {
            // Iterate over every pair of cluster entrances and if a path exists, create an edge between them
            // in the graph
            for (var k = 0; k < cluster.GetNrEntrances(); k++)
                for (var l = k + 1; l < cluster.GetNrEntrances(); l++)
                {
                    if (cluster.AreConnected(l, k))
                    {
	                    var absTilingEdgeInfo1 = new AbsTilingEdgeInfo(cluster.GetDistance(l, k), 1, false);
						AbstractMap.Graph.AddEdge(
							cluster.GetGlobalAbsNodeId(k), 
							cluster.GetGlobalAbsNodeId(l),
							absTilingEdgeInfo1);

						var absTilingEdgeInfo2 = new AbsTilingEdgeInfo(cluster.GetDistance(k, l), 1, false);
						AbstractMap.Graph.AddEdge(
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
                CreateEntranceEdges(entrance, AbstractMap.Type, AbstractNodes);
            }

            foreach (var cluster in clusters)
	        {
                // Computes the paths that lie inside every cluster, 
                // connecting the several entrances among them
                cluster.ComputeInternalPaths();
                CreateIntraClusterEdges(cluster);
	        }
			
            // TODO: Review this one!
            AbstractMap.CreateHierarchicalEdges();
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
    }
}
