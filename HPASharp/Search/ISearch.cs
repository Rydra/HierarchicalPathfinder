using System;
using System.Collections.Generic;
using System.Linq;

namespace HPASharp.Search
{
    public class AStarNode : IComparable<AStarNode>
    {
        public int CompareTo(AStarNode other)
        {
            int f1 = this.F;
            int f2 = other.F;
            if (f1 != f2) return f1.CompareTo(f2); //(f1 < f2);
            int g1 = this.G;
            int g2 = other.G;
            if (g1 != g2) return g1.CompareTo(g2); //(g1 > g2);
            return this.NodeId.CompareTo(other.NodeId);
        }

        public AStarNode(int nodeId, AStarNode parent, int g, int h, CellStatus status)
        {
            NodeId = nodeId;
            Parent = parent;
            G = g;
            H = h;
            Status = status;
        }

        public int NodeId { get; set; }
        public AStarNode Parent { get; set; }
        public CellStatus Status { get; set; }
        public int H { get; set; }
        public int G { get; set; }
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
        
        private AStarNode[] openListLookup; 
        private SortedSet<AStarNode> openList;

        public AStar()
        {
            openList = new SortedSet<AStarNode>();
        }

        public List<int> Path { get; set; }

        public bool FindPath(IMap map, int start, int target)
        {
            this.map = map;
            this.isGoal = nodeId => nodeId == target;
            this.getHeuristic = nodeId => map.GetHeuristic(nodeId, target);
            Path = new List<int>();
            FindPathAstar(start);
            return true;
        }

        /// <summary>
        /// Finds a node in the openqueue. A faster (though more memory consuming)
        /// alternative could be use an array indexed by nodeId to assess the status of that node
        /// </summary>
        private AStarNode FindNodeInOpenQueue(int nodeId)
        {
            return openListLookup[nodeId];
        }

        private void ReconstructPath(int start, AStarNode node)
        {
            Path = new List<int>();
            PathCost = node.F;
            var currnode = node;
            while (currnode.NodeId != start)
            {
                Path.Add(currnode.NodeId);
                currnode = currnode.Parent;
            }

            Path.Add(currnode.NodeId);
        }

        private void FindPathAstar(int start)
        {
            openListLookup = new AStarNode[map.NrNodes];

            var heuristic = getHeuristic(start);
            PathCost = Constants.NO_COST;
            var startNode = new AStarNode(start, null, 0, heuristic, CellStatus.Open);
            openList.Add(startNode);
            openListLookup[start] = startNode;

            while (openList.Count != 0)
            {
                var node = openListLookup[openList.Min.NodeId];
                openList.RemoveWhere(n => n.NodeId == node.NodeId);
                //var pos = ((HTiling) map).Graph.GetNodeInfo(node.NodeId).Position;
                if (node.Status == CellStatus.Closed)
                    continue;

                if (isGoal(node.NodeId))
                {
                    ReconstructPath(start, node);
                    return;
                }

                var successors = map.GetNeighbours(node.NodeId, Constants.NO_NODE);
                foreach (var successor in successors)
                {
                    var newg = node.G + successor.Cost;
                    var successorTarget = successor.Target;
                    var targetAStarNode = FindNodeInOpenQueue(successorTarget);
                    if (targetAStarNode != null)
                    {
                        if (targetAStarNode.Status == CellStatus.Closed || newg >= targetAStarNode.G)
                            continue;

                        // NOTE: I comment this temporarily
                        openListLookup[successorTarget] = null;
                        //openList.RemoveWhere(n => n.NodeId == successorTarget);
                    }

                    var newHeuristic = getHeuristic(successorTarget);
                    var newAStarNode = new AStarNode(successorTarget, node, newg, newHeuristic, CellStatus.Open);
                    openList.Add(newAStarNode);
                    openListLookup[successorTarget] = newAStarNode;
                }

                node.Status = CellStatus.Closed;
            }
        }
    }
}
