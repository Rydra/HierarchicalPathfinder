using System;
using System.Collections.Generic;
using HPASharp.Infrastructure;

namespace HPASharp.Graph
{

    public class AbstractNode : INode<AbstractNode, AbstractNodeInfo, AbstractEdge>
    {
        public Id<AbstractNode> NodeId { get; set; }
        public AbstractNodeInfo Info { get; set; }
        public List<AbstractEdge> Edges { get; set; }

        public AbstractNode(Id<AbstractNode> nodeId, AbstractNodeInfo info)
        {
            NodeId = nodeId;
            Info = info;
            Edges = new List<AbstractEdge>();
        }

        public void RemoveEdge(Id<AbstractNode> targetNodeId)
        {
            Edges.RemoveAll(e => e.TargetNodeId == targetNodeId);
        }
    }

    public class AbstractEdge : IEdge<AbstractNode, AbstractEdgeInfo>
    {
        public Id<AbstractNode> TargetNodeId { get; set; }
        public AbstractEdgeInfo Info { get; set; }

        public AbstractEdge(Id<AbstractNode> targetNodeId, AbstractEdgeInfo info)
        {
            TargetNodeId = targetNodeId;
            Info = info;
        }
    }
    
    public class AbstractEdgeInfo
    {
        public int Cost { get; set; }
        public int Level { get; set; }
        public bool IsInterEdge { get; set; }

        public AbstractEdgeInfo(int cost, int level = 1, bool inter = true)
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
    public class AbstractNodeInfo
    {
        public Id<AbstractNode> Id { get; set; }
        public Position Position { get; set; }
        public int ClusterId { get; set; }
        public Id<ConcreteNode> ConcreteNodeId { get; set; }
        public int Level { get; set; }

        public AbstractNodeInfo(Id<AbstractNode> id, int level, int clId,
                    Position position, Id<ConcreteNode> concreteNodeId)
        {
            Id = id;
            Level = level;
            ClusterId = clId;
            Position = position;
            ConcreteNodeId = concreteNodeId;
        }

        public void PrintInfo()
        {
            Console.Write("id: " + Id);
            Console.Write("; level: " + Level);
            Console.Write("; cluster: " + ClusterId);
            Console.Write("; row: " + Position.Y);
            Console.Write("; col: " + Position.X);
            Console.Write("; center: " + ConcreteNodeId);
            Console.WriteLine();
        }
    }

}
