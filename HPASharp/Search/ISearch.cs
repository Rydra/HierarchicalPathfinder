using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Search
{


    public class AStar
    {
        private class AStarNode : IComparable<AStarNode>
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

            public int G { get; set; }

            public int F { get; set; }
        }

        private IMap map;

        private int target;

        public int PathCost { get; set; }

        public AStar(bool y)
        {
            
        }

        private List<int> path;

        private int nodesExpanded;
        private int nodesVisited;

        public bool FindPath(IMap map, int start, int target)
        {
            this.nodesExpanded = 0;
            this.nodesVisited = 0;
            this.map = map;
            this.target = target;
            path = new List<int>();
            findPathAstar(start);
            return true;
        }

        public void findPathAstar(int start)
        {
            //var maxopen = 0;
            ////    int closedsize = 0;
            //var numberNodes = this.map.NrNodes;
            //m_closed->init(numberNodes);
            //m_open.init(numberNodes);
            //int heuristic = map.GetHeuristic(start, target);
            //m_pathCost = NO_COST;
            //AStarNode startNode(start, NO_NODE, 0, heuristic);
            //m_open.insert(startNode);
            //var successors = new List<Neighbour>();
            //while (! m_open.isEmpty())
            //{
            //    AStarNode node = getBestNodeFromOpen();
            //    if (node.m_nodeId == m_target)
            //    {
            //        finishSearch(start, node);
            //        return;
            //    }
            //    ++this.nodesExpanded;
            //    m_env->getSuccessors(node.m_nodeId, NO_NODE, successors);
            //    m_branchingFactor.add(successors.size());
            //    for (vector<Environment::Successor>::const_iterator i
            //             = successors.begin(); i != successors.end(); ++i)
            //    {
            //        int newg = node.m_g + i->m_cost;
            //        int target = i->m_target;
            //        const AStarNode* targetAStarNode = findNode(target);
            //        if (targetAStarNode != 0)
            //        {
            //            if (newg >= targetAStarNode->m_g)
            //                continue;
            //            if (! m_open.remove(target))
            //                m_closed->remove(target);
            //        }

            //        int newHeuristic = map.GetHeuristic(target, m_target);
            //        AStarNode newAStarNode(target, node.m_nodeId, newg, newHeuristic);
            //        m_open.insert(newAStarNode);
            //    }
            //    //        closedsize++;
            //    m_closed->add(node);
            //    //        m_statistics.get("closed_length").add(m_closed.size());
            //    //closed->print(cout);
            //}
        }
    }

    public interface ISearch
    {
        void reset(AStar aStar);

        void findPath(object tiling, int target, int start);

        List<int> getPath();

        int getPathCost();

        bool checkPathExists(object tiling, int start, int target);
    }

    public class SearchImp : ISearch
    {
        public void reset(AStar aStar)
        {
            
        }

        public void findPath(object tiling, int target, int start)
        {
            
        }

        public List<int> getPath()
        {
            return null;
        }

        public int getPathCost()
        {
            return 0;
        }

        public bool checkPathExists(object tiling, int start, int target)
        {
            return true;
        }
    }
}
