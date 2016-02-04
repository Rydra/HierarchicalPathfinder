using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Search
{
    public class SearchUtils
    {
        Environment m_env;
        bool[] closedList;
        int m_target;

        public bool checkPathExists(Environment env, int start, int target)
        {
            m_env = env;
            m_target = target;
            closedList = new bool[env.NrAbsNodes];
            return searchPathExists(start, 0);
        }

        List<List<Successor>> m_successorStack = new List<List<Successor>>();
        private bool searchPathExists(int node, int depth)
        {
            // AdiB 21/04/2003
            if (depth > 10000)
                return false;
            if (this.closedList[node])
                return false;
            if (node == m_target)
                return true;

            this.closedList[node] = true;

            if (m_successorStack.Count < depth + 1)
                m_successorStack.Add(new List<Successor>());
            
            m_successorStack[depth] = m_env.getSuccessors(node, Constants.NO_NODE);
            int numberSuccessors = m_successorStack[depth].Count;
            for (int i = 0; i < numberSuccessors; ++i)
            {
                // Get reference on successor again, because resize could have
                // changed it.
                var successor = m_successorStack[depth][i];
                int targetNodeId = successor.Target;

                if (searchPathExists(targetNodeId, depth + 1))
                    return true;
            }

            return false;
        }
    }
}
