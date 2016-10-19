using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Interfaces
{
	/** A connection between two nodes of the {@link Graph}. The connection has a non-negative cost that often represents time or
	 * distance. However, the cost can be anything you want, for instance a combination of time, distance, and other factors.
	 * 
	 * @param <N> Type of node
	 * 
	 * @author davebaol */
	public interface IConnection<N>
	{

		/** Returns the non-negative cost of this connection */
		float getCost();

		/** Returns the node that this connection came from */
		N getFromNode();

		/** Returns the node that this connection leads to */
		N getToNode();

	}

}
