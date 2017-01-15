using System;
using System.Collections.Generic;
using System.Linq;
using HPASharp.Search;

namespace HPASharp
{
    public enum Orientation
    {
        Horizontal, Vertical, Hdiag1, Hdiag2, Vdiag1, Vdiag2
    }

	/// <summary>
	/// An Entrance Point represents a point inside a cluster   
	/// that belongs to an entrance. It holds a reference to the
	/// abstract node it belongs to
	/// </summary>
	public class EntrancePoint
	{
		public int AbstractNodeId { get; set; }
		public Position RelativePosition { get; set; }

		public EntrancePoint(int abstractNodeId, Position relativePosition)
		{
			AbstractNodeId = abstractNodeId;  
			RelativePosition = relativePosition;
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
        
		public List<EntrancePoint> EntrancePoints { get; set; }
		
		// This concreteMap object contains the subregion of the main grid that this cluster contains.
		// Necessary to do local search to find paths and distances between local entrances
        public ConcreteMap SubConcreteMap { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; }

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
            return entrancePoint.RelativePosition.Y * Size.Width + entrancePoint.RelativePosition.X;
        }
        
        private void ComputePathBetweenEntrances(EntrancePoint e1, EntrancePoint e2)
        {
            var start = GetEntrancePositionIndex(e1);
            var target = GetEntrancePositionIndex(e2);
	        var tuple = Tuple.Create(e1.AbstractNodeId, e2.AbstractNodeId);
			var invtuple = Tuple.Create(e2.AbstractNodeId, e1.AbstractNodeId);

			// If a path already existed, or both are the same node, just return
			if (DistanceCalculated.ContainsKey(tuple) || e1.AbstractNodeId == e2.AbstractNodeId)
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

            DistanceCalculated[tuple] = DistanceCalculated[invtuple] = true;
        }
        
        public void UpdatePathsForLocalEntrance(EntrancePoint srcEntrancePoint)
        {
	        foreach (var entrancePoint in EntrancePoints)
	        {
		        ComputePathBetweenEntrances(srcEntrancePoint, entrancePoint);
	        }
        }
		
        public int GetDistance(int abstractNodeId1, int AbstractNodeId2)
        {
            return Distances[Tuple.Create(abstractNodeId1,AbstractNodeId2)];
        }

		public List<int> GetPath(int abstractNodeId1, int abstractNodeId2)
		{
			return CachedPaths[Tuple.Create(abstractNodeId1, abstractNodeId2)];
		}
        
		public bool AreConnected(int abstractNodeId1, int abstractNodeId2)
        {
            return Distances.ContainsKey(Tuple.Create(abstractNodeId1,abstractNodeId2));
        }

		public int NumberOfEntrances => EntrancePoints.Count;

        /// <summary>
        /// Adds an entrance point to the cluster and returns the entrance index assigned for the point
        /// </summary>
        public EntrancePoint AddEntrance(int abstractNodeId, Position relativePosition)
        {
            var entrancePoint = new EntrancePoint(
                abstractNodeId,
                relativePosition);
            EntrancePoints.Add(entrancePoint);
	        return entrancePoint;
        }

        public void RemoveLastEntranceRecord()
        {
            EntrancePoints.RemoveAt(EntrancePoints.Count - 1);
            var idx = EntrancePoints.Count;

	        var keysToRemove = this.DistanceCalculated.Keys.Where(k => k.Item1 == idx || k.Item2 == idx).ToList();

			foreach (var key in keysToRemove)
			{
				DistanceCalculated.Remove(key);
				Distances.Remove(key);
            }
        }
    }
}