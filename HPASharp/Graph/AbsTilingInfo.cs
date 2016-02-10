using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    // implements edges in the abstract graph
    public class AbsTilingEdgeInfo
    {
        public int Cost { get; set; }
        public int Level { get; set; }
        public bool IsInterEdge { get; set; }

        public AbsTilingEdgeInfo(int cost, int level = 1, bool inter = true)
        {
            Cost = cost;
            Level = level;
            IsInterEdge = inter;
        }
        public override string ToString()
        {
            return ("cost: " + Cost + "; level: " + Level + "; inter: " + IsInterEdge);
        }

        public void PrintInfo()
        {
            Console.WriteLine(this.ToString());
        }
    }

    // implements nodes in the abstract graph
    public class AbsTilingNodeInfo
    {
        public int Id { get; set; }
        public Position Position { get; set; }
        public int ClusterId { get; set; }
        public int CenterId { get; set; }
        public int Level { get; set; }
        public int LocalIdxCluster { get; set; }
        
        public AbsTilingNodeInfo(int id, int level, int clId,
                    Position position, int centerId,
                    int localIdxCluster)
        {
            Id = id;
            Level = level;
            ClusterId = clId;
            Position = position;
            CenterId = centerId;
            LocalIdxCluster = localIdxCluster;
        }

        public void PrintInfo()
        {
            Console.Write("id: " + Id);
            Console.Write("; level: " + Level);
            Console.Write("; cluster: " + ClusterId);
            Console.Write("; row: " + Position.Y);
            Console.Write("; col: " + Position.X);
            Console.Write("; center: " + CenterId);
            Console.Write("; local idx: " + LocalIdxCluster);
            Console.WriteLine();
        }
    }
}
