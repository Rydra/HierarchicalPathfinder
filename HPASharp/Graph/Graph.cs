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
            public List<Edge> OutEdges { get; set; }

            public Node(int nodeId, NODEINFO info)
            {
                NodeId = nodeId;
                Info = info;
                OutEdges = new List<Edge>();
            }

            public void RemoveOutEdge(int targetNodeId)
            {
                OutEdges.RemoveAll(e => e.TargetNodeId == targetNodeId);
            }
        }

        public List<Node> Nodes { get; set; }

        public Graph()
        {
            Nodes = new List<Node>();
        } 

        public void AddNode(int nodeId, NODEINFO info)
        {
            int size = nodeId + 1;
            if (Nodes.Count < size)
                Nodes.Add(new Node(nodeId, info));
            else
                Nodes[nodeId] = new Node(nodeId, info);
        }

        public void AddOutEdge(int sourceNodeId, int targetNodeId,
                        EDGEINFO info)
        {
            Nodes[sourceNodeId].OutEdges.Add(new Edge(targetNodeId, info));
        }

        public void RemoveNodeEdges(int nodeId)
        {
            foreach (var edge in Nodes[nodeId].OutEdges)
            {
                Nodes[edge.TargetNodeId].RemoveOutEdge(nodeId);
            }

            Nodes[nodeId].OutEdges.Clear();
        }

        public void RemoveLastNode()
        {
            Nodes.RemoveAt(Nodes.Count - 1);
        }

        public void RemoveOutEdge(int sourceNodeId, int targetNodeId)
        {
            Nodes[sourceNodeId].OutEdges.RemoveAll(e => e.TargetNodeId == targetNodeId);
        }

        public void Clear()
        {
            Nodes.Clear();
        }

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
            return Nodes[nodeId].OutEdges;
        }
    }
}