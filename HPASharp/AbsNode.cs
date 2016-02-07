using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    // implements edges in the abstract graph
    public class AbsNode
    {
        public int ClusterId { get; set; }
        // The id is useful to have indexed arrays and look up based on them
        public int Id { get; set; }
        public Position Position { get; set; }
        public int OriginNodeId { get; set; }
        public int LocalIdxCluster { get; set; }
        public int Level { get; set; }

        public AbsNode(int id, int clusterId, Position position, int originNodeId)
        {
            Level = -1;
            ClusterId = clusterId;
            Id = id;
            Position = position;
            this.OriginNodeId = originNodeId;
        }
    }
}
