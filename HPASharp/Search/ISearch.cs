using System;
using System.Collections.Generic;
using System.Linq;
using Priority_Queue;

namespace HPASharp.Search
{
    public struct AStarNode
    {
        public AStarNode(int parent, int g, int h, CellStatus status)
        {
            Parent = parent;
            G = g;
            H = h;
            Status = status;
        }

        public int Parent;
        public CellStatus Status;
        public int H;
        public int G;
        public int F { get { return G + H; } }
    }

    public enum CellStatus
    {
        Unexplored,
        Open,
        Closed
    }

    public class AStar
    {
        private IMap map;

        private Func<int, bool> isGoal;
        private Func<int, int> getHeuristic;

        public int PathCost { get; set; }
        
        private AStarNode?[] openListLookup;
        private IPriorityQueue<int> openList;

        public List<int> Path { get; set; }

        public bool FindPath(IMap map, int start, int target)
        {
            openList = new SimplePriorityQueue<int>();
            openListLookup = new AStarNode?[map.NrNodes];
            this.map = map;
            this.isGoal = nodeId => nodeId == target;
            this.getHeuristic = nodeId => map.GetHeuristic(nodeId, target);
            Path = new List<int>();
            FindPathAstar(start);
            return true;
        }
        
        private void ReconstructPath(int destination)
        {
            Path = new List<int>();
            PathCost = openListLookup[destination].Value.F;
            var currnode = destination;
            while (openListLookup[currnode].Value.Parent != currnode)
            {
                Path.Add(currnode);
                currnode = openListLookup[currnode].Value.Parent;
            }

            Path.Add(currnode);
        }

        private void FindPathAstar(int start)
        {
            var heuristic = getHeuristic(start);
            PathCost = Constants.NO_COST;
            var startNode = new AStarNode(start, 0, heuristic, CellStatus.Open);

            openList.Enqueue(start, startNode.F);
            openListLookup[start] = startNode;

            while (openList.Count != 0)
            {
                var nodeId = openList.Dequeue();
                var node = openListLookup[nodeId].Value;

                //var pos = ((HTiling) map).Graph.GetNodeInfo(nodeId).Position;
                if (node.Status == CellStatus.Closed)
                    continue;

                openListLookup[nodeId] = new AStarNode(node.Parent, node.G, node.H, CellStatus.Closed);

                if (isGoal(nodeId))
                {
                    ReconstructPath(nodeId);
                    return;
                }

                var successors = map.GetNeighbours(nodeId, Constants.NO_NODE);
                foreach (var successor in successors)
                {
                    var newg = node.G + successor.Cost;
                    var successorTarget = successor.Target;
                    var targetAStarNode = openListLookup[successorTarget];
                    if (targetAStarNode.HasValue)
                    {
                        if (targetAStarNode.Value.Status == CellStatus.Closed || newg >= targetAStarNode.Value.G)
                            continue;

                        targetAStarNode = new AStarNode(nodeId, newg, targetAStarNode.Value.H, CellStatus.Open);
                        openListLookup[successorTarget] = targetAStarNode;
                        openList.UpdatePriority(successorTarget, targetAStarNode.Value.F);
                    }
                    else
                    {
                        var newHeuristic = getHeuristic(successorTarget);
                        var newAStarNode = new AStarNode(nodeId, newg, newHeuristic, CellStatus.Open);
                        openList.Enqueue(successorTarget, newAStarNode.F);
                        openListLookup[successorTarget] = newAStarNode;
                    }
                }
            }
        }
    }
}
