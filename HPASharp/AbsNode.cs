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
        public int Id { get; set; }
        public Position Position { get; set; }
        public int CenterId { get; set; }
        public int LocalIdxCluster { get; set; }
        public int Level { get; set; }

        public AbsNode(int id, int clusterId, Position position, int centerId)
        {
            Level = -1;
            ClusterId = clusterId;
            Id = id;
            Position = position;
            this.CenterId = centerId;
        }
    }
}
