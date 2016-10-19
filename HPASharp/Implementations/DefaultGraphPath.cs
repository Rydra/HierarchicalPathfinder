using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Interfaces;

namespace HPASharp.Implementations
{

	/** Default implementation of a {@link GraphPath} that uses an internal {@link Array} to store nodes or connections.
	 * 
	 * @param <N> Type of node
	 * 
	 * @author davebaol */
	public class DefaultGraphPath<N> : IGraphPath<N> {
	public List<N> nodes;

	/** Creates a {@code DefaultGraphPath} with no nodes. */
	public DefaultGraphPath() : this(new List<N>())
	{
		
	}

	/** Creates a {@code DefaultGraphPath} with the given capacity and no nodes. */
	public DefaultGraphPath(int capacity) : this(new List<N>(capacity))
	{
		
	}

	/** Creates a {@code DefaultGraphPath} with the given nodes. */
	public DefaultGraphPath(List<N> nodes)
	{
		this.nodes = nodes;
	}
		
	public void clear()
	{
		nodes.Clear();
	}
		
	public int getCount()
	{
		return nodes.Count;
	}
		
	public void add(N node)
	{
		nodes.Add(node);
	}
		
	public N get(int index)
	{
		return nodes[index];
	}
		
	public void reverse()
	{
		nodes.Reverse();
	}
		

		public IEnumerator<N> GetEnumerator()
		{
			return nodes.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
