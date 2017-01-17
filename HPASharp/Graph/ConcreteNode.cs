using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Infrastructure;

namespace HPASharp.Graph
{

    public class ConcreteNode : INode<ConcreteNode, ConcreteNodeInfo, ConcreteEdge>
    {
        public Id<ConcreteNode> NodeId { get; set; }
        public ConcreteNodeInfo Info { get; set; }
        public List<ConcreteEdge> Edges { get; set; }

        public ConcreteNode(Id<ConcreteNode> nodeId, ConcreteNodeInfo info)
        {
            NodeId = nodeId;
            Info = info;
            Edges = new List<ConcreteEdge>();
        }

        public void RemoveEdge(Id<ConcreteNode> targetNodeId)
        {
            Edges.RemoveAll(e => e.TargetNodeId == targetNodeId);
        }
    }

    public class ConcreteEdge : IEdge<ConcreteNode, ConcreteEdgeInfo>
    {
        public Id<ConcreteNode> TargetNodeId { get; set; }
        public ConcreteEdgeInfo Info { get; set; }

        public ConcreteEdge(Id<ConcreteNode> targetNodeId, ConcreteEdgeInfo info)
        {
            TargetNodeId = targetNodeId;
            Info = info;
        }
    }

    public class ConcreteEdgeInfo
    {
        public ConcreteEdgeInfo(int cost)
        {
            Cost = cost;
        }

        public int Cost { get; set; }
    }

    public class ConcreteNodeInfo
    {
        public ConcreteNodeInfo(bool isObstacle, int cost, Position position)
        {
            IsObstacle = isObstacle;
            Position = position;
            Cost = cost;
        }

        public Position Position { get; set; }
        public bool IsObstacle { get; set; }
        public int Cost { get; set; }
    }
}
