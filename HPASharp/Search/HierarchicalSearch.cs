using System.Collections.Generic;
using HPASharp.Graph;
using HPASharp.Infrastructure;

namespace HPASharp.Search
{
    public class HierarchicalSearch
    {
        public List<AbstractPathNode> DoHierarchicalSearch(HierarchicalMap map, Id<AbstractNode> startNodeId, Id<AbstractNode> targetNodeId, int maxSearchLevel, int maxPathsToRefine = int.MaxValue)
        {
	        var path = PerformSearch(map, startNodeId, targetNodeId, maxSearchLevel, true);

            if (path.Count == 0) return path;

            for (var level = maxSearchLevel; level > 1; level--)
                path = RefineAbstractPath(map, path, level, maxPathsToRefine);

            return path;
        }

        private List<AbstractPathNode> PerformSearch(HierarchicalMap map, Id<AbstractNode> startNodeId, Id<AbstractNode> targetNodeId, int level, bool mainSearch)
        {
            var search = new AStar<AbstractNode>();
            map.SetCurrentLevel(level);
            var nodeInfo = map.AbstractGraph.GetNodeInfo(startNodeId);
            if (mainSearch)
                map.SetCurrentClusterByPositionAndLevel(nodeInfo.Position, map.MaxLevel + 1);
            else
                map.SetCurrentClusterByPositionAndLevel(nodeInfo.Position, level + 1);

            // TODO: This could be perfectly replaced by cached paths in the clusters!
            var path = search.FindPath(map, startNodeId, targetNodeId);
            if (path.PathCost == -1)
            {
                return new List<AbstractPathNode>();
            }

            var result = new List<AbstractPathNode>(path.PathNodes.Count);
            for (int i = 0; i < result.Count; i++)
            {
                result[i] = new AbstractPathNode(path.PathNodes[i], level);
            }

            return result;
        }

        /// <summary>
        /// Refines all the nodes that belong to a certain level to a lower level
        /// </summary>
        public List<AbstractPathNode> RefineAbstractPath(HierarchicalMap map, List<AbstractPathNode> path, int level, int maxPathsToRefine = int.MaxValue)
        {
            var result = new List<AbstractPathNode>();
            var calculatedPaths = 0;

            for (var i = 0; i < path.Count - 1; i++)
            {
                // if the two consecutive points belong to the same cluster, compute the path between them and
                // add the resulting nodes of that path to the list
                if (path[i].Level == path[i + 1].Level && path[i].Level == level &&
                    map.BelongToSameCluster(path[i].Id, path[i + 1].Id, level) && calculatedPaths < maxPathsToRefine)
                {
	                var tmp = PerformSearch(map, path[i].Id, path[i + 1].Id, level - 1, false);
                    result.AddRange(tmp);

                    calculatedPaths++;

                    // When we have calculated a path between 2 nodes, the next path in the search
                    // will be an interEdge node. We can safely skip it
                    i++;
                }
                else
                    result.Add(new AbstractPathNode(path[i].Id, level - 1));
            }

            // make sure last elem is added
            if (result[result.Count - 1].Id != path[path.Count - 1].Id)
                result.Add(path[path.Count - 1]);

            return result;
        }

        public List<IPathNode> AbstractPathToLowLevelPath(HierarchicalMap map, List<AbstractPathNode> abstractPath, int mapWidth, int maxPathsToCalculate = int.MaxValue)
        {
            var result = new List<IPathNode>();
            if (abstractPath.Count == 0)
				return result;

            var calculatedPaths = 0;
            var lastAbstractNodeId = abstractPath[0].Id;

	        if (abstractPath[0].Level != 1)
	        {
		        result.Add(abstractPath[0]);
	        }
	        else
	        {
				var abstractNode = map.AbstractGraph.GetNodeInfo(lastAbstractNodeId);
				result.Add(new ConcretePathNode(abstractNode.ConcreteNodeId));
	        }

			for (var currentPoint = 1; currentPoint < abstractPath.Count; currentPoint++)
            {
                var currentAbstractNodeId = abstractPath[currentPoint].Id;
				var lastNodeInfo = map.AbstractGraph.GetNodeInfo(lastAbstractNodeId);
				var currentNodeInfo = map.AbstractGraph.GetNodeInfo(currentAbstractNodeId);
                
	            if (lastAbstractNodeId == currentAbstractNodeId)
	            {
		            continue;
	            }

                // We cannot compute a low level path from a level which is higher than lvl 1
                // (obvious...) therefore, ignore any non-refined path
                if (abstractPath[currentPoint].Level != 1)
                {
                    result.Add(abstractPath[currentPoint]);
                    continue;
                }

                var currentNodeClusterId = currentNodeInfo.ClusterId;
                var lastNodeClusterId = lastNodeInfo.ClusterId;

                if (currentNodeClusterId == lastNodeClusterId && calculatedPaths < maxPathsToCalculate)
                {
					var cluster = map.GetCluster(currentNodeClusterId);

	                var localPath = cluster.GetPath(Id<AbstractNode>.From(lastAbstractNodeId),
		                Id<AbstractNode>.From(currentAbstractNodeId));

	                var concretePath = new List<IPathNode>();
	                for (int i = 1; i < localPath.Count; i++)
	                {
						int concreteNodeId = LocalNodeId2ConcreteNodeId(localPath[i], cluster, mapWidth);
						concretePath.Add(new ConcretePathNode(Id<ConcreteNode>.From(concreteNodeId)));
					}

                    result.AddRange(concretePath);

                    calculatedPaths++;
                }
                else
                {
					// Inter-cluster edge
                    var lastConcreteNodeId = lastNodeInfo.ConcreteNodeId;
                    var currentConcreteNodeId = currentNodeInfo.ConcreteNodeId;

                    if (((ConcretePathNode)result[result.Count - 1]).Id != lastConcreteNodeId)
                        result.Add(new ConcretePathNode(lastConcreteNodeId));

					if (((ConcretePathNode)result[result.Count - 1]).Id != currentConcreteNodeId)
						result.Add(new ConcretePathNode(currentConcreteNodeId));
                }

                lastAbstractNodeId = currentAbstractNodeId;
            }

            return result;
        }

        private static int LocalNodeId2ConcreteNodeId(int localId, Cluster cluster, int width)
        {
            var localX = localId % cluster.Size.Width;
            var localY = localId / cluster.Size.Width;
            var result = (localY + cluster.Origin.Y) * width +
                         (localX + cluster.Origin.X);
            return result;
        }

        private static int GlobalId2LocalId(int globalId, Cluster cluster, int width)
        {
            var globalY = globalId / width;
            var globalX = globalId % width;
            return (globalY - cluster.Origin.Y) * cluster.Size.Width +
                (globalX - cluster.Origin.X);
        }
    }
}
