using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HPASharp.Interfaces;

namespace HPASharp.Implementations
{

	/** A {@code PathFinder} that can find a {@link GraphPath path} from one node in an arbitrary {@link Graph graph} to a goal node
	 * based on information provided by that graph.
	 * <p>
	 * A fully implemented path finder can perform both interruptible and non-interruptible searches. If a specific path finder is not
	 * able to perform one of the two type of search then the corresponding method should throw an
	 * {@link UnsupportedOperationException}.
	 * 
	 * @param <N> Type of node
	 * 
	 * @author davebaol */
	public interface IPathFinder<N>
	{

		/** Performs a non-interruptible search, trying to find a path made up of connections from the start node to the goal node
		 * attempting to honor costs provided by the graph.
		 * 
		 * @param startNode the start node
		 * @param endNode the end node
		 * @param heuristic the heuristic function
		 * @param outPath the output path that will only be filled if a path is found, otherwise it won't get touched.
		 * @return {@code true} if a path was found; {@code false} otherwise. */
		bool searchConnectionPath(N startNode, N endNode, IHeuristic<N> heuristic, GraphPath<IConnection<N>> outPath);

		/** Performs a non-interruptible search, trying to find a path made up of nodes from the start node to the goal node attempting
		 * to honor costs provided by the graph.
		 * 
		 * @param startNode the start node
		 * @param endNode the end node
		 * @param heuristic the heuristic function
		 * @param outPath the output path that will only be filled if a path is found, otherwise it won't get touched.
		 * @return {@code true} if a path was found; {@code false} otherwise. */
		bool searchNodePath(N startNode, N endNode, IHeuristic<N> heuristic, GraphPath<N> outPath);

		/** Performs an interruptible search, trying to find a path made up of nodes from the start node to the goal node attempting to
		 * honor costs provided by the graph.
		 * 
		 * @param request the pathfinding request
		 * @param timeToRun the time in nanoseconds that can be used to advance the search
		 * @return {@code true} if the search has finished; {@code false} otherwise. */
		bool search(PathFinderRequest<N> request, long timeToRun);
	}
}
