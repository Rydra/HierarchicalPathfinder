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
		private readonly Func<Id<TNode>, bool> _isGoal;
		private readonly Func<Id<TNode>, int> _calculateHeuristic;
		private readonly IMap<TNode> _map;
		private readonly SimplePriorityQueue<Id<TNode>> _openQueue;
		private readonly NodeLookup<TNode> _nodeLookup;
		
		public AStar(IMap<TNode> map, Id<TNode> startNodeId, Id<TNode> targetNodeId)
		{
			_isGoal = nodeId => nodeId == targetNodeId;
			_calculateHeuristic = nodeId => map.GetHeuristic(nodeId, targetNodeId);
			_map = map;

			var estimatedCost = _calculateHeuristic(startNodeId);

			var startNode = new AStarNode<TNode>(startNodeId, 0, estimatedCost, CellStatus.Open);
			_openQueue = new SimplePriorityQueue<Id<TNode>>();
			_openQueue.Enqueue(startNodeId, startNode.F);

			_nodeLookup = new NodeLookup<TNode>(map.NrNodes);
			_nodeLookup.SetNodeValue(startNodeId, startNode);
		}

		public bool NodeIsClosed(Id<TNode> nodeId)
		{
			return _nodeLookup.NodeIsVisited(nodeId) && _nodeLookup.GetNodeValue(nodeId).Status == CellStatus.Closed;
		}

		public bool CanExpand
		{
			get { return _openQueue != null && _openQueue.Count != 0; }
		}

		public static Path<TNode> FindBidiPath(IMap<TNode> map, Id<TNode> startNodeId, Id<TNode> targetNodeId)
		{
			var search1 = new AStar<TNode>(map, startNodeId, targetNodeId);
			var search2 = new AStar<TNode>(map, targetNodeId, startNodeId);
			var expand = 0;

			while (search1.CanExpand && search2.CanExpand)
			{
				var frontier = search1.Expand();
				expand++;
				if (search2.NodeIsClosed(frontier)) //TODO: Need to add a condition to tell that the node was reachable
				{
					return ReconstructPath(search1, search2, frontier);
				}

				frontier = search2.Expand();
				expand++;
				if (search1.NodeIsClosed(frontier)) //TODO: Need to add a condition to tell that the node was reachable
				{
					return ReconstructPath(search1, search2, frontier);
				}
			}

			return new Path<TNode>(new List<Id<TNode>>(), -1);
		}

		private static Path<TNode> ReconstructPath(AStar<TNode> search1, AStar<TNode> search2, Id<TNode> frontier)
		{
			var halfPath1 = search1.ReconstructPathFrom(frontier);
			var halfPath2 = search2.ReconstructPathFrom(frontier);

			halfPath2.PathNodes.Reverse();
			var p = halfPath2.PathNodes;
			if (p.Count > 0)
			{
				for (int i = 1; i < p.Count; i++)
				{
					halfPath1.PathNodes.Add(p[i]);
				}

			}

			return halfPath1;
		}

		public Path<TNode> FindPath()
		{
            while (CanExpand)
            {
	            var nodeId = Expand();
	            if (_isGoal(nodeId))
				{
					return ReconstructPathFrom(nodeId);
				}
            }
			
	        return new Path<TNode>(new List<Id<TNode>>(), -1);
        }

		private Id<TNode> Expand()
		{
			var nodeId = _openQueue.Dequeue();
			var node = _nodeLookup.GetNodeValue(nodeId);

			ProcessNeighbours(nodeId, node);

			_nodeLookup.SetNodeValue(nodeId, new AStarNode<TNode>(node.Parent, node.G, node.H, CellStatus.Closed));
			return nodeId;
		}

		private void ProcessNeighbours(Id<TNode> nodeId, AStarNode<TNode> node)
		{
			var connections = _map.GetConnections(nodeId);
			foreach (var connection in connections)
			{
				var gCost = node.G + connection.Cost;
				var neighbour = connection.Target;
				if (_nodeLookup.NodeIsVisited(neighbour))
				{
					var targetAStarNode = _nodeLookup.GetNodeValue(neighbour);
					// If we already processed the neighbour in the past or we already found in the past
					// a better path to reach this node that the current one, just skip it, else create
					// and replace a new PathNode
					if (targetAStarNode.Status == CellStatus.Closed || gCost >= targetAStarNode.G)
						continue;

					targetAStarNode = new AStarNode<TNode>(nodeId, gCost, targetAStarNode.H, CellStatus.Open);
					_openQueue.UpdatePriority(neighbour, targetAStarNode.F);
					_nodeLookup.SetNodeValue(neighbour, targetAStarNode);
				}
				else
				{
					var newHeuristic = _calculateHeuristic(neighbour);
					var newAStarNode = new AStarNode<TNode>(nodeId, gCost, newHeuristic, CellStatus.Open);
					_openQueue.Enqueue(neighbour, newAStarNode.F);
					_nodeLookup.SetNodeValue(neighbour, newAStarNode);
				}
			}
		}

		/// <summary>
		/// Reconstructs the path from the destination node with the aid
		/// of the node Lookup that stored the states of all processed nodes
		/// TODO: Maybe I should guard this with some kind of safetyGuard to prevent
		/// possible infinite loops in case of bugs, but meh...
		/// </summary>
		private Path<TNode> ReconstructPathFrom(Id<TNode> destination)
		{
			var pathNodes = new List<Id<TNode>>();
			var pathCost = _nodeLookup.GetNodeValue(destination).F;
			var currentNode = destination;
			while (_nodeLookup.GetNodeValue(currentNode).Parent != currentNode)
			{
				pathNodes.Add(currentNode);
				currentNode = _nodeLookup.GetNodeValue(currentNode).Parent;
			}

			pathNodes.Add(currentNode);
			pathNodes.Reverse();

			return new Path<TNode>(pathNodes, pathCost);
		}
	}
}
