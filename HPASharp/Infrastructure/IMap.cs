using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    /** Interface to search environment. */
    public interface IMap
    {
        int NrNodes { get; }
        
        /** Generate successor nodes for the search.
            @param lastNodeId
            Can be used to prune nodes,
            (is set to NO_NODE in Search::checkPathExists).
        */
        IEnumerable<Neighbour> GetNeighbours(int nodeId);

        int GetHeuristic(int start, int target);
    }
}
