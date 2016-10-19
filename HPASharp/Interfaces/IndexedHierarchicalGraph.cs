using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Interfaces
{
	/** A hierarchical graph for the {@link IndexedAStarPathFinder}.
 * 
 * @param <N> Type of node
 * 
 * @author davebaol */
	public abstract class IndexedHierarchicalGraph<N> : IIndexedGraph<N>, IHierarchicalGraph<N>
	{

		protected int levelCount;
		protected int level;

		/** Creates an {@code IndexedHierarchicalGraph} with the given number of levels. */
		public IndexedHierarchicalGraph(int levelCount)
		{
			this.levelCount = levelCount;
			this.level = 0;
		}
	
		public int getLevelCount()
		{
			return levelCount;
		}
		
		public void setLevel(int level)
		{
			this.level = level;
		}
		
		public abstract N convertNodeBetweenLevels(int inputLevel, N node, int outputLevel);

	}
}
