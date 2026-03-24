using System.Collections.Generic;
using System.Linq;
using HydroCAD.Common;

namespace HydroCAD.Models.Geometry
{
    /// <summary>
    /// Terrain survey point.
    /// </summary>
    public enum HC_BOD
    {
        BASIC,      // basic terrain point
        BOTTOM,     // pit/basin singularity
        SADDLE,     // saddle point
        CORNER,     // corner/break point
        PEAK,       // local maximum
    }

    public class HCPoint
    {
        public HCPoint(Point3d point, int number, string handle = "0", HC_BOD type = HC_BOD.BASIC, string tag = "")
        {
            Point3d = point;
            Normal = new Vector3d(0, 0, 1);
            IsValid = false;
            Number = number;
            Handle = handle;
            Type = type;
            Tag = tag;
            NeighborsHandles = new HashSet<string>();
            Triangles = new List<HCTriangle>();
            Lines = new List<HCLine>();
            IsHranice = false;
        }

        public Point3d Point3d { get; private set; }
        public Vector3d Normal { get; private set; }
        public int Number { get; private set; }
        public string Tag { get; private set; }
        public string Handle { get; private set; }
        public HC_BOD Type { get; private set; }
        public HashSet<string> NeighborsHandles { get; private set; }
        public IList<HCTriangle> Triangles { get; private set; }
        public IList<HCLine> Lines { get; private set; }
        public bool IsHranice { get; private set; }
        public bool IsValid { get; private set; }

        // Associated label handles
        public string Label { get; private set; }
        public string TextNumber { get; private set; }
        public string TextTag { get; private set; }
        public string TextHeight { get; private set; }

        public Point2d Point2d => Point3d.ToPoint2d();

        public bool Equals(HCPoint other)
        {
            if (Handle.IsNullHandle() && other.Handle.IsNullHandle())
                return Number == other.Number;
            return Handle == other.Handle;
        }

        public IList<HCPoint> GetNeighbors(HC_SPOJNICE type = HC_SPOJNICE.ALL_TYPES)
        {
            var neighbors = new List<HCPoint>(Lines.Count);
            foreach (var line in Lines)
            {
                if (type.HasFlag(line.Type))
                {
                    HCPoint neighbor = Equals(line.Pt1) ? line.Pt2 : line.Pt1;
                    neighbors.Add(neighbor);
                }
            }
            return neighbors;
        }

        public void SetHandle(string handle)
        {
            if (!handle.IsNullHandle()) Handle = handle;
        }

        public void SetNormal(Vector3d normal)
        {
            Normal = normal;
            IsValid = true;
        }

        public void SetIsHranice() => IsHranice = true;

        public void SetNeighborsHandles(HashSet<string> neighbors) => NeighborsHandles = neighbors;

        public void SetAssociatedEntities(string label, string textNumber, string textTag, string textHeight)
        {
            if (!label.IsNullHandle()) Label = label;
            if (!textNumber.IsNullHandle()) TextNumber = textNumber;
            if (!textTag.IsNullHandle()) TextTag = textTag;
            if (!textHeight.IsNullHandle()) TextHeight = textHeight;
        }

        public HCLine CommonLine(HCPoint other)
        {
            if (this != null && other != null)
                return Lines.FirstOrDefault(l1 => other.Lines.Any(l2 => l1.Equals(l2)));
            return null;
        }

        public HCTriangle CommonTriangle(HCPoint other)
        {
            if (this != null && other != null)
                return Triangles.FirstOrDefault(t1 => other.Triangles.Any(t2 => t1.Equals(t2)));
            return null;
        }

        public static IDictionary<string, HCPoint> HandleMap(IList<HCPoint> points)
        {
            if (points != null)
                return points.ToDictionary(pt => pt.Handle, pt => pt);
            return null;
        }

        public override string ToString() => $"{Number}: {Point3d} ({Tag})";
    }
}
