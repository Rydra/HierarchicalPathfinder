using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Implementations
{
	/** A request for interruptible path smoothing.
 * 
 * @param <N> Type of node
 * @param <V> Type of vector, either 2D or 3D, implementing the {@link Vector} interface
 * 
 * @author davebaol */
	public class PathSmootherRequest<N, V extends Vector<V>> {

	public bool isNew;
	public int outputIndex;
	public int inputIndex;
	public SmoothableGraphPath<N, V> path;

	/** Creates an empty {@code PathSmootherRequest} */
	public PathSmootherRequest()
	{
		isNew = true;
	}

	public void refresh(SmoothableGraphPath<N, V> path)
	{
		this.path = path;
		this.isNew = true;
	}

}

}
