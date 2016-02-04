using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Search;

namespace HPASharp
{
    public enum Orientation
    {
        HORIZONTAL, VERTICAL, HDIAG1, HDIAG2, VDIAG1, VDIAG2
    }

    /// <summary>
    /// Represents an entrance point between 2 clusters
    /// </summary>
    public class Entrance
    {
        public int Id { get; set; }
        public int Cluster1Id { get; set; }
        public int Cluster2Id { get; set; }
        public int Center1Id { get; set; }
        public int Center2Id { get; set; }
        public Orientation Orientation { get; set; }
        
        public Position Center1 { get; set; }

        public Position Center2
        {
            get
            {
                int x;
                switch (Orientation)
                {
                    case Orientation.HORIZONTAL:
                    case Orientation.HDIAG2:
                        x = Center1.X;
                        break;
                    case Orientation.VERTICAL:
                    case Orientation.VDIAG2:
                    case Orientation.VDIAG1:
                    case Orientation.HDIAG1:
                        x = Center1.X + 1;
                        break;
                    default:
                        //assert(false);
                        x = - 1;
                        break;
                }

                int y;
                switch (Orientation)
                {
                    case Orientation.HORIZONTAL:
                    case Orientation.HDIAG1:
                    case Orientation.HDIAG2:
                    case Orientation.VDIAG1:
                        y = Center1.Y + 1;
                        break;
                    case Orientation.VERTICAL:
                    case Orientation.VDIAG2:
                        y = Center1.Y;
                        break;
                    default:
                        //assert(false);
                        y = - 1;
                        break;
                }

                return new Position(x, y);
            }
        }

        public Entrance(int id, int cl1Id, int cl2Id, int center1Row, int center1Col, int center1Id, int center2Id, Orientation orientation)
        {
            Id = id;
            Cluster1Id = cl1Id;
            Cluster2Id = cl2Id;

            int center1y, center1x;
            if (orientation == Orientation.HDIAG2)
            {
                center1x = center1Col + 1;
            }
            else
            {
                center1x = center1Col;
            }

            if (orientation == Orientation.VDIAG2)
            {
                center1y = center1Row + 1;
            }
            else
            {
                center1y = center1Row;
            }
            
            Center1 = new Position(center1x, center1y);
            Center1Id = center1Id;
            Center2Id = center2Id;
            Orientation = orientation;
        }
    }

    public class Cluster
    {
        const int MAX_CLENTRANCES = 50;

        public int Id { get; set; }
        public int Row { get; set; } // abstract row of this cluster (e.g., 1 for the second clusters horizontally)
        public int Column { get; set; } // abstract col of this cluster (e.g., 1 for the second clusters vertically)
        public int[,] Distances { get; set; }
        public bool[,] BoolPathMap { get; set; }
        public List<LocalEntrance> Entrances { get; set; }
        public Tiling Tiling { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; }

        public Cluster(Tiling tiling, int id,
                int row, int col,
                Position origin,
                Size size)
        {
            Tiling = new Tiling(tiling, origin.X, origin.Y, size.Width, size.Height);
            Id = id;
            Row = row;
            Column = col;
            Origin = origin;
            Size = size;
            Distances = new int[MAX_CLENTRANCES, MAX_CLENTRANCES];
            BoolPathMap = new bool[MAX_CLENTRANCES, MAX_CLENTRANCES];
            Entrances = new List<LocalEntrance>();
        }

        /// <summary>
        /// Computes the paths that lie inside the cluster, 
        /// connecting the several entrances among them
        /// </summary>
        public void computePaths()
        {
            for (int i = 0; i < MAX_CLENTRANCES; i++)
                for (int j = 0; j < MAX_CLENTRANCES; j++)
                    BoolPathMap[i,j] = false;

            foreach(var entrance1 in Entrances)
                foreach (var entrance2 in Entrances)
                    computeAddPath(entrance1, entrance2);
        }

        private int getEntranceCenter(LocalEntrance entrance)
        {
            return entrance.RelativePos.Y *Size.Width + entrance.RelativePos.X;
        }

        private void addNoPath(int startIdx, int targetIdx)
        {
            Distances[startIdx,targetIdx] = Distances[targetIdx,startIdx] = int.MaxValue;
        }

        private int computeDistance(int start, int target)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(false));
            search.findPath(Tiling, target, start);
            return search.getPathCost();
        }

        private void computeAddPath(LocalEntrance e1, LocalEntrance e2)
        {
            int start = getEntranceCenter(e1);
            int target = getEntranceCenter(e2);
            int startIdx = e1.EntranceLocalIdx;
            int targetIdx = e2.EntranceLocalIdx;

            //If a path already existed, or both are the same node, just return
            if (BoolPathMap[startIdx,targetIdx] || startIdx == targetIdx)
                return;

            var searchUtils = new SearchUtils();
            if (searchUtils.checkPathExists(Tiling, start, target))
                Distances[startIdx,targetIdx] = Distances[targetIdx,startIdx] = computeDistance(start, target);
            else
                addNoPath(startIdx, targetIdx);

            BoolPathMap[startIdx,targetIdx] = true;
            BoolPathMap[targetIdx,startIdx] = true;
        }
        
        public void updatePaths(int entranceId)
        {
            var entrance = Entrances[entranceId];
            foreach(var j in Entrances)
                computeAddPath(entrance, j);
        }

        // Gets the abstract node Id that an entrance belong to
        public int getGlobalAbsNodeId(int localIdx)
        {
            return Entrances[localIdx].AbsNodeId;
        }

        public int getDistance(int localIdx1, int localIdx2)
        {
            return Distances[localIdx1,localIdx2];
        }

        public bool areConnected(int localIdx1, int localIdx2)
        {
            return (Distances[localIdx1,localIdx2] != int.MaxValue);
        }

        public int getNrEntrances()
        {
            return Entrances.Count;
        }

        public void addEntrance(LocalEntrance entrance)
        {
            Entrances.Add(entrance);
            Entrances[Entrances.Count - 1].EntranceLocalIdx = Entrances.Count - 1;
        }

        public void removeLastEntranceRecord()
        {
            Entrances.RemoveAt(Entrances.Count - 1);
            int idx = Entrances.Count;
            for (int i = 0; i < MAX_CLENTRANCES; i++)
            {
                BoolPathMap[idx,i] = BoolPathMap[i,idx] = false;
                Distances[idx,i] = Distances[i,idx] = int.MaxValue;
            }
        }

//          const vector<int>& getPath(int idx1, int idx2) const
//          {
//             assert(m_distances[idx1][idx2] != s_Infinity);
//             return m_paths[idx1][idx2];
//          }

        public int getLocalCenter(int localIndex)
        {
            var entrance = Entrances[localIndex];
            return entrance.RelativePos.Y *Size.Width + entrance.CenterCol;
        }

        public List<int> computePath(int start, int target)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(false));
            search.findPath(Tiling, target, start);
            return search.getPath();
        }

        private int getPointId(int row, int col)
        {
            return row *Size.Width + col;
        }
    
    };
}