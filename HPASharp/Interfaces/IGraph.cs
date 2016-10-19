using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Interfaces
{
	/** A graph is a collection of nodes, each one having a collection of outgoing {@link Connection connections}.
 * 
 * @param <N> Type of node
 * 
 * @author davebaol */
	public interface IGraph<N>
	{

		/** Returns the connections outgoing from the given node.
		 * @param fromNode the node whose outgoing connections will be returned
		 * @return the array of connections outgoing from the given node. */
		IList<IConnection<N>> getConnections(N fromNode);
	}

}
