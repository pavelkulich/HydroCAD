using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.Models.Geometry;

namespace HydroCAD.Common
{
    public static class GeometryHelper
    {
        public static bool EqualsWithTol(double a, double b, double tol = 1e-6)
        {
            return Math.Abs(a - b) < tol;
        }

        public static bool EqualsZeroWithTol(double a, double tol = 1e-6)
        {
            return Math.Abs(a) < tol;
        }

        public static double TriangleArea(Point2d pnt1, Point2d pnt2, Point2d pnt3)
        {
            return Math.Abs((pnt1.X * (pnt2.Y - pnt3.Y) +
                             pnt2.X * (pnt3.Y - pnt1.Y) +
                             pnt3.X * (pnt1.Y - pnt2.Y)) / 2.0);
        }

        public static Vector3d AverageVectors(IEnumerable<Vector3d> vectors)
        {
            return new Vector3d(
                vectors.Average(x => x.X),
                vectors.Average(x => x.Y),
                vectors.Average(x => x.Z)
            );
        }

        public static Vector3d TriangleNormal(HCTriangle triangle)
        {
            Vector3d vec1 = triangle.Points3d[1].VectorTo(triangle.Points3d[0]);
            Vector3d vec2 = triangle.Points3d[1].VectorTo(triangle.Points3d[2]);
            return vec1.CrossProduct(vec2).Normalized();
        }

        public static int Orientation(Point2d p1, Point2d p2, Point2d p3)
        {
            double val = (p2.Y - p1.Y) * (p3.X - p2.X) - (p2.X - p1.X) * (p3.Y - p2.Y);
            if (val == 0) return 0;
            return (val > 0) ? 1 : 2;
        }

        public static IList<HCPoint> OrientClockwise(HCPoint p1, HCPoint p2, HCPoint p3)
        {
            switch (Orientation(p1.Point2d, p2.Point2d, p3.Point2d))
            {
                case 1: return new List<HCPoint> { p1, p2, p3 };
                case 2: return new List<HCPoint> { p1, p3, p2 };
                default: return null;
            }
        }

        public static HCPoint FirstBorderPoint(HCPoint basePoint, HCPoint p1, HCPoint p2)
        {
            IList<HCPoint> points = OrientClockwise(basePoint, p1, p2);
            if (points != null) return points[2];
            if ((p1.Point2d.X == p2.Point2d.X) ? (p1.Point2d.Y > p2.Point2d.Y) : (p1.Point2d.X > p2.Point2d.X))
                return p1;
            return p2;
        }

        public static Point2d? FindIntersectionBetweenTwo2DLines(Point2d line1Pt1, Point2d line1Pt2, Point2d line2Pt1, Point2d line2Pt2)
        {
            double a1 = line1Pt2.Y - line1Pt1.Y;
            double b1 = line1Pt1.X - line1Pt2.X;
            double c1 = a1 * line1Pt1.X + b1 * line1Pt1.Y;

            double a2 = line2Pt2.Y - line2Pt1.Y;
            double b2 = line2Pt1.X - line2Pt2.X;
            double c2 = a2 * line2Pt1.X + b2 * line2Pt1.Y;

            double delta = a1 * b2 - a2 * b1;
            if (EqualsZeroWithTol(delta, 1E-9)) return null;
            return new Point2d((b2 * c1 - b1 * c2) / delta, (a1 * c2 - a2 * c1) / delta);
        }

        public static Point2d ClosestPointOnLine(Point2d start, Point2d end, Point2d point)
        {
            Vector2d ab = start.VectorTo(end);
            double len2 = ab.DotProduct(ab);
            if (len2 < 1e-12) return start;
            Vector2d ap = start.VectorTo(point);
            double t = ap.DotProduct(ab) / len2;
            t = Math.Max(0, Math.Min(1, t));
            return new Point2d(start.X + t * ab.X, start.Y + t * ab.Y);
        }

        public static bool IsEqualTo(this List<Point3d> first, List<Point3d> second, double tolerance)
        {
            if (first == null || second == null || first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (!first[i].IsEqualTo(second[i], tolerance)) return false;
            }
            return true;
        }
    }
}
