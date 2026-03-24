using System.Collections.Generic;
using System.Linq;

using RailCAD.CadInterface.Tools;
using RailCAD.Common;

namespace RailCAD.Models.Geometry
{
    public enum RC_BOD
    {
        ZAKLAD,  // basic point - ZAKLAD is not a type of fixed point but a type of a terrain model entity in general (ZAKLAD = point and SPOJNICE = line segment)!
        DNO,   // singularities
        SEDLO,
        ROH,
        VRCHOL,
    }

    public class RCPoint : IRCEntity
    {
        public RCPoint(Point3d point, int number, string handle = "0", RC_BOD type = RC_BOD.ZAKLAD, string tag = "")
        {
            Point3d = point;
            this.Normal = new Vector3d(0, 0, 1);
            this.IsValid = false;
            
            this.Number = number;  // used for equality comparision in terrain model
            this.Handle = handle;
            this.Type = type;
            this.Tag = tag;

            this.NeighborsHandles = new HashSet<string>();
            this.Triangles = new List<RCTriangle>();
            this.Lines = new List<RCLine>();

            this.IsHranice = false;
        }

        public Point3d Point3d { get; private set; }

        public Vector3d Normal { get; private set; }

        public int Number { get; private set; }
        public string Tag { get; private set; }
        public string Handle { get; private set; }

        // links to associated entites (labels and block)
        public string Label { get; private set; } // block label
        public string Sign { get; private set; } // "sign" block
        public string TextNumber { get; private set; } // text lablels
        public string TextTag { get; private set; }
        public string TextHeight1 { get; private set; }
        public string TextHeight2 { get; private set; }

        // terrain model
        public HashSet<string> NeighborsHandles { get; private set; } // neighbor points

        public RC_BOD Type { get; private set; }
        
        public IList<RCTriangle> Triangles { get; private set; }
        
        public IList<RCLine> Lines { get; private set; }

        public bool IsHranice { get; private set; }

        public bool IsValid { get; private set; }

        public Point2d Point2d
        {
            get
            {
                return Point3d.ToPoint2d();
            }
        }

        /// <summary>
        /// Equality test. Uses handles if at least one point has it, otherwise it uses numbers.
        /// </summary>
        public bool Equals(RCPoint other)
        {
            if (this.Handle.IsNullHandle() && other.Handle.IsNullHandle())
                return this.Number == other.Number;
            else
                return this.Handle == other.Handle;
        }
        
        public IList<RCPoint> GetNeighbors(RC_SPOJNICE type = RC_SPOJNICE.ALL_TYPES)
        {
            var neighbors = new List<RCPoint>(this.Lines.Count);
            foreach (var line in this.Lines)
            {
                if (type.HasFlag(line.Type))
                {
                    RCPoint neighbor = this.Equals(line.Pt1) ? line.Pt2 : line.Pt1;
                    neighbors.Add(neighbor);
                }
            }

            return neighbors;
        }

        public void SetHandle(string handle)
        {
            if (!handle.IsNullHandle())
                this.Handle = handle;
        }

        public void SetNormal(Vector3d normal)
        {
            this.Normal = normal;
            this.IsValid = true;
        }

        public void SetIsHranice()
        {
            this.IsHranice = true;
        }

        public void SetAssociatedEntities(string label, string sign, string textNumber, string textTag, string textHeight1, string textHeight2)
        {
            if (!label.IsNullHandle())
                this.Label = label;
            if (!sign.IsNullHandle())
                this.Sign = sign;
            if (!textNumber.IsNullHandle())
                this.TextNumber = textNumber;
            if (!textTag.IsNullHandle())
                this.TextTag = textTag;
            if (!textHeight1.IsNullHandle())
                this.TextHeight1 = textHeight1;
            if (!textHeight2.IsNullHandle())
                this.TextHeight2 = textHeight2;
        }

        /// <summary>
        /// Creates alist of points numbers sorted in ascending order.
        /// </summary>
        public static IList<int> GetPointsNumbers(IEnumerable<RCPoint> points)
        {
            if (points == null || points.Count() == 0) return null;

            return points.Select(p => p.Number).OrderBy(n => n).ToList();
        }

        public void SetNeighborsHandles(HashSet<string> neighbors)
        {
            this.NeighborsHandles = neighbors;
        }

        public override string ToString() => $"{Number}: {Point3d} ({Tag})";

        public object WriteToXData()
        {
            return this.WriteXData();
        }

        public static IDictionary<string, RCPoint> HandleMap(IList<RCPoint> points)
        {
            if (points != null)
            {
                IDictionary<string, RCPoint> handleMap = points.ToDictionary( pt => pt.Handle, pt => pt);
                return handleMap;
            }
            return null;
        }

        public static IDictionary<int, HashSet<string>> GetNeighborsHandles(HashSet<RCPoint> points)
        {
            if (points != null)
            {
                IDictionary<int, HashSet<string>> neighborsHandles = points.ToDictionary(pt => pt.Number, pt => pt.NeighborsHandles);
                return neighborsHandles;
            }
            return null;
        }

        public RCLine CommonLine(RCPoint other)
        {
            if (this != null && other != null)
            {
                return Lines.FirstOrDefault(line1 => other.Lines.Any(line2 => line1.Equals(line2)));
            }
            return null;
        }

        public RCTriangle CommonTriangle(RCPoint other)
        {
            if (this != null && other != null)
            {
                return Triangles.FirstOrDefault(line1 => other.Triangles.Any(line2 => line1.Equals(line2)));
            }
            return null;
        }
    }
}