using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Interfaces;

namespace HPASharp.Implementations
{
	/** A {@code DefaultConnection} is a {@link IConnection} whose cost is 1.
 * 
 * @param <N> Type of node
 * 
 * @author davebaol */
	public class DefaultConnection<N> : IConnection<N> {

	protected N fromNode;
	protected N toNode;

	public DefaultConnection(N fromNode, N toNode)
	{
		this.fromNode = fromNode;
		this.toNode = toNode;
	}
		
	public float getCost()
	{
		return 1f;
	}
		
	public N getFromNode()
	{
		return fromNode;
	}
		
	public N getToNode()
	{
		return toNode;
	}

}

}
