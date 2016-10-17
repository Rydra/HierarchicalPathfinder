using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public enum EntranceStyle
    {
        MIDDLE_ENTRANCE, END_ENTRANCE
    }
    
    public class AbstractMapFactory
    {
        const int MAX_ENTRANCE_WIDTH = 6;

        public AbsTiling AbsTiling { get; set; }
        public Tiling Tiling { get; set; }
        public EntranceStyle EntranceStyle { get; set; }
        public int AbstractionRate { get; set; }
        public int ClusterSize { get; set; }
		public int MaxLevel { get; set; }

        public AbstractMapFactory(Tiling tiling, int clusterSize, int maxLevel, EntranceStyle style)
        {
            this.ClusterSize = clusterSize;
            this.EntranceStyle = style;
	        MaxLevel = maxLevel;
            Tiling = tiling;
            AbsTiling = new HierarchicalTiling(clusterSize, maxLevel, tiling.Height, tiling.Width);
        }

        public void CreateAbstractMap()
        {
            CreateEntrancesAndClusters();
            // TODO: Here the edges are created. Honestly, I'd prefer to have them built on a per-cluster basis.
            CreateEdges();
        }

        private void CreateEntrancesAndClusters()
        {
            // now build clusters
            var clusterId = 0;
            var entranceId = 0;
            
            AbsTiling.SetType(Tiling.TileType);
			List<Entrance> entrancesList = new List<Entrance>();
			List<Cluster> clusters = new List<Cluster>();
			AbsTiling.Clusters = clusters;

			// NOTE: Here we create bottom-level cluster instances. Maybe I could deal here with the levels
			// And create multi-level clusters
			for (int top = 0, clusterY = 0; top < Tiling.Height; top += ClusterSize, clusterY++)
            for (int left = 0, clusterX = 0; left < Tiling.Width; left += ClusterSize, clusterX++)
            {
                var horizSize = Math.Min(ClusterSize, Tiling.Width - left);
                var vertSize = Math.Min(ClusterSize, Tiling.Height - top);
                var cluster = new Cluster(Tiling, clusterId++, clusterX, clusterY, new Position(left, top), new Size(horizSize, vertSize));
				clusters.Add(cluster);

                // add inter-cluster entrances. Obviously we should not add entrances on leftmost clusters when adding vertical entrances
				// nor on topmost clusters when adding horizontal entrances.
				
	            if (top > 0)
	            {
		            int lastEntranceId;
		            var entrances = CreateHorizEntrances(left, left + horizSize - 1, top - 1,
			            AbsTiling.GetCluster(clusterX, clusterY - 1).Id, cluster.Id, entranceId, out lastEntranceId);
		            entranceId = lastEntranceId;
					entrancesList.AddRange(entrances);
				}

	            if (left > 0)
	            {
					int lastEntranceId;
					var entrances = CreateVertEntrances(top, top + vertSize - 1, left - 1,
			        AbsTiling.GetCluster(clusterX - 1, clusterY).Id, cluster.Id, entranceId, out lastEntranceId);
					entranceId = lastEntranceId;
					entrancesList.AddRange(entrances);
				}
            }

			// The entrances we created define points that we must transform as abstract nodes of the abstract graph
			var abstractNodes = AbsTiling.GenerateAbstractNodes(entrancesList);
            
            // add nodes to the abstract graph
            foreach (var absNode in abstractNodes)
            {
                AbsTiling.Graph.AddNode(absNode.Id, absNode);
            }

			// Create the interEdges. NOTE: SRP Violation here!
			foreach (var entrance in entrancesList)
			{
				AbsTiling.CreateInterClusterEdges(entrance);
			}

			AbsTiling.ComputeClusterPaths();
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
						AbsTiling.Graph.AddEdge(
							cluster.GetGlobalAbsNodeId(k), 
							cluster.GetGlobalAbsNodeId(l),
							absTilingEdgeInfo1);

						var absTilingEdgeInfo2 = new AbsTilingEdgeInfo(cluster.GetDistance(k, l), 1, false);
						AbsTiling.Graph.AddEdge(
							cluster.GetGlobalAbsNodeId(l), 
							cluster.GetGlobalAbsNodeId(k),
							absTilingEdgeInfo2);
                    }
                }
        }

        private void CreateEdges()
        {
	        foreach (var cluster in AbsTiling.Clusters)
	        {
		        CreateIntraClusterEdges(cluster);
	        }
			
            AbsTiling.CreateEdges();
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

	        var tilingGraph = Tiling.Graph;
			Func<int, int, Graph<TilingNodeInfo, TilingEdgeInfo>.Node> getNode =
				(top, left) => tilingGraph.GetNode(Tiling.GetNodeIdFromPos(top, left));

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
									   getNode(entranceStart, y + 1).NodeId, Orientation.HORIZONTAL);

                    var entrance2 = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, (i - 1),
									   getNode(i - 1, y).NodeId,
									   getNode(i - 1, y + 1).NodeId, Orientation.HORIZONTAL);

					entrances.Add(entrance1);
					entrances.Add(entrance2);
				}
                else
                {
                    // if it is small, create one entrance in the middle 
                    var entrance = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, ((i - 1) + entranceStart) / 2,
									  getNode(((i - 1) + entranceStart) / 2, y).NodeId,
									  getNode(((i - 1) + entranceStart) / 2, y + 1).NodeId, Orientation.HORIZONTAL);

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
			var tilingGraph = Tiling.Graph;
	        Func<int, int, Graph<TilingNodeInfo, TilingEdgeInfo>.Node> getNode =
		        (top, left) => tilingGraph.GetNode(Tiling.GetNodeIdFromPos(top, left));

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
