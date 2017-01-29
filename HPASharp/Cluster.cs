using System;
using System.Collections.Generic;
using HPASharp.Graph;
using HPASharp.Infrastructure;
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
		public Id<AbstractNode> AbstractNodeId { get; set; }
		public Position RelativePosition { get; set; }

		public EntrancePoint(Id<AbstractNode> abstractNodeId, Position relativePosition)
		{
			AbstractNodeId = abstractNodeId;  
			RelativePosition = relativePosition;
		}
	}

    public class Cluster
    {
        public Id<Cluster> Id { get; set; }
        public int ClusterY { get; set; }
        public int ClusterX { get; set; }

	    /// <summary>
	    /// A 2D array which represents a distance between 2 entrances.
	    /// This array could be represented as a Dictionary, but it's faster
	    /// to use an array.
	    /// </summary>
	    private readonly Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, int> _distances;

	    private readonly Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, List<Id<ConcreteNode>>> _cachedPaths;

        // Tells whether a path has already been calculated for 2 node ids
	    private readonly Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, bool> _distanceCalculated;
        
		public List<EntrancePoint> EntrancePoints { get; set; }
		
		// This concreteMap object contains the subregion of the main grid that this cluster contains.
		// Necessary to do local search to find paths and distances between local entrances
        public ConcreteMap SubConcreteMap { get; set; }
        public Size Size { get; set; }
        public Position Origin { get; set; }

        public Cluster(ConcreteMap concreteMap, Id<Cluster> id, int clusterX, int clusterY, Position origin, Size size)
        {
            SubConcreteMap = concreteMap.Slice(origin.X, origin.Y, size.Width, size.Height, concreteMap.Passability);
            Id = id;
            ClusterY = clusterY;
            ClusterX = clusterX;
            Origin = origin;
            Size = size;
            _distances = new Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, int>();
			_cachedPaths = new Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, List<Id<ConcreteNode>>>();
			_distanceCalculated = new Dictionary<Tuple<Id<AbstractNode>, Id<AbstractNode>>, bool>();
            EntrancePoints = new List<EntrancePoint>();
        }
        
        public void CreateIntraClusterEdges()
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
	        if (e1.AbstractNodeId == e2.AbstractNodeId)
		        return;
			
	        var tuple = Tuple.Create(e1.AbstractNodeId, e2.AbstractNodeId);
			var invtuple = Tuple.Create(e2.AbstractNodeId, e1.AbstractNodeId);
			
			if (_distanceCalculated.ContainsKey(tuple))
                return;
			
			var startNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(e1));
			var targetNodeId = Id<ConcreteNode>.From(GetEntrancePositionIndex(e2));
			var search = new AStar<ConcreteNode>(SubConcreteMap, startNodeId, targetNodeId);
			var path = search.FindPath();
			
	        if (path.PathCost != -1)
	        {
				// Yeah, we are supposing reaching A - B is the same like reaching B - A. Which
				// depending on the game this is NOT necessarily true (e.g climbing, downstepping a mountain)
		        _distances[tuple] = _distances[invtuple] = path.PathCost;
		        _cachedPaths[tuple] = new List<Id<ConcreteNode>>(path.PathNodes);
		        path.PathNodes.Reverse();
		        _cachedPaths[invtuple] = path.PathNodes;

	        }

            _distanceCalculated[tuple] = _distanceCalculated[invtuple] = true;
        }
        
        public void UpdatePathsForLocalEntrance(EntrancePoint srcEntrancePoint)
        {
	        foreach (var entrancePoint in EntrancePoints)
	        {
		        ComputePathBetweenEntrances(srcEntrancePoint, entrancePoint);
	        }
        }
		
        public int GetDistance(Id<AbstractNode> abstractNodeId1, Id<AbstractNode> abstractNodeId2)
        {
            return _distances[Tuple.Create(abstractNodeId1,abstractNodeId2)];
        }

		public List<Id<ConcreteNode>> GetPath(Id<AbstractNode> abstractNodeId1, Id<AbstractNode> abstractNodeId2)
		{
			return _cachedPaths[Tuple.Create(abstractNodeId1, abstractNodeId2)];
		}
        
		public bool AreConnected(Id<AbstractNode> abstractNodeId1, Id<AbstractNode> abstractNodeId2)
        {
            return _distances.ContainsKey(Tuple.Create(abstractNodeId1,abstractNodeId2));
        }

		public int NumberOfEntrances => EntrancePoints.Count;
		
        public EntrancePoint AddEntrance(Id<AbstractNode> abstractNodeId, Position relativePosition)
        {
            var entrancePoint = new EntrancePoint(abstractNodeId, relativePosition);
            EntrancePoints.Add(entrancePoint);
	        return entrancePoint;
        }

        public void RemoveLastEntranceRecord()
        {
            var entrancePoint = EntrancePoints[EntrancePoints.Count - 1];
            
            var abstractNodeToRemove = entrancePoint.AbstractNodeId;
            EntrancePoints.RemoveAt(EntrancePoints.Count - 1);
            var keysToRemove = new List<Tuple<Id<AbstractNode>, Id<AbstractNode>>>();
            foreach (var key in _distanceCalculated.Keys)
            {
                if (key.Item1 == abstractNodeToRemove || key.Item2 == abstractNodeToRemove)
                {
                    keysToRemove.Add(key);
                }
            }

			foreach (var key in keysToRemove)
			{
				_distanceCalculated.Remove(key);
				_distances.Remove(key);
			    _cachedPaths.Remove(key);
			}
        }
    }
}