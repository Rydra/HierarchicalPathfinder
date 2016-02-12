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

	public class LocalEntrance
	{
		public int AbsNodeId { get; set; } // id of the abstract node

		// Relative position of entrance inside cluster
		public Position RelativePos { get; set; }
		public int EntranceLocalIdx { get; set; } // local id

		public LocalEntrance(int absNodeId, int localIdx, Position relativePosition)
		{
			AbsNodeId = absNodeId;
			EntranceLocalIdx = localIdx;
			RelativePos = relativePosition;
		}
	}

    public class Cluster
    {
        const int MAX_CLENTRANCES = 50;

        public int Id { get; set; }
        public int ClusterY { get; set; }
        public int ClusterX { get; set; }
        public int[,] Distances { get; set; }
        public bool[,] DistanceCalculated { get; set; } // Tells whether a path has already been calculated for 2 node ids
        
		// A local entrance is a point inside this cluster
		public List<LocalEntrance> Entrances { get; set; }
		
		// This tiling object contains the subregion of the main grid that this cluster contains.
		// Necessary to do local search to find paths and distances between local entrances
        public Tiling Tiling { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; } // The position where this cluster starts in the main grid

        public Cluster(Tiling tiling, int id, int clusterX, int clusterY, Position origin, Size size)
        {
            Tiling = new Tiling(tiling, origin.X, origin.Y, size.Width, size.Height, tiling.Passability);
            Id = id;
            ClusterY = clusterY;
            this.ClusterX = clusterX;
            Origin = origin;
            Size = size;
            Distances = new int[MAX_CLENTRANCES, MAX_CLENTRANCES];
            this.DistanceCalculated = new bool[MAX_CLENTRANCES, MAX_CLENTRANCES];
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
                    this.DistanceCalculated[i,j] = false;

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
            var search = new AStar();
            search.FindPath(Tiling, target, start);
            return search.PathCost;
        }

        private void ComputeAddPath(LocalEntrance e1, LocalEntrance e2)
        {
            var start = GetEntrancePositionIndex(e1);
            var target = GetEntrancePositionIndex(e2);
            var startIdx = e1.EntranceLocalIdx;
            var targetIdx = e2.EntranceLocalIdx;

            //If a path already existed, or both are the same node, just return
            if (this.DistanceCalculated[startIdx,targetIdx] || startIdx == targetIdx)
                return;

            var searchUtils = new SearchUtils();
            if (searchUtils.checkPathExists(Tiling, start, target))
                Distances[startIdx,targetIdx] = Distances[targetIdx,startIdx] = ComputeDistance(start, target);
            else
                NoPath(startIdx, targetIdx);

            this.DistanceCalculated[startIdx,targetIdx] = true;
            this.DistanceCalculated[targetIdx,startIdx] = true;
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
                this.DistanceCalculated[idx,i] = this.DistanceCalculated[i,idx] = false;
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
            var search = new AStar();
            search.FindPath(Tiling, target, start);
            return search.Path;
        }

        private int GetPointId(int row, int col)
        {
            return row *Size.Width + col;
        }
    
    };
}