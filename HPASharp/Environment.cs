using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
        /** Interface to search environment. */

    public interface Environment
    {
        int getHeuristic(int start, int target);
        
        int getMinCost();

        int NrAbsNodes { get; set; }
        
        /** Generate successor nodes for the search.
            @param lastNodeId
            Can be used to prune nodes,
            (is set to NO_NODE in Search::checkPathExists).
        */
        List<Successor> getSuccessors(int nodeId, int lastNodeId);
    }
}
