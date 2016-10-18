using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPASharp.Factories
{
    /// <summary>
    /// Represents an entrance point between 2 clusters
    /// </summary>
    public class Entrance
    {
        public int Id { get; set; }
        public int Cluster1Id { get; set; }
        public int Cluster2Id { get; set; }

        /// <summary>
        /// This is the id of the lvl 1 abstract node of one of the entrance points.
        /// TODO: This is horrible, why lvl 1? Even I can't understand!
        /// </summary>
		public int Coord1Id { get; set; }
        public int Coord2Id { get; set; }
        public Orientation Orientation { get; set; }

        /// <summary>
        /// This position represents one end of the entrance
        /// </summary>
        public Position Coord1 { get; set; }

        /// <summary>
        /// This position represents the other end of the entrance
        /// </summary>
        public Position Coord2
        {
            get
            {
                int x;
                switch (Orientation)
                {
                    case Orientation.HORIZONTAL:
                    case Orientation.HDIAG2:
                        x = this.Coord1.X;
                        break;
                    case Orientation.VERTICAL:
                    case Orientation.VDIAG2:
                    case Orientation.VDIAG1:
                    case Orientation.HDIAG1:
                        x = this.Coord1.X + 1;
                        break;
                    default:
                        //assert(false);
                        x = -1;
                        break;
                }

                int y;
                switch (Orientation)
                {
                    case Orientation.HORIZONTAL:
                    case Orientation.HDIAG1:
                    case Orientation.HDIAG2:
                    case Orientation.VDIAG1:
                        y = this.Coord1.Y + 1;
                        break;
                    case Orientation.VERTICAL:
                    case Orientation.VDIAG2:
                        y = this.Coord1.Y;
                        break;
                    default:
                        //assert(false);
                        y = -1;
                        break;
                }

                return new Position(x, y);
            }
        }

        public Entrance(int id, int cl1Id, int cl2Id, int center1Row, int center1Col, int coord1Id, int coord2Id, Orientation orientation)
        {
            Id = id;
            Cluster1Id = cl1Id;
            Cluster2Id = cl2Id;

            int center1y, center1x;
            if (orientation == Orientation.HDIAG2)
                center1x = center1Col + 1;
            else
                center1x = center1Col;

            if (orientation == Orientation.VDIAG2)
                center1y = center1Row + 1;
            else
                center1y = center1Row;

            this.Coord1 = new Position(center1x, center1y);
            this.Coord1Id = coord1Id;
            this.Coord2Id = coord2Id;
            Orientation = orientation;
        }
    }
}
