using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Search
{
    public class HierarchicalSearch
    {
        public List<PathNode> DoHierarchicalSearch(HierarchicalMap map, int startNodeId, int targetNodeId, int maxSearchLevel, int maxPathsToRefine = int.MaxValue)
        {
            var path = this.PerformSearch(map, startNodeId, targetNodeId, maxSearchLevel, true)
                .Select(n => new PathNode(n, maxSearchLevel)).ToList();

            if (path.Count == 0) return path;

            for (var level = maxSearchLevel; level > 1; level--)
                path = this.RefineAbstractPath(map, path, level, maxPathsToRefine);

            return path;
        }

        public List<int> PerformSearch(HierarchicalMap map, int startNodeId, int targetNodeId, int level, bool mainSearch)
        {
            var search = new AStar();
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
                // No path found
                return new List<int>();
            }

            var result = path.PathNodes;
            result.Reverse();
            return result;
        }

        /// <summary>
        /// Refines all the nodes that belong to a certain level to a lower level
        /// </summary>
        public List<PathNode> RefineAbstractPath(HierarchicalMap map, List<PathNode> path, int level, int maxPathsToRefine = int.MaxValue)
        {
            var result = new List<PathNode>();
            var calculatedPaths = 0;

            for (var i = 0; i < path.Count - 1; i++)
            {
                // if the two consecutive points belong to the same cluster, compute the path between them and
                // add the resulting nodes of that path to the list
                if (path[i].Level == path[i + 1].Level && path[i].Level == level &&
                    map.BelongToSameCluster(path[i].Id, path[i + 1].Id, level) && calculatedPaths < maxPathsToRefine)
                {
                    var tmp = this.PerformSearch(map, path[i].Id, path[i + 1].Id, level - 1, false)
                        .Select(n => new PathNode(n, level - 1))
                        .ToList();
                    result.AddRange(tmp);

                    calculatedPaths++;

                    // When we have calculated a path between 2 nodes, the next path in the search
                    // will be an interEdge node. We can safely skip it
                    i++;
                }
                else
                    result.Add(path[i]);
            }

            // make sure last elem is added
            if (result[result.Count - 1].Id != path[path.Count - 1].Id)
                result.Add(path[path.Count - 1]);

            return result;
        }

        public List<PathNode> AbstractPathToLowLevelPath(HierarchicalMap map, List<PathNode> absPath, int width, int maxPathsToCalculate = int.MaxValue)
        {
            var result = new List<PathNode>(absPath.Count * 10);
            if (absPath.Count == 0) return result;

            var calculatedPaths = 0;
            var lastAbsNodeId = absPath[0].Id;

            for (var j = 1; j < absPath.Count; j++)
            {
                var currentAbsNodeId = absPath[j].Id;
                var currentNodeInfo = map.AbstractGraph.GetNodeInfo(currentAbsNodeId);
                var lastNodeInfo = map.AbstractGraph.GetNodeInfo(lastAbsNodeId);

                // We cannot compute a low level path from a level which is higher than lvl 1
                // (obvious...) therefore, ignore any non-refined path
                if (absPath[j].Level > 1)
                {
                    result.Add(absPath[j]);
                    continue;
                }

                var eClusterId = currentNodeInfo.ClusterId;
                var leClusterId = lastNodeInfo.ClusterId;

                if (eClusterId == leClusterId && calculatedPaths < maxPathsToCalculate)
                {
                    // insert the local solution into the global one
                    // var cluster = map.GetCluster(eClusterId);
                    //var localpos1 = cluster.GetLocalPosition(lastNodeInfo.LocalEntranceId);
                    //var localpos2 = cluster.GetLocalPosition(currentNodeInfo.LocalEntranceId);
                    if (lastNodeInfo.Id != currentNodeInfo.Id)
                    {
						var cluster = map.GetCluster(eClusterId);
						var localPath = cluster.GetPath(lastNodeInfo.Id, currentNodeInfo.Id)
                            .Select(
                                localId =>
                                {
                                    int localPoint = LocalClusterId2GlobalId(localId, cluster, width);
                                    return new PathNode(localPoint, 0);
                                });

                        result.AddRange(localPath);

                        calculatedPaths++;
                    }
                }
                else
                {
                    var lastVal = lastNodeInfo.CenterId;
                    var currentVal = currentNodeInfo.CenterId;
                    if (result[result.Count - 1].Id != lastVal)
                        result.Add(new PathNode(lastVal, 0));

                    result.Add(new PathNode(currentVal, 0));
                }

                lastAbsNodeId = currentAbsNodeId;
            }

            return result;
        }

        private static int LocalClusterId2GlobalId(int localId, Cluster cluster, int width)
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
