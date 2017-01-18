using System;
using System.Collections.Generic;
using HPASharp.Infrastructure;

namespace HPASharp.Graph
{
	public interface INode<TId, TInfo, TEdge>
	{
		Id<TId> NodeId { get; set; }
		TInfo Info { get; set; }
		IDictionary<Id<TId>, TEdge> Edges { get; set; }
        void RemoveEdge(Id<TId> targetNodeId);
		void AddEdge(TEdge targetNodeId);
	}

	public interface IEdge<TNode, TEdgeInfo>
	{
		Id<TNode> TargetNodeId { get; set; }
		TEdgeInfo Info { get; set; }
	}

	/// <summary>
	/// A graph is a set of nodes connected with edges. Each node and edge can hold
	/// a certain amount of information, which is expressed in the templated parameters
	/// NODEINFO and EDGEINFO
	/// </summary>
	public class Graph<TNode, TNodeInfo, TEdge, TEdgeInfo> 
		where TNode : INode<TNode, TNodeInfo, TEdge>
		where TEdge : IEdge<TNode, TEdgeInfo>
	{
		// We store the nodes in a list because the main operations we use
		// in this list are additions, random accesses and very few removals (only when
		// adding or removing nodes to perform specific searches).
		// This list is implicitly indexed by the nodeId, which makes removing a random
		// Node in the list quite of a mess. We could use a dictionary to ease removals,
		// but lists and arrays are faster for random accesses, and we need performance.
        public List<TNode> Nodes { get; set; }

	    private readonly Func<Id<TNode>, TNodeInfo, TNode> _nodeCreator;
		private readonly Func<Id<TNode>, TEdgeInfo, TEdge> _edgeCreator;

		public Graph(Func<Id<TNode>, TNodeInfo, TNode> nodeCreator, Func<Id<TNode>, TEdgeInfo, TEdge> edgeCreator)
        {
            Nodes = new List<TNode>();
	        _nodeCreator = nodeCreator;
	        _edgeCreator = edgeCreator;
        } 

		/// <summary>
		/// Adds or updates a node with the provided info. A node is updated
		/// only if the nodeId provided previously existed.
		/// </summary>
        public void AddNode(Id<TNode> nodeId, TNodeInfo info)
        {
            var size = nodeId.IdValue + 1;
            if (Nodes.Count < size)
                Nodes.Add(_nodeCreator(nodeId, info));
            else
                Nodes[nodeId.IdValue] = _nodeCreator(nodeId, info);
        }

		#region AbstractGraph updating

		public void AddEdge(Id<TNode> sourceNodeId, Id<TNode> targetNodeId, TEdgeInfo info)
        {
            Nodes[sourceNodeId.IdValue].AddEdge(_edgeCreator(targetNodeId, info));
        }
        
        public void RemoveEdgesFromAndToNode(Id<TNode> nodeId)
        {
            foreach (var targetNodeId in Nodes[nodeId.IdValue].Edges.Keys)
            {
                Nodes[targetNodeId.IdValue].RemoveEdge(nodeId);
            }

            Nodes[nodeId.IdValue].Edges.Clear();
        }

        public void RemoveLastNode()
        {
            Nodes.RemoveAt(Nodes.Count - 1);
        }

        #endregion

        public TNode GetNode(Id<TNode> nodeId)
        {
            return Nodes[nodeId.IdValue];
        }

        public TNodeInfo GetNodeInfo(Id<TNode> nodeId)
        {
            return GetNode(nodeId).Info;
        }
        
        public IDictionary<Id<TNode>, TEdge> GetEdges(Id<TNode> nodeId)
        {
            return Nodes[nodeId.IdValue].Edges;
        }
    }

	public class ConcreteGraph : Graph<ConcreteNode, ConcreteNodeInfo, ConcreteEdge, ConcreteEdgeInfo>
	{
		public ConcreteGraph() : base((nodeid, info) => new ConcreteNode(nodeid, info), (nodeid, info) => new ConcreteEdge(nodeid, info))
		{
		}
	}

	public class AbstractGraph : Graph<AbstractNode, AbstractNodeInfo, AbstractEdge, AbstractEdgeInfo>
	{
		public AbstractGraph() : base((nodeid, info) => new AbstractNode(nodeid, info), (nodeid, info) => new AbstractEdge(nodeid, info))
		{
		}
	}
}