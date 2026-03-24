using System;
using System.Collections.Generic;
using System.Linq;

using RailCAD.Models.Geometry;

namespace RailCAD.Common
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

        public static Vector3d TriangleNormal(RCTriangle triangle)
        {
            // triangle points are oriented clockwise -> normal will be oriented "up" (.)
            // (2)
            //  ^
            //  | .
            // (1)-->(0)
            Vector3d vec1 = triangle.Points3d[1].VectorTo(triangle.Points3d[0]);
            Vector3d vec2 = triangle.Points3d[1].VectorTo(triangle.Points3d[2]);
            return vec1.CrossProduct(vec2).Normalized();  // normalized
        }

        // To find orientation of ordered triplet 
        // (p1, p2, p3). The function returns 
        // following values 
        // 0 --> p, q and r are collinear
        // 1 --> Clockwise
        // 2 --> Counterclockwise
        public static int Orientation(Point2d p1, Point2d p2, Point2d p3)
        {
            // https://www.geeksforgeeks.org/orientation-3-ordered-points/
            double val = (p2.Y - p1.Y) * (p3.X - p2.X) - (p2.X - p1.X) * (p3.Y - p2.Y);

            if (val == 0) return 0;  // collinear

            // clock or counterclock wise
            return (val > 0) ? 1 : 2;
        }

        public static IList<RCPoint> OrientClockwise(RCPoint p1, RCPoint p2, RCPoint p3)
        {
            switch (Orientation(p1.Point2d, p2.Point2d, p3.Point2d))
            {
                case 1:   // clockwise
                    return new List<RCPoint> { p1, p2, p3 };
                case 2:  // counterclockwise
                    return new List<RCPoint> { p1, p3, p2 };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Finds first border point of terrain model.
        /// 1st point is always 1st of points are oriented clockwise or has bigger X or Y coordinate, if they are colinear.
        /// </summary>
        /// <returns>First border point</returns>
        public static RCPoint FirstBorderPoint(RCPoint basePoint, RCPoint p1, RCPoint p2)
        {
            IList<RCPoint> points = OrientClockwise(basePoint, p1, p2);
            if (points != null)
                return points[2];
            // points are colinear - bigger X or Y coordinate wins
            if ((p1.Point2d.X == p2.Point2d.X) ? (p1.Point2d.Y > p2.Point2d.Y) : (p1.Point2d.X > p2.Point2d.X))
                return p1;
            return p2;
        }

        /// <summary>
        /// Finds the intersection of two infinite 2D lines defined by two points each.
        /// Returns null if the lines are parallel.
        /// </summary>
        public static Point2d? FindIntersectionBetweenTwo2DLines(Point2d line1Pt1, Point2d line1Pt2, Point2d line2Pt1, Point2d line2Pt2)
        {
            // https://rosettacode.org/wiki/Find_the_intersection_of_two_lines
            // note: infinite length lines, passing through Pt1 and Pt2, are considered (not segments with finite length)
            double a1 = line1Pt2.Y - line1Pt1.Y;
            double b1 = line1Pt1.X - line1Pt2.X;
            double c1 = a1 * line1Pt1.X + b1 * line1Pt1.Y;

            double a2 = line2Pt2.Y - line2Pt1.Y;
            double b2 = line2Pt1.X - line2Pt2.X;
            double c2 = a2 * line2Pt1.X + b2 * line2Pt1.Y;

            double delta = a1 * b2 - a2 * b1;

            if (EqualsZeroWithTol(delta, 1E-9))
            {
                return null;  // lines are parallel
            }
            else
            {
                return new Point2d((b2 * c1 - b1 * c2) / delta, (a1 * c2 - a2 * c1) / delta);
            }
        }

        // <summary>
        /// Calculates the polyline bulge for an arc segment defined by start/end points and middle point.
        /// Sign of the bulge follows the sweep direction: CCW > 0, CW < 0.
        /// </summary>
        public static double CalculateBulge(Point2d middlePoint, Point2d startPoint, Point2d endPoint)
        {
            double angle = AngleBetween3Points(middlePoint, startPoint, endPoint);

            // Compute orientation (sign) using cross product in XY plane
            Vector2d v1 = middlePoint.VectorTo(startPoint);
            Vector2d v2 = middlePoint.VectorTo(endPoint);

            double cross = v1.CrossProduct(v2);
            double sign = cross < 0 ? 1 : -1;

            // Bulge = tan(angle), sign gives CW/CCW
            return sign * Math.Tan(angle);
        }

        /// <summary>
        /// Calculate point at polar coordinates
        /// </summary>
        public static Point2d PolarPoint(Point2d pt, double angle, double distance)
        {
            return new Point2d(
                pt.X + distance * Math.Cos(angle),
                pt.Y + distance * Math.Sin(angle)
            );
        }

        // <summary>
        /// Calculates the angle (in radians) between three points (A-B-C), where B is the vertex.
        /// </summary>
        public static double AngleBetween3Points(Point2d pointA, Point2d pointB, Point2d pointC)
        {
            double dA = pointB.DistanceTo(pointC); // |BC|
            double dB = pointC.DistanceTo(pointA); // |CA|
            double dC = pointA.DistanceTo(pointB); // |AB|

            double numerator = (dA * dA + dC * dC - dB * dB);
            double denominator = (2.0 * dA * dC);

            if (denominator == 0)
                return 0.0; // Degenerate case: points overlap

            double an = numerator / denominator;

            // Clamp due to possible floating-point rounding errors
            if (Math.Abs(an) > 1.0)
                an = an > 0 ? 1.0 : -1.0;

            return Math.Abs(Math.Acos(an));
        }

        /// <summary>
        /// Returns closest point on a 2D line to a given point.
        /// </summary>
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

        /// <summary>
        /// Calculation of arc center using chord and bulge
        /// </summary>
        /// <returns>Center point, start and end angle for given polyline arc segment</returns>
        public static void GetArcCenter(Point2d p1, Point2d p2, double bulge, out Point2d center, out double startAng, out double endAng)
        {
            // Chord vector and length
            Vector2d chord = p1.VectorTo(p2);
            double chordLength = chord.Length;

            double theta = 4.0 * Math.Atan(Math.Abs(bulge));
            double radius = chordLength / (2.0 * Math.Sin(theta / 2.0));

            // midpoint chords
            Point2d mid = new Point2d((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0);

            // perpendicular vector
            Vector2d perp = new Vector2d(-chord.Y, chord.X).Normalize();

            // distance from midpoint to arc center
            double h = Math.Sqrt(Math.Max(0, radius * radius - (chordLength / 2.0) * (chordLength / 2.0)));

            if (bulge < 0)
            {
                perp = -perp;
            }

            center = new Point2d(mid.X + perp.X * h, mid.Y + perp.Y * h);

            startAng = center.AngleTo(p1);
            endAng = center.AngleTo(p2);
        }

        /// <summary>
        /// Tests if given angle is between two angles of polyline arc segment
        /// </summary>
        public static bool AngleInRange(double angle, double start, double end, bool ccw)
        {
            double angNorm = NormalizeAngle(angle);
            double s = NormalizeAngle(start);
            double e = NormalizeAngle(end);

            if (ccw)
                return (s <= e) ? (angNorm >= s && angNorm <= e) : (angNorm >= s || angNorm <= e);
            else
                return (e <= s) ? (angNorm <= s && angNorm >= e) : (angNorm <= s || angNorm >= e);
        }

        /// <summary>
        /// Arc direction is always counter-clockwise
        /// </summary>
        /// <returns>Returns the correct angle of arc between two angles</returns>
        public static double NormalizeAngle(double a)
        {
            while (a < 0) a += 2 * Math.PI;
            while (a >= 2 * Math.PI) a -= 2 * Math.PI;
            return a;
        }

        /// <summary>
        /// Finds intersection points between two circles
        /// </summary>
        /// <param name="center1">Center of first circle</param>
        /// <param name="radius1">Radius of first circle</param>
        /// <param name="center2">Center of second circle</param>
        /// <param name="radius2">Radius of second circle</param>
        /// <returns>List of intersection points (0, 1, or 2 points)</returns>
        public static List<Point2d> FindCircleCircleIntersections(Point2d center1, double radius1, Point2d center2, double radius2)
        {
            var intersections = new List<Point2d>();

            // Vector between circle centers
            Vector2d diff = center1.VectorTo(center2);
            double d = diff.Length;

            // Check for no intersection cases
            if (d > radius1 + radius2) // Circles too far apart
                return intersections;

            if (d < Math.Abs(radius1 - radius2)) // One circle inside another
                return intersections;

            if (EqualsZeroWithTol(d)) // Concentric circles
            {
                if (EqualsWithTol(radius1, radius2)) // Identical circles - infinite intersections
                    return intersections; // Return empty - too many solutions
                else
                    return intersections; // No intersection
            }

            // Calculate intersection points
            double a = (radius1 * radius1 - radius2 * radius2 + d * d) / (2.0 * d);
            double h = Math.Sqrt(Math.Max(0, radius1 * radius1 - a * a));

            // Base point along the line between centers
            Vector2d dir = diff / d; // normalized direction
            Point2d p2 = new Point2d(center1.X + a * dir.X, center1.Y + a * dir.Y);

            if (EqualsZeroWithTol(h)) // Circles touch at one point
            {
                intersections.Add(p2);
            }
            else // Two intersection points
            {
                // Perpendicular vector
                Vector2d perp = new Vector2d(-dir.Y, dir.X);

                intersections.Add(new Point2d(p2.X + h * perp.X, p2.Y + h * perp.Y));
                intersections.Add(new Point2d(p2.X - h * perp.X, p2.Y - h * perp.Y));
            }

            return intersections;
        }

        /// <summary>
        /// Finds intersection between a line and a circle.
        /// Returns intersection point if it lies on the line, null otherwise.
        /// </summary>
        /// <param name="lineStart">Start point of line</param>
        /// <param name="lineEnd">End point of line</param>
        /// <param name="circleCenter">Center of the circle</param>
        /// <param name="circleRadius">Radius of the circle</param>
        /// <returns>Intersection point or null</returns>
        public static Point2d? FindLineCircleIntersection(Point2d lineStart, Point2d lineEnd, Point2d circleCenter, double circleRadius)
        {
            var intersections = FindLineCircleIntersections(lineStart, lineEnd, circleCenter, circleRadius);

            // Check which intersection lies on both the line segment and arc segment
            foreach (var intersection in intersections)
            {
                if (IsPointOnLineSegment(lineStart, lineEnd, intersection))
                {
                    return intersection;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds intersection points between a line segment and a circle.
        /// </summary>
        public static List<Point2d> FindLineCircleIntersections(Point2d lineStart, Point2d lineEnd,
                                                          Point2d circleCenter, double circleRadius)
        {
            var intersections = new List<Point2d>();

            // Vector from line start to end
            Vector2d d = lineStart.VectorTo(lineEnd);

            // Vector from line start to circle center
            Vector2d f = circleCenter.VectorTo(lineStart);

            // Quadratic equation coefficients for line-circle intersection
            double a = d.DotProduct(d);
            double b = 2 * f.DotProduct(d);
            double c = f.DotProduct(f) - circleRadius * circleRadius;

            double discriminant = b * b - 4 * a * c;

            if (discriminant < 0 || EqualsZeroWithTol(a))
                return intersections; // No intersection or degenerate line

            double sqrt_discriminant = Math.Sqrt(discriminant);

            // Calculate intersection points on infinite line
            double t1 = (-b - sqrt_discriminant) / (2 * a);
            double t2 = (-b + sqrt_discriminant) / (2 * a);

            // Add valid intersections
            if (!EqualsWithTol(t1, t2)) // Two different intersections
            {
                intersections.Add(new Point2d(lineStart.X + t1 * d.X, lineStart.Y + t1 * d.Y));
                intersections.Add(new Point2d(lineStart.X + t2 * d.X, lineStart.Y + t2 * d.Y));
            }
            else // Tangent case - single intersection
            {
                intersections.Add(new Point2d(lineStart.X + t1 * d.X, lineStart.Y + t1 * d.Y));
            }

            return intersections;
        }

        /// <summary>
        /// Finds intersection between two line segments.
        /// Returns intersection point if it lies on both segments, null otherwise.
        /// </summary>
        public static Point2d? FindLineLineIntersection(Point2d line1Start, Point2d line1End,
                                                 Point2d line2Start, Point2d line2End)
        {
            // Use existing GeometryHelper method for infinite lines
            Point2d? intersection = FindIntersectionBetweenTwo2DLines(line1Start, line1End, line2Start, line2End);

            if (intersection == null)
                return null;

            Point2d intersectionPoint = intersection.Value;

            // Check if intersection lies on both line segments
            if (IsPointOnLineSegment(line1Start, line1End, intersectionPoint) &&
                IsPointOnLineSegment(line2Start, line2End, intersectionPoint))
            {
                return intersectionPoint;
            }

            return null;
        }

        /// <summary>
        /// Checks if a point lies on a line segment within tolerance.
        /// </summary>
        public static bool IsPointOnLineSegment(Point2d lineStart, Point2d lineEnd, Point2d point, double tolerance = 1e-6)
        {
            double lineLength = lineStart.DistanceTo(lineEnd);
            double distToStart = point.DistanceTo(lineStart);
            double distToEnd = point.DistanceTo(lineEnd);

            // Point is on segment if sum of distances equals line length (within tolerance)
            return EqualsWithTol(distToStart + distToEnd, lineLength, tolerance);
        }

        /// <summary>
        /// Finds the closest points between two line segments (p1-p2) and (q1-q2).
        /// Returns a list with the point on the first segment and the point on the second segment.
        /// </summary>
        public static List<Point2d> ClosestPointsLineLine(Point2d p1, Point2d p2, Point2d q1, Point2d q2)
        {
            // Compute direction vectors
            Vector2d u = p1.VectorTo(p2);
            Vector2d v = q1.VectorTo(q2);
            Vector2d w0 = q1.VectorTo(p1);

            // Dot products
            double a = u.DotProduct(u);
            double b = u.DotProduct(v);
            double c = v.DotProduct(v);
            double d = u.DotProduct(w0);
            double e = v.DotProduct(w0);

            double denom = a * c - b * b;

            // Check for parallel lines
            double sc, tc;
            if (Math.Abs(denom) < 1e-12)
            {
                sc = 0.0;
                tc = (b > c ? d / b : e / c);
            }
            else
            {
                sc = (b * e - c * d) / denom;
                tc = (a * e - b * d) / denom;
            }

            // Clamp sc and tc to [0,1] to stay within segments
            sc = Math.Max(0.0, Math.Min(1.0, sc));
            tc = Math.Max(0.0, Math.Min(1.0, tc));

            // Candidate point from projection
            Point2d candP = new Point2d(p1.X + u.X * sc, p1.Y + u.Y * sc);
            Point2d candQ = new Point2d(q1.X + v.X * tc, q1.Y + v.Y * tc);

            // Prepare list of candidate point pairs
            List<Point2d[]> candidates = new List<Point2d[]>
            {
                new Point2d[] { candP, candQ },
                new Point2d[] { p1, ClosestPointOnSegment(p1, q1, q2) },
                new Point2d[] { p2, ClosestPointOnSegment(p2, q1, q2) },
                new Point2d[] { ClosestPointOnSegment(q1, p1, p2), q1 },
                new Point2d[] { ClosestPointOnSegment(q2, p1, p2), q2 }
            };

            // Find the pair with the minimum distance
            double minDist = double.MaxValue;
            Point2d bestP = candP;
            Point2d bestQ = candQ;

            foreach (var pair in candidates)
            {
                double dist = pair[0].DistanceTo(pair[1]);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestP = pair[0];
                    bestQ = pair[1];
                }
            }

            return new List<Point2d> { bestP, bestQ };
        }

        /// <summary>
        /// Finds the closest point on segment AB to point P.
        /// </summary>
        private static Point2d ClosestPointOnSegment(Point2d p, Point2d a, Point2d b)
        {
            Vector2d ab = a.VectorTo(b);
            Vector2d ap = a.VectorTo(p);
            double t = ap.DotProduct(ab) / ab.DotProduct(ab);
            t = Math.Max(0, Math.Min(1, t));
            return new Point2d(a.X + t * ab.X, a.Y + t * ab.Y);
        }

        /// <summary>
        /// Determines if two lists of Point3d are equal within tolerance or not.
        /// </summary>
        /// <returns>True if they are equal within tolerance, otherwise false.</returns>
        public static bool IsEqualTo(this List<Point3d> first, List<Point3d> second, double tolerance)
        {
            if (first == null || second == null || first.Count != second.Count) return false;

            for (int i = 0; i < first.Count; i++)
            {
                if (!first[i].IsEqualTo(second[i], tolerance))
                    return false;
            }
            return true;
        }
    }
}
