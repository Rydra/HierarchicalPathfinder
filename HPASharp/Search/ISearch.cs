using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Search
{
    public class AStar
    {
        public int PathCost { get; set; }

        public AStar(bool i)
        {
            
        }

        public void FindPath(HTiling hTiling, int absNodeId, int i)
        {
        }
    }

    public interface ISearch
    {
        void reset(AStar aStar);

        void findPath(object tiling, int target, int start);

        List<int> getPath();

        int getPathCost();

        bool checkPathExists(object tiling, int start, int target);
    }

    public class SearchImp : ISearch
    {
        public void reset(AStar aStar)
        {
            
        }

        public void findPath(object tiling, int target, int start)
        {
            
        }

        public List<int> getPath()
        {
            return null;
        }

        public int getPathCost()
        {
            return 0;
        }

        public bool checkPathExists(object tiling, int start, int target)
        {
            return true;
        }
    }
}
