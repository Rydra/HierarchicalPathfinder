using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public class LocalEntrance
    {
        public int Id { get; set; } // id of the global abstract node
        public int AbsNodeId { get; set; }
        public Position RelativePos { get; set; } // Relative position of entrance inside cluster
        public int CenterCol { get; set; } // center col in local coordinates
        public int EntranceLocalIdx { get; set; } // local id

        public LocalEntrance(int nodeId, int absNodeId, int localIdx, Position relativePosition)
        {
            Id = nodeId;
            AbsNodeId = absNodeId;
            EntranceLocalIdx = localIdx;
            RelativePos = relativePosition;
        }
    }
}
