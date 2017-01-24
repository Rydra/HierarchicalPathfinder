using System;
using System.Collections.Generic;
using HPASharp.Infrastructure;
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
    public struct AStarNode<TNode>
    {
        public AStarNode(Id<TNode> parent, int g, int h, CellStatus status)
        {
            Parent = parent;
            G = g;
            H = h;
	        F = g + h;
            Status = status;
        }

        public Id<TNode> Parent;
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
	
	public class Path<TNode>
	{
		public int PathCost { get; private set; }
		public List<Id<TNode>> PathNodes { get; private set; }

		public Path(List<Id<TNode>> pathNodes, int pathCost)
		{
			PathCost = pathCost;
			PathNodes = pathNodes;
		}
	}

	public class NodeLookup<TNode>
	{
		private AStarNode<TNode>?[] _astarNodes;

		public NodeLookup(int numberOfNodes)
		{
			_astarNodes = new AStarNode<TNode>?[numberOfNodes];
		}

		public void SetNodeValue(Id<TNode> nodeId, AStarNode<TNode> value)
		{
			_astarNodes[nodeId.IdValue] = value;
		}

		public bool NodeIsVisited(Id<TNode> nodeId)
		{
			return _astarNodes[nodeId.IdValue].HasValue;
		}

		public AStarNode<TNode> GetNodeValue(Id<TNode> nodeId)
		{
			return _astarNodes[nodeId.IdValue].Value;
		}
	}

	public class AStar<TNode>
	{
		private Func<Id<TNode>, bool> isGoal;
		private Func<Id<TNode>, int> calculateHeuristic;
		private IMap<TNode> map;

		/// <summary>
		/// Performs an A* search following the Node Array A* implementation
		/// </summary>
		public Path<TNode> FindPath(IMap<TNode> map, Id<TNode> startNodeId, Id<TNode> targetNodeId)
        {
			isGoal = nodeId => nodeId == targetNodeId;
			calculateHeuristic = nodeId => map.GetHeuristic(nodeId, targetNodeId);
			this.map = map;

			var estimatedCost = calculateHeuristic(startNodeId);

            var startNode = new AStarNode<TNode>(startNodeId, 0, estimatedCost, CellStatus.Open);
			var openQueue = new SimplePriorityQueue<Id<TNode>>();
			openQueue.Enqueue(startNodeId, startNode.F);
			
			var nodeLookup = new NodeLookup<TNode>(map.NrNodes);
	        nodeLookup.SetNodeValue(startNodeId, startNode);
			
            while (openQueue.Count != 0)
            {
                var nodeId = openQueue.Dequeue();
	            var node = nodeLookup.GetNodeValue(nodeId);

				if (isGoal(nodeId))
                {
                    return ReconstructPath(nodeId, nodeLookup);
                }
				
                ProcessNeighbours(nodeId, node, nodeLookup, openQueue);
				
				nodeLookup.SetNodeValue(nodeId, new AStarNode<TNode>(node.Parent, node.G, node.H, CellStatus.Closed));
			}
			
	        return new Path<TNode>(new List<Id<TNode>>(), -1);
        }
		
		private void ProcessNeighbours(Id<TNode> nodeId, AStarNode<TNode> node, NodeLookup<TNode> nodeLookup, SimplePriorityQueue<Id<TNode>> openQueue)
		{
			var connections = map.GetConnections(nodeId);
			foreach (var connection in connections)
			{
				var gCost = node.G + connection.Cost;
				var neighbour = connection.Target;
				if (nodeLookup.NodeIsVisited(neighbour))
				{
					var targetAStarNode = nodeLookup.GetNodeValue(neighbour);
					// If we already processed the neighbour in the past or we already found in the past
					// a better path to reach this node that the current one, just skip it, else create
					// and replace a new PathNode
					if (targetAStarNode.Status == CellStatus.Closed || gCost >= targetAStarNode.G)
						continue;

					targetAStarNode = new AStarNode<TNode>(nodeId, gCost, targetAStarNode.H, CellStatus.Open);
					openQueue.UpdatePriority(neighbour, targetAStarNode.F);
					nodeLookup.SetNodeValue(neighbour, targetAStarNode);
				}
				else
				{
					var newHeuristic = calculateHeuristic(neighbour);
					var newAStarNode = new AStarNode<TNode>(nodeId, gCost, newHeuristic, CellStatus.Open);
					openQueue.Enqueue(neighbour, newAStarNode.F);
					nodeLookup.SetNodeValue(neighbour, newAStarNode);
				}
			}
		}

		/// <summary>
		/// Reconstructs the path from the destination node with the aid
		/// of the node Lookup that stored the states of all processed nodes
		/// TODO: Maybe I should guard this with some kind of safetyGuard to prevent
		/// possible infinite loops in case of bugs, but meh...
		/// </summary>
		private Path<TNode> ReconstructPath(Id<TNode> destination, NodeLookup<TNode> nodeLookup)
		{
			var pathNodes = new List<Id<TNode>>();
			var pathCost = nodeLookup.GetNodeValue(destination).F;
			var currentNode = destination;
			while (nodeLookup.GetNodeValue(currentNode).Parent != currentNode)
			{
				pathNodes.Add(currentNode);
				currentNode = nodeLookup.GetNodeValue(currentNode).Parent;
			}

			pathNodes.Add(currentNode);
			pathNodes.Reverse();

			return new Path<TNode>(pathNodes, pathCost);
		}
	}
}
