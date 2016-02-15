using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp
{
    public struct Node
    {
        public int Id;

        public int Level;

        public Node(int id, int lvl)
        {
            Id = id;
            Level = lvl;
        }
    }
}
