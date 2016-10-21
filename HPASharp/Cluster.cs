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
        public int Id { get; set; }
        public int ClusterY { get; set; }
        public int ClusterX { get; set; }

        /// <summary>
        /// A 2D array which represents a distance between 2 entrances.
        /// This array could be represented as a Dictionary, but it's faster
        /// to use an array.
        /// </summary>
        public Dictionary<Tuple<int, int>, int> Distances { get; set; }

		public Dictionary<Tuple<int, int>, List<int>> CachedPaths { get; set; }

        // Tells whether a path has already been calculated for 2 node ids
        public Dictionary<Tuple<int, int>, bool> DistanceCalculated { get; set; } 
        
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
            Distances = new Dictionary<Tuple<int, int>, int>();
			CachedPaths = new Dictionary<Tuple<int, int>, List<int>>();
			DistanceCalculated = new Dictionary<Tuple<int, int>, bool>();
            EntrancePoints = new List<EntrancePoint>();
        }

        /// <summary>
        /// Computes the paths that lie inside the cluster, 
        /// connecting the several entrances among them
        /// </summary>
        public void ComputeInternalPaths()
        {
            foreach (var point1 in EntrancePoints)
            foreach (var point2 in EntrancePoints)
                ComputePathBetweenEntrances(point1, point2);
        }

        /// <summary>
        /// Gets the index of the entrance point inside this cluster
        /// </summary>
        private int GetEntrancePositionIndex(EntrancePoint entrancePoint)
        {
            return entrancePoint.RelativePos.Y * Size.Width + entrancePoint.RelativePos.X;
        }
        
        private void ComputePathBetweenEntrances(EntrancePoint e1, EntrancePoint e2)
        {
            var start = GetEntrancePositionIndex(e1);
            var target = GetEntrancePositionIndex(e2);
            var startIdx = e1.EntranceLocalIdx;
            var targetIdx = e2.EntranceLocalIdx;
	        var tuple = Tuple.Create(startIdx, targetIdx);
			var invtuple = Tuple.Create(targetIdx, startIdx);

			// If a path already existed, or both are the same node, just return
			if (this.DistanceCalculated.ContainsKey(tuple) || startIdx == targetIdx)
                return;

            var search = new AStar();
            var path = search.FindPath(SubConcreteMap, start, target);

            // TODO: Store the path as well, not only the cost. This will make everything faster!
	        if (path.PathCost != -1)
	        {
				// Yeah, we are supposing reaching A - B is the same like reaching B - A. Which
				// depending on the game this is NOT necessarily true (e.g climbing, downstepping a mountain)
		        Distances[tuple] = Distances[invtuple] = path.PathCost;
		        CachedPaths[tuple] = CachedPaths[invtuple] = path.PathNodes;
	        }

            this.DistanceCalculated[tuple] = this.DistanceCalculated[invtuple] = true;
        }
        
        public void UpdatePaths(int localEntranceId)
        {
            var entrance = EntrancePoints[localEntranceId];
	        foreach (var j in EntrancePoints)
	        {
		        ComputePathBetweenEntrances(entrance, j);
	        }
        }

        // Gets the abstract node Id that an entrance belong to
        public int GetGlobalAbsNodeId(int localIdx)
        {
            return EntrancePoints[localIdx].AbsNodeId;
        }

        public int GetDistance(int localIdx1, int localIdx2)
        {
            return Distances[Tuple.Create(localIdx1,localIdx2)];
        }

		public List<int> GetPath(int localIdx1, int localIdx2)
		{
			return CachedPaths[Tuple.Create(localIdx1, localIdx2)];
		}

		/// <summary>
		/// Tells whether a path exists inside the cluster between localIdx1 and localIdx2
		/// </summary>
		public bool AreConnected(int localIdx1, int localIdx2)
        {
            return Distances.ContainsKey(Tuple.Create(localIdx1,localIdx2));
        }

        public int GetNrEntrances()
        {
            return EntrancePoints.Count;
        }

        /// <summary>
        /// Adds an entrance point to the cluster and returns the entrance index assigned for the point
        /// </summary>
        public int AddEntrance(int abstractNodeId, Position relativePosition)
        {
            var entranceLocalIdx = EntrancePoints.Count;
            var localEntrance = new EntrancePoint(
                abstractNodeId,
                EntrancePoints.Count,
                relativePosition);
            EntrancePoints.Add(localEntrance);
            return entranceLocalIdx;
        }

        public void RemoveLastEntranceRecord()
        {
            EntrancePoints.RemoveAt(EntrancePoints.Count - 1);
            var idx = EntrancePoints.Count;

	        var keysToRemove = this.DistanceCalculated.Keys.Where(k => k.Item1 == idx || k.Item2 == idx).ToList();

			foreach (var key in keysToRemove)
			{
				this.DistanceCalculated.Remove(key);
				this.Distances.Remove(key);
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