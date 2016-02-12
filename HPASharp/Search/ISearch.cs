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
            return g1.CompareTo(g2); //(g1 > g2);
        }

        public AStarNode(int nodeId, AStarNode parent, int g, int h)
        {
            NodeId = nodeId;
            Parent = parent;
            G = g;
            H = h;
        }
        
        public int NodeId { get; set; }
        public AStarNode Parent { get; set; }
        public int H { get; set; }
        public int G { get; set; }
        public int F { get { return G + H; } }
    }

    public class AStar
    {
        private IMap map;

        private int target;

        public int PathCost { get; set; }

        private HashSet<int> closedList;
        private SortedSet<AStarNode> openList;

        public AStar()
        {
            closedList = new HashSet<int>();
            openList = new SortedSet<AStarNode>();
        }

        public List<int> Path { get; set; }

        private int nodesExpanded;
        private int nodesVisited;

        public bool FindPath(IMap map, int start, int target)
        {
            this.nodesExpanded = 0;
            this.nodesVisited = 0;
            this.map = map;
            this.target = target;
            Path = new List<int>();
            findPathAstar(start);
            return true;
        }

        public AStarNode findNodeInOpenList(int nodeId)
        {
            AStarNode result = null;
            result = openList.FirstOrDefault(n => n.NodeId == nodeId);
            return result;
        }

        private void finishSearch(int start, AStarNode node)
        {
            closedList.Add(node.NodeId);
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

        public void findPathAstar(int start)
        {
            var heuristic = map.GetHeuristic(start, target);
            PathCost = Constants.NO_COST;
            var startNode = new AStarNode(start, null, 0, heuristic);
            openList.Add(startNode);
            while (openList.Count != 0)
            {
                var node = openList.Min;
                openList.Remove(node);

                if (closedList.Contains(node.NodeId))
                    continue;

                if (node.NodeId == target)
                {
                    finishSearch(start, node);
                    return;
                }
                
                var successors = map.GetNeighbours(node.NodeId, Constants.NO_NODE);
                foreach (var successor in successors)
                {
                    var newg = node.G + successor.Cost;
                    var successorTarget = successor.Target;
                    var targetAStarNode = findNodeInOpenList(successorTarget);
                    if (targetAStarNode != null)
                    {
                        if (newg >= targetAStarNode.G)
                            continue;

                        openList.RemoveWhere(n => n.NodeId == successorTarget);
                    }

                    var newHeuristic = map.GetHeuristic(successorTarget, this.target);
                    var newAStarNode = new AStarNode(successorTarget, node, newg, newHeuristic);
                    openList.Add(newAStarNode);
                }

                closedList.Add(node.NodeId);
            }
        }
    }
}
