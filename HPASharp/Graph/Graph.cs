using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public class Graph<NODEINFO, EDGEINFO>
    {
        public class Edge
        {
            public int TargetNodeId { get; set; }
            public EDGEINFO Info { get; set; }

            public Edge(int targetNodeId, EDGEINFO info)
            {
                TargetNodeId = targetNodeId;
                Info = info;
            }
        }

        public class Node
        {
            public int NodeId { get; set; }
            public NODEINFO Info { get; set; }
            public List<Edge> Edges { get; set; }

            public Node(int nodeId, NODEINFO info)
            {
                NodeId = nodeId;
                Info = info;
                this.Edges = new List<Edge>();
            }

            public void RemoveOutEdge(int targetNodeId)
            {
                this.Edges.RemoveAll(e => e.TargetNodeId == targetNodeId);
            }
        }

        public List<Node> Nodes { get; set; }

        public Graph()
        {
            Nodes = new List<Node>();
        } 

        public void AddNode(int nodeId, NODEINFO info)
        {
            var size = nodeId + 1;
            if (Nodes.Count < size)
                Nodes.Add(new Node(nodeId, info));
            else
                Nodes[nodeId] = new Node(nodeId, info);
        }

        #region Graph updating

        public void AddEdge(int sourceNodeId, int targetNodeId,
                        EDGEINFO info)
        {
            Nodes[sourceNodeId].Edges.Add(new Edge(targetNodeId, info));
        }
        
        public void RemoveOutEdge(int sourceNodeId, int targetNodeId)
        {
            Nodes[sourceNodeId].Edges.RemoveAll(e => e.TargetNodeId == targetNodeId);
        }
        
        public void RemoveNodeEdges(int nodeId)
        {
            foreach (var edge in Nodes[nodeId].Edges)
            {
                Nodes[edge.TargetNodeId].RemoveOutEdge(nodeId);
            }

            Nodes[nodeId].Edges.Clear();
        }

        public void RemoveLastNode()
        {
            Nodes.RemoveAt(Nodes.Count - 1);
        }

        public void Clear()
        {
            Nodes.Clear();
        }

        #endregion

        public Node GetNode(int nodeId)
        {
            return Nodes[nodeId];
        }

        public NODEINFO GetNodeInfo(int nodeId)
        {
            return GetNode(nodeId).Info;
        }
        
        public List<Edge> GetOutEdges(int nodeId)
        {
            return Nodes[nodeId].Edges;
        }
    }
}