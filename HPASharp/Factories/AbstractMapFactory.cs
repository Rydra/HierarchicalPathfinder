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

        public AbstractMapFactory(Tiling tiling, int clusterSize, int maxLevel, EntranceStyle style)
        {
            this.ClusterSize = clusterSize;
            this.EntranceStyle = style;
            Tiling = tiling;
            AbsTiling = new HTiling(clusterSize, maxLevel, tiling.Height, tiling.Width);
        }

        public void CreateAbstractMap()
        {
            CreateEntrancesAndClusters();
            CreateEdges();
        }

        private void CreateEntrancesAndClusters()
        {
            // now build clusters
            var clusterId = 0;
            var entranceId = 0;
            
            AbsTiling.SetType(Tiling.TileType);
            for (int j = 0, clusterY = 0; j < Tiling.Height; j+= ClusterSize, clusterY++)
            for (int i = 0, clusterX = 0; i < Tiling.Width; i+= ClusterSize, clusterX++)
            {
                var horizSize = Math.Min(ClusterSize, Tiling.Width - i);
                var vertSize = Math.Min(ClusterSize, Tiling.Height - j);
                var cluster = new Cluster(Tiling, clusterId++, clusterX, clusterY, new Position(i, j), new Size(horizSize, vertSize));
                AbsTiling.AddCluster(cluster);

                // add entrances
                if (j > 0)
                    entranceId = CreateHorizEntrances(i, i + horizSize - 1, j - 1, AbsTiling.GetCluster(clusterX, clusterY - 1).Id, cluster.Id, entranceId);

                if (i > 0)
                    entranceId = CreateVertEntrances(j, j + vertSize - 1, i - 1, AbsTiling.GetCluster(clusterX - 1, clusterY).Id, cluster.Id, entranceId);

                // NOTE: This piece of code creates diagonal entrances between 2 clusters placed in diagonal
                // against each other. Leaving this commented as a reminder until I make sure whether this
                // is useful or not.
                //if (AbsTiling.Type ==  TileType.OCTILE)
                //{
                //    if (j > 0 && j < m_tiling.getHeight())
                //        createDHEntrances(i, i + horizSize - 2, j - 1, row - 1, col, &entranceId);
                //    if (i > 0 && i < m_tiling.getWidth())
                //        createDVEntrances(j, j + vertSize - 2, i - 1, row, col - 1, &entranceId);
                //}
            }
            
            AbsTiling.AddAbstractNodes();
            AbsTiling.ComputeClusterPaths();
        }

        private void CreateEdges()
        {
            AbsTiling.CreateEdges();
        }

        // TODO: Together with Vert Entrances, refactor the code, they are too similar!
        /// <summary>
        /// Creates the horizontal entrances between the two clusters, and returns the last entrance id
        /// </summary>
        private int CreateHorizEntrances(
            int x0,
            int x1,
            int y,
            int clusterid1,
            int clusterid2,
            int currId)
        {
            var currentIdCounter = currId;

            // rolls over the horizontal edge between x0 and x1 in order to find edges between
            // the top cluster (latitude marks the other cluster entrance line)
            for (var i = x0; i <= x1; i++)
            {
                var node1isObstacle = Tiling[i, y].Info.IsObstacle;
                var node2isObstacle = Tiling[i, y + 1].Info.IsObstacle;
                // get the next communication spot
                if (node1isObstacle || node2isObstacle)
                {
                    continue;
                }

                // start building and tracking the entrance
                var entranceStart = i;
                while (true)
                {
                    i++;
                    if (i >= x1)
                        break;
                    node1isObstacle = Tiling[i, y].Info.IsObstacle;
                    node2isObstacle = Tiling[i, y + 1].Info.IsObstacle;
                    if (node1isObstacle || node2isObstacle || i >= x1)
                        break;
                }

                if (EntranceStyle == EntranceStyle.END_ENTRANCE && i - entranceStart > MAX_ENTRANCE_WIDTH)
                {
                    // If the tracked entrance is big, create 2 entrance points at the edges of the entrance.
                    // create two new entrances, one for each end
                    var entrance1 = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, entranceStart,
                                       this.Tiling[entranceStart, y].NodeId,
                                       this.Tiling[entranceStart, y + 1].NodeId, Orientation.HORIZONTAL);
                    AbsTiling.AddEntrance(entrance1);
                    var entrance2 = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, (i - 1),
                                       this.Tiling[i - 1, y].NodeId,
                                       this.Tiling[i - 1, y + 1].NodeId, Orientation.HORIZONTAL);
                    AbsTiling.AddEntrance(entrance2);
                }
                else
                {
                    // if it is small, create one entrance in the middle 
                    var entrance = new Entrance(currentIdCounter++, clusterid1, clusterid2, y, ((i - 1) + entranceStart) / 2,
                                      this.Tiling[((i - 1) + entranceStart) / 2, y].NodeId,
                                      this.Tiling[((i - 1) + entranceStart) / 2, y + 1].NodeId, Orientation.HORIZONTAL);
                    AbsTiling.AddEntrance(entrance);
                }
            }

            return currentIdCounter;
        }

        private int CreateVertEntrances(int y0, int y1, int x, int clusterid1,
            int clusterid2, int currId)
        {
            var currentIdCounter = currId;

            for (var i = y0; i <= y1; i++)
            {
                var node1isObstacle = Tiling[x, i].Info.IsObstacle;
                var node2isObstacle = Tiling[x + 1, i].Info.IsObstacle;
                // get the next communication spot
                if (node1isObstacle || node2isObstacle)
                {
                    continue;
                }

                // start building the entrance
                var entranceStart = i;
                while (true)
                {
                    i++;
                    if (i >= y1)
                        break;
                    node1isObstacle = Tiling[x, i].Info.IsObstacle;
                    node2isObstacle = Tiling[x + 1, i].Info.IsObstacle;
                    if (node1isObstacle || node2isObstacle || i >= y1)
                        break;
                }

                if (EntranceStyle == EntranceStyle.END_ENTRANCE && (i - entranceStart) > MAX_ENTRANCE_WIDTH)
                {
                    // create two entrances, one for each end
                    var entrance1 = new Entrance(currentIdCounter++, clusterid1, clusterid2, entranceStart, x,
                                       this.Tiling[x, entranceStart].NodeId,
                                       this.Tiling[x + 1, entranceStart].NodeId, Orientation.VERTICAL);
                    AbsTiling.AddEntrance(entrance1);

                    // BEWARE! We are getting the tileNode for position i - 1. If clustersize was 8
                    // for example, and end would had finished at 7, you would set the entrance at 6.
                    // This seems to be intended.
                    var entrance2 = new Entrance(currentIdCounter++, clusterid1, clusterid2, (i - 1), x,
                                       this.Tiling[x, i - 1].NodeId,
                                       this.Tiling[x + 1, i - 1].NodeId, Orientation.VERTICAL);
                    AbsTiling.AddEntrance(entrance2);
                }
                else
                {
                    // create one entrance
                    var entrance = new Entrance(currentIdCounter++, clusterid1, clusterid2, ((i - 1) + entranceStart) / 2, x,
                                      this.Tiling[x, (i - 1 + entranceStart) / 2].NodeId,
                                      this.Tiling[x + 1, (i - 1 + entranceStart) / 2].NodeId, Orientation.VERTICAL);
                    AbsTiling.AddEntrance(entrance);
                }
            }

            return currentIdCounter;
        }
    }
}
