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
        int GetHeuristic(int start, int target);
        
        int GetMinCost();

        int NrAbsNodes { get; set; }
        
        /** Generate successor nodes for the search.
            @param lastNodeId
            Can be used to prune nodes,
            (is set to NO_NODE in Search::checkPathExists).
        */
        List<Neighbour> getSuccessors(int nodeId, int lastNodeId);
    }
}
