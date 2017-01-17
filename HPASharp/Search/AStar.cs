using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

namespace HPASharp.Search
{
	/// <summary>
	/// An A* node embeds the status of a processed node, containing information like
	/// the cost it's taken to reach it (Cost So far, G), the expected cost to reach the goal
	/// (The heuristic, H), the parent where this node came from (which will serve later to reconstruct best paths)
	/// the current Status of the node (Open, Closed, Unexplored, see CellStatus documentation for more information) and the F-score
	/// that serves to compare which nodes are the best
	/// </summary>
    public struct AStarNode
    {
        public AStarNode(int parent, int g, int h, CellStatus status)
        {
            Parent = parent;
            G = g;
            H = h;
	        F = g + h;
            Status = status;
        }

        public int Parent;
        public CellStatus Status;
        public int H;
        public int G;
	    public int F;
    }

	/// <summary>
	/// The cell status indicates whether a node has not yet been processed 
	/// but it lies in the open queue (Open) or the node has been processed (Closed)
	/// </summary>
    public enum CellStatus
    {
        Open,
        Closed
    }
	
	public class Path
	{
		public int PathCost { get; private set; }
		public List<int> PathNodes { get; private set; }

		public Path(List<int> pathNodes, int pathCost)
		{
			PathCost = pathCost;
			PathNodes = pathNodes;
		}
	}

	public class AStar
	{
		private Func<int, bool> isGoal;
		private Func<int, int> calculateHeuristic;
		private IMap map;

		/// <summary>
		/// Performs an A* search following the Node Array A* implementation
		/// </summary>
		public Path FindPath(IMap map, int startNodeId, int targetNodeId)
        {
			isGoal = nodeId => nodeId == targetNodeId;
			calculateHeuristic = nodeId => map.GetHeuristic(nodeId, targetNodeId);
			this.map = map;

			var heuristic = calculateHeuristic(startNodeId);

            var startNode = new AStarNode(startNodeId, 0, heuristic, CellStatus.Open);
			var openQueue = new SimplePriorityQueue<int>();
			openQueue.Enqueue(startNodeId, startNode.F);

			// The open list lookup is indexed by the number of nodes in the graph/map,
			// and it is useful to check quickly the status of any node that has been processed
			var nodeLookup = new AStarNode?[map.NrNodes];
			nodeLookup[startNodeId] = startNode;

            while (openQueue.Count != 0)
            {
                var nodeId = openQueue.Dequeue();
                var node = nodeLookup[nodeId].Value;
				
                if (isGoal(nodeId))
                {
                    return ReconstructPath(nodeId, nodeLookup);
                }

                ProcessNeighbours(nodeId, node, nodeLookup, openQueue);

				// Close the node. I hope some day the will implement something
				// like the records in F# with the "with" keyword
				nodeLookup[nodeId] = new AStarNode(node.Parent, node.G, node.H, CellStatus.Closed);
			}

			// No path found. We could return a null, but since I read the book "Code Complete" I decided
			// its best to return an empty path, and I'll return a -1 as PathCost
			// TODO: Additionally, all those magic numbers like this -1 should be converted to explicit,
			// clearer constants
	        return new Path(new List<int>(), -1);
        }

		/// <summary>
		/// Processes every open or unexplored successor of nodeId
		/// </summary>
		private void ProcessNeighbours(int nodeId, AStarNode node, AStarNode?[] nodeLookup, SimplePriorityQueue<int> openQueue)
		{
			var successors = map.GetNeighbours(nodeId);
			foreach (var successor in successors)
			{
				var newg = node.G + successor.Cost;
				var successorTarget = successor.Target;
				var targetAStarNode = nodeLookup[successorTarget];
				if (targetAStarNode.HasValue)
				{
					// If we already processed the neighbour in the past or we already found in the past
					// a better path to reach this node that the current one, just skip it, else create
					// and replace a new PathNode
					if (targetAStarNode.Value.Status == CellStatus.Closed || newg >= targetAStarNode.Value.G)
						continue;

					targetAStarNode = new AStarNode(nodeId, newg, targetAStarNode.Value.H, CellStatus.Open);
					nodeLookup[successorTarget] = targetAStarNode;
					openQueue.UpdatePriority(successorTarget, targetAStarNode.Value.F);
				}
				else
				{
					var newHeuristic = calculateHeuristic(successorTarget);
					var newAStarNode = new AStarNode(nodeId, newg, newHeuristic, CellStatus.Open);
					openQueue.Enqueue(successorTarget, newAStarNode.F);
					nodeLookup[successorTarget] = newAStarNode;
				}
			}
		}

		/// <summary>
		/// Reconstructs the path from the destination node with the aid
		/// of the node Lookup that stored the states of all processed nodes
		/// TODO: Maybe I should guard this with some kind of safetyGuard to prevent
		/// possible infinite loops in case of bugs, but meh...
		/// </summary>
		private Path ReconstructPath(int destination, AStarNode?[] nodeLookup)
		{
			var pathNodes = new List<int>();
			var pathCost = nodeLookup[destination].Value.F;
			var currnode = destination;
			while (nodeLookup[currnode].Value.Parent != currnode)
			{
				pathNodes.Add(currnode);
				currnode = nodeLookup[currnode].Value.Parent;
			}

			pathNodes.Add(currnode);
			pathNodes.Reverse();

			return new Path(pathNodes, pathCost);
		}
	}
}
