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
	/// An Entrance Point represents a point inside a cluster
	/// that belongs to an entrance. It holds a reference to the
	/// abstract node it belongs to
	/// </summary>
	public class EntrancePoint
	{
		public int AbsNodeId { get; set; } // id of the abstract node

		// Relative position of entrance inside cluster
		public Position RelativePos { get; set; }
		public int EntranceLocalIdx { get; set; } // local id

		public EntrancePoint(int absNodeId, int localIdx, Position relativePosition)
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

        /// <summary>
        /// A 2D array which represents a distance between 2 entrances.
        /// This array could be represented as a Dictionary, but it's faster
        /// to use an array.
        /// </summary>
        public int[,] Distances { get; set; }

        // Tells whether a path has already been calculated for 2 node ids
        public bool[,] DistanceCalculated { get; set; } 
        
		// A local entrance is a point inside this cluster
		public List<EntrancePoint> EntrancePoints { get; set; }
		
		// This concreteMap object contains the subregion of the main grid that this cluster contains.
		// Necessary to do local search to find paths and distances between local entrances
        public ConcreteMap SubConcreteMap { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; } // The position where this cluster starts in the main grid

        public Cluster(ConcreteMap concreteMap, int id, int clusterX, int clusterY, Position origin, Size size)
        {
            SubConcreteMap = concreteMap.Slice(origin.X, origin.Y, size.Width, size.Height, concreteMap.Passability);
            Id = id;
            ClusterY = clusterY;
            ClusterX = clusterX;
            Origin = origin;
            Size = size;
            Distances = new int[MAX_CLENTRANCES, MAX_CLENTRANCES];
            DistanceCalculated = new bool[MAX_CLENTRANCES, MAX_CLENTRANCES];
            EntrancePoints = new List<EntrancePoint>();
        }

        /// <summary>
        /// Computes the paths that lie inside the cluster, 
        /// connecting the several entrances among them
        /// </summary>
        public void ComputeInternalPaths()
        {
            for (var j = 0; j < MAX_CLENTRANCES; j++)
            for (var i = 0; i < MAX_CLENTRANCES; i++)
                this.DistanceCalculated[i,j] = false;

            foreach (var point1 in EntrancePoints)
            foreach (var point2 in EntrancePoints)
                ComputePath(point1, point2);
        }

        /// <summary>
        /// Gets the index of the entrance point inside this cluster
        /// </summary>
        private int GetEntrancePositionIndex(EntrancePoint entrancePoint)
        {
            return entrancePoint.RelativePos.Y * Size.Width + entrancePoint.RelativePos.X;
        }
        
        private void ComputePath(EntrancePoint e1, EntrancePoint e2)
        {
            var start = GetEntrancePositionIndex(e1);
            var target = GetEntrancePositionIndex(e2);
            var startIdx = e1.EntranceLocalIdx;
            var targetIdx = e2.EntranceLocalIdx;

            // If a path already existed, or both are the same node, just return
            if (this.DistanceCalculated[startIdx,targetIdx] || startIdx == targetIdx)
                return;

            var search = new AStar();
            var path = search.FindPath(SubConcreteMap, start, target);

            // TODO: Store the path as well, not only the cost. This will make everything faster!
            if (path.PathCost != -1) Distances[startIdx, targetIdx] = Distances[targetIdx, startIdx] = path.PathCost;
            else Distances[startIdx, targetIdx] = Distances[targetIdx, startIdx] = int.MaxValue;

            this.DistanceCalculated[startIdx,targetIdx] = true;
            this.DistanceCalculated[targetIdx,startIdx] = true;
        }
        
        public void UpdatePaths(int localEntranceId)
        {
            var entrance = EntrancePoints[localEntranceId];
            foreach(var j in EntrancePoints)
                ComputePath(entrance, j);
        }

        // Gets the abstract node Id that an entrance belong to
        public int GetGlobalAbsNodeId(int localIdx)
        {
            return EntrancePoints[localIdx].AbsNodeId;
        }

        public int GetDistance(int localIdx1, int localIdx2)
        {
            return Distances[localIdx1,localIdx2];
        }

        /// <summary>
        /// Tells whether a path exists inside the cluster between localIdx1 and localIdx2
        /// </summary>
        public bool AreConnected(int localIdx1, int localIdx2)
        {
            return Distances[localIdx1,localIdx2] != int.MaxValue;
        }

        public int GetNrEntrances()
        {
            return EntrancePoints.Count;
        }

        /// <summary>
        /// Adds an entrance point to the cluster and returns the entrance index assigned for the point
        /// </summary>
        public int AddEntrance(int abstractNodeId, Position pos)
        {
            var entranceLocalIdx = EntrancePoints.Count;
            var localEntrance = new EntrancePoint(
                abstractNodeId,
                EntrancePoints.Count,
                pos);
            EntrancePoints.Add(localEntrance);
            return entranceLocalIdx;
        }

        public void RemoveLastEntranceRecord()
        {
            EntrancePoints.RemoveAt(EntrancePoints.Count - 1);
            var idx = EntrancePoints.Count;
            for (var i = 0; i < MAX_CLENTRANCES; i++)
            {
                this.DistanceCalculated[idx, i] = this.DistanceCalculated[i, idx] = false;
                Distances[idx, i] = Distances[i,  idx] = int.MaxValue;
            }
        }

        /// <summary>
        /// Gets the index of an entrance point of this cluster
        /// </summary>
        public int GetLocalPosition(int entranceLocalIndex)
        {
            var entrance = EntrancePoints[entranceLocalIndex];
            return entrance.RelativePos.Y * Size.Width + entrance.RelativePos.X;
        }

        public List<int> ComputePath(int start, int target)
        {
            var search = new AStar();
            var path = search.FindPath(SubConcreteMap, target, start);
            return path.PathNodes;
        }
    }
}