using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.Common;

namespace HydroCAD.Models.Geometry
{
    public class HCTriangle
    {
        private IList<HCLine> lines;

        public HCTriangle(int id, IList<HCPoint> points, IList<HCLine> lines = null)
        {
            Id = id;
            Points = points;
            this.lines = lines;
            Area = GeometryHelper.TriangleArea(Points2d[0], Points2d[1], Points2d[2]);
            Normal = GeometryHelper.TriangleNormal(this);
        }

        public int Id { get; private set; }
        public double Area { get; private set; }
        public Vector3d Normal { get; private set; }
        public IList<HCPoint> Points { get; private set; }

        public IList<HCLine> Lines
        {
            get
            {
                if (this.lines == null)
                {
                    var result = new List<HCLine>(3);
                    for (int i = 0; i < Points.Count; i++)
                    {
                        HCPoint point = Points[i];
                        int nextIndex = i < Points.Count - 1 ? i + 1 : 0;
                        HCPoint pointNext = Points[nextIndex];
                        var triangleLine = point.Lines
                            .FirstOrDefault(l => (l.Pt1 == point && l.Pt2 == pointNext) ||
                                                 (l.Pt1 == pointNext && l.Pt2 == point));
                        if (triangleLine != null) result.Add(triangleLine);
                    }
                    this.lines = result;
                }
                return this.lines;
            }
        }

        public IList<Point2d> Points2d => Points.Select(p => p.Point2d).ToList();
        public IList<Point3d> Points3d => Points.Select(p => p.Point3d).ToList();

        public void LinkPointsToTriangle()
        {
            foreach (HCPoint point in Points)
                point.Triangles.Add(this);
        }

        public bool Contains2D(Point2d point)
        {
            double A1 = GeometryHelper.TriangleArea(point, Points2d[1], Points2d[2]);
            double A2 = GeometryHelper.TriangleArea(Points2d[0], point, Points2d[2]);
            double A3 = GeometryHelper.TriangleArea(Points2d[0], Points2d[1], point);
            return GeometryHelper.EqualsWithTol(Area, A1 + A2 + A3);
        }

        public override bool Equals(object obj)
        {
            if (obj is HCTriangle other) return Id == other.Id;
            return false;
        }

        public override int GetHashCode()
        {
            var ordered = Points.Select(p => p.Number).OrderBy(n => n);
            return HashCode.Combine(ordered.ElementAt(0), ordered.ElementAt(1), ordered.ElementAt(2));
        }

        public override string ToString() => $"({Points[0].Number};{Points[1].Number};{Points[2].Number})";
    }
}
