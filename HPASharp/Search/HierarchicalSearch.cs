using System.Collections.Generic;
using System.Linq;
using HPASharp.Graph;
using HPASharp.Infrastructure;

namespace HPASharp.Search
{
    public class HierarchicalSearch
    {
        public List<AbstractPathNode> DoHierarchicalSearch(HierarchicalMap map, Id<AbstractNode> startNodeId, Id<AbstractNode> targetNodeId, int maxSearchLevel, int maxPathsToRefine = int.MaxValue)
        {
	        List<AbstractPathNode> path = GetPath(map, startNodeId, targetNodeId, maxSearchLevel, true);

            if (path.Count == 0)
				return path;

            for (var level = maxSearchLevel; level > 1; level--)
                path = RefineAbstractPath(map, path, level, maxPathsToRefine);

            return path;
        }

        private List<AbstractPathNode> GetPath(HierarchicalMap map, Id<AbstractNode> startNodeId, Id<AbstractNode> targetNodeId, int level, bool mainSearch)
        {
            map.SetCurrentLevelForSearches(level);
            var nodeInfo = map.AbstractGraph.GetNodeInfo(startNodeId);

            // TODO: This could be perfectly replaced by cached paths in the clusters!
	        Path<AbstractNode> path;
	        if (!mainSearch)
	        {
                map.SetCurrentClusterByPositionAndLevel(nodeInfo.Position, level + 1);
                var edgeInfo = map.AbstractGraph.GetEdges(startNodeId)[targetNodeId].Info;
				path = new Path<AbstractNode>(edgeInfo.InnerLowerLevelPath, edgeInfo.Cost);
			}
	        else
	        {
                map.SetAllMapAsCurrentCluster();
		        var search = new AStar<AbstractNode>(map, startNodeId, targetNodeId);
		        path = search.FindPath();
	        }

	        if (path.PathCost == -1)
            {
                return new List<AbstractPathNode>();
            }

            var result = new List<AbstractPathNode>(path.PathNodes.Count);
            foreach (Id<AbstractNode> abstractNodeId in path.PathNodes)
            {
                result.Add(new AbstractPathNode(abstractNodeId, level));
            }

            return result;
        }
        
        public List<AbstractPathNode> RefineAbstractPath(HierarchicalMap map, List<AbstractPathNode> path, int level, int maxPathsToRefine = int.MaxValue)
        {
            var refinedAbstractPath = new List<AbstractPathNode>();
            var calculatedPaths = 0;

            if (path.Count == 0)
                return refinedAbstractPath;

            refinedAbstractPath.Add(new AbstractPathNode(path[0].Id, level - 1));
            for (var i = 1; i < path.Count; i++)
            {
                if (path[i].Level == level && path[i].Level == path[i - 1].Level &&
                    map.BelongToSameCluster(path[i].Id, path[i - 1].Id, level) && calculatedPaths < maxPathsToRefine)
                {
                    var interNodePath = GetPath(map, path[i - 1].Id, path[i].Id, level - 1, false);
                    for (int j = 1; j < interNodePath.Count; j++)
                    {
                        refinedAbstractPath.Add(interNodePath[j]);
                    }

                    calculatedPaths++;
                }
                else
                    refinedAbstractPath.Add(new AbstractPathNode(path[i].Id, level - 1));
            }
            
            return refinedAbstractPath;
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

	                var localPath = cluster.GetPath(lastAbstractNodeId, currentAbstractNodeId);

	                var concretePath = new List<IPathNode>();
	                for (int i = 1; i < localPath.Count; i++)
	                {
						int concreteNodeId = LocalNodeId2ConcreteNodeId(localPath[i].IdValue, cluster, mapWidth);
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
