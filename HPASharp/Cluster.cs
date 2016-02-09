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
        public int ClusterY { get; set; } // abstract row of this cluster (e.g., 1 for the second clusters horizontally)
        public int Column { get; set; } // abstract col of this cluster (e.g., 1 for the second clusters vertically)
        public int[,] Distances { get; set; }
        public bool[,] BoolPathMap { get; set; } // Tells whether a path has already been calculated for 2 node ids
        public List<LocalEntrance> Entrances { get; set; }
        public Tiling Tiling { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; }

        public Cluster(Tiling tiling, int id, int clusterX, int clusterY, Position origin, Size size)
        {
            Tiling = new Tiling(tiling, origin.X, origin.Y, size.Width, size.Height);
            Id = id;
            ClusterY = clusterY;
            Column = clusterX;
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
        public void ComputePaths()
        {
            for (var i = 0; i < MAX_CLENTRANCES; i++)
                for (var j = 0; j < MAX_CLENTRANCES; j++)
                    BoolPathMap[i,j] = false;

            foreach(var entrance1 in Entrances)
                foreach (var entrance2 in Entrances)
                    ComputeAddPath(entrance1, entrance2);
        }

        /// <summary>
        /// Gets the index of the entrance point inside this cluster
        /// </summary>
        private int GetEntrancePositionIndex(LocalEntrance entrance)
        {
            return entrance.RelativePos.Y * Size.Width + entrance.RelativePos.X;
        }

        private void NoPath(int startIdx, int targetIdx)
        {
            Distances[startIdx,targetIdx] = Distances[targetIdx,startIdx] = int.MaxValue;
        }

        private int ComputeDistance(int start, int target)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(false));
            search.findPath(Tiling, target, start);
            return search.getPathCost();
        }

        private void ComputeAddPath(LocalEntrance e1, LocalEntrance e2)
        {
            var start = GetEntrancePositionIndex(e1);
            var target = GetEntrancePositionIndex(e2);
            var startIdx = e1.EntranceLocalIdx;
            var targetIdx = e2.EntranceLocalIdx;

            //If a path already existed, or both are the same node, just return
            if (BoolPathMap[startIdx,targetIdx] || startIdx == targetIdx)
                return;

            var searchUtils = new SearchUtils();
            if (searchUtils.checkPathExists(Tiling, start, target))
                Distances[startIdx,targetIdx] = Distances[targetIdx,startIdx] = ComputeDistance(start, target);
            else
                NoPath(startIdx, targetIdx);

            BoolPathMap[startIdx,targetIdx] = true;
            BoolPathMap[targetIdx,startIdx] = true;
        }
        
        public void UpdatePaths(int entranceId)
        {
            var entrance = Entrances[entranceId];
            foreach(var j in Entrances)
                ComputeAddPath(entrance, j);
        }

        // Gets the abstract node Id that an entrance belong to
        public int GetGlobalAbsNodeId(int localIdx)
        {
            return Entrances[localIdx].AbsNodeId;
        }

        public int GetDistance(int localIdx1, int localIdx2)
        {
            return Distances[localIdx1,localIdx2];
        }

        public bool AreConnected(int localIdx1, int localIdx2)
        {
            return Distances[localIdx1,localIdx2] != int.MaxValue;
        }

        public int GetNrEntrances()
        {
            return Entrances.Count;
        }

        public void AddEntrance(LocalEntrance entrance)
        {
            Entrances.Add(entrance);
            Entrances[Entrances.Count - 1].EntranceLocalIdx = Entrances.Count - 1;
        }

        public void RemoveLastEntranceRecord()
        {
            Entrances.RemoveAt(Entrances.Count - 1);
            var idx = Entrances.Count;
            for (var i = 0; i < MAX_CLENTRANCES; i++)
            {
                BoolPathMap[idx,i] = BoolPathMap[i,idx] = false;
                Distances[idx,i] = Distances[i,idx] = int.MaxValue;
            }
        }

        /// <summary>
        /// Gets the index of an entrance point of this cluster
        /// </summary>
        /// <param name="localIndex"></param>
        /// <returns></returns>
        public int GetLocalCenter(int localIndex)
        {
            var entrance = Entrances[localIndex];
            return entrance.RelativePos.Y * Size.Width + entrance.RelativePos.X;
        }

        public List<int> ComputePath(int start, int target)
        {
            ISearch search = new SearchImp();
            search.reset(new AStar(false));
            search.findPath(Tiling, target, start);
            return search.getPath();
        }

        private int GetPointId(int row, int col)
        {
            return row *Size.Width + col;
        }
    
    };
}