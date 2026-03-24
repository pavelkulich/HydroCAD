using System;
using System.Collections.Generic;
using System.Linq;
using RailCAD.Common;

namespace RailCAD.Models.Geometry
{
    public class RCTriangle : IRCEntity
    {
        private IList<RCLine> lines;

        public RCTriangle(int id, IList<RCPoint> points, IList<RCLine> lines = null)
        {
            this.Id = id;
            this.Points = points;
            this.lines = lines;

            // calculate triangle properties
            this.Area = GeometryHelper.TriangleArea(Points2d[0], Points2d[1], Points2d[2]);
            this.Normal = GeometryHelper.TriangleNormal(this);
        }

        public int Id { get; private set; }

        public double Area { get; private set; }

        public Vector3d Normal { get; private set; }

        public IList<RCPoint> Points { get; private set; }

        public IList<RCLine> Lines
        {
            get
            {
                // lazy loading of lines (when required and if not already provided in constructor)
                if (this.lines == null)
                {
                    IList<RCLine> lines = new List<RCLine>(3);
                    for (int i = 0; i < this.Points.Count; i++)
                    {
                        RCPoint point = this.Points[i];
                        int nextIndex = i < (this.Points.Count - 1) ? (i + 1) : 0;
                        RCPoint pointNext = this.Points[nextIndex];

                        var triangleLine = point.Lines
                            .Where(l => (l.Pt1 == point && l.Pt2 == pointNext) ||
                                        (l.Pt1 == pointNext && l.Pt2 == point))
                            .FirstOrDefault();   // should always find one line

                        if (triangleLine != null)
                        {
                            lines.Add(triangleLine);
                        }
                    }
                    this.lines = lines;
                }
                return this.lines;
            }
        }

        public IList<Point2d> Points2d
        {
            get
            {
                return Points.Select(rcp => rcp.Point2d).ToList();
            }
        }

        public IList<Point3d> Points3d
        {
            get
            {
                return Points.Select(rcp => rcp.Point3d).ToList();
            }
        }

        public void LinkPointsToTriangle()
        {
            // add reference: point -> triangle
            foreach (RCPoint point in this.Points)
            {
                point.Triangles.Add(this);
            }
        }

        public bool Contains2D(Point2d point)
        {
            return Contains2D_Area(point);
            //return Contains2D_Bary(point);
            //return Contains2D_Dot(point);
            //return Contains2D_Dot2(point);
        }

        private bool Contains2D_Area(Point2d point)
        {
            // triangles area approach: https://www.geeksforgeeks.org/check-whether-a-given-point-lies-inside-a-triangle-or-not/

            // calculate area of triangle PBC
            double A1 = GeometryHelper.TriangleArea(point, Points2d[1], Points2d[2]);

            // calculate area of triangle PAC
            double A2 = GeometryHelper.TriangleArea(Points2d[0], point, Points2d[2]);

            // calculate area of triangle PAB
            double A3 = GeometryHelper.TriangleArea(Points2d[0], Points2d[1], point);

            // check if sum of A1, A2 and A3 is same as Area of ABC
            return GeometryHelper.EqualsWithTol(Area, A1 + A2 + A3);
        }

        private bool Contains2D_Bary(Point2d point)
        {
            // barycentric coordinates approach: https://www.geeksforgeeks.org/check-whether-a-given-point-lies-inside-a-triangle-or-not/

            // calculate the barycentric coordinates of point P with respect to triangle ABC
            Point2d point0 = Points2d[0];
            Point2d point1 = Points2d[1];
            Point2d point2 = Points2d[2];
            double denominator = ((point1.Y - point2.Y) * (point0.X - point2.X) + (point2.X - point1.X) * (point0.Y - point2.Y));
            double a = ((point1.Y - point2.Y) * (point.X - point2.X) + (point2.X - point1.X) * (point.Y - point2.Y)) / denominator;
            double b = ((point2.Y - point0.Y) * (point.X - point2.X) + (point0.X - point2.X) * (point.Y - point2.Y)) / denominator;
            double c = 1 - a - b;

            // check if all barycentric coordinates are non-negative
            return (a >= 0 && b >= 0 && c >= 0);
        }

        private bool Contains2D_Dot(Point2d point)
        {
            // dot product approach: https://stackoverflow.com/questions/2049582/how-to-determine-if-a-point-is-in-a-2d-triangle

            double d1 = Sign(point, Points2d[0], Points2d[1]);
            double d2 = Sign(point, Points2d[1], Points2d[2]);
            double d3 = Sign(point, Points2d[2], Points2d[0]);

            bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(has_neg && has_pos);
        }

        private static double Sign(Point2d p1, Point2d p2, Point2d p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        public override bool Equals(object obj)
        {
            if (obj is RCTriangle other)
            {
                return this.Id == other.Id;
            }
            return false;
        }

        public override int GetHashCode()
        {
            var ordered = this.Points.Select(p => p.Number).OrderBy(n => n);
            return HashCode.Combine(ordered.ElementAt(0), ordered.ElementAt(1), ordered.ElementAt(2));
        }

        public override string ToString()
        {
            return $"({Points[0].Number};{Points[1].Number};{Points[2].Number})";
        }

        public object WriteToXData()
        {
            throw new System.NotImplementedException();
        }

        //private bool Contains2D_Dot2(RCPoint point)
        //{
        //    // dot product approach: Tri
        //    var t0 = Points[0];
        //    var t1 = Points[1];
        //    var t2 = Points[2];

        //    // todo: no need to create new Point instances here
        //    Point2d d0 = new Point2d(t1.X - t0.X, t1.Y - t0.Y);
        //    Point2d d1 = new Point2d(t2.X - t0.X, t2.Y - t0.Y);
        //    Point2d d2 = new Point2d(point.X - t0.X, point.Y - t0.Y);

        //    // crossproduct of (0, 0, 1) and d0
        //    Point2d c0 = new Point2d(-d0.Y, d0.X);

        //    // crossproduct of (0, 0, 1) and d1
        //    Point2d c1 = new Point2d(-d1.Y, d1.X);

        //    // linear combination d2 = s * d0 + v * d1.
        //    // multiply both sides of the equation with c0 and c1 and solve for s and v respectively
        //    // s = d2 * c1 / d0 * c1
        //    // v = d2 * c0 / d1 * c0

        //    double s = DotProduct(d2, c1) / DotProduct(d0, c1);
        //    double v = DotProduct(d2, c0) / DotProduct(d1, c0);

        //    // check if point is inside or on the edge of this triangle.
        //    return (s >= 0 && v >= 0 && ((s + v) <= 1));
        //}

        //private static double DotProduct(Point2d p, Point2d q)
        //{
        //    return p.X * q.X + p.Y * q.Y;
        //}
    }
}
