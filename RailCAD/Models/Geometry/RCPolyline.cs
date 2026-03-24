using System;
using System.Collections.Generic;
using System.Linq;
using static RailCAD.Common.GeometryHelper;

namespace RailCAD.Models.Geometry
{
    public class RCPolyline
    {
        private List<PolylineVertex> vertices;

        public string Handle { get; internal set; }
        public List<PolylineVertex> Vertices
        {
            get => vertices;
            set
            {
                if (value != null && value.Count > 0)
                {
                    vertices = value;
                }
            }
        }
        public int NumberOfVertices
        { 
            get
            {
                if (Vertices != null)
                {
                    return Vertices.Count;
                }
                return 0;
            }
        }
        public double Length
        {
            get
            {
                if (NumberOfVertices == 0)
                    return 0;

                return GetLengthToVertex(NumberOfVertices - 1);
            }
        }
        public List<Point2d> Points
        {
            get
            {
                if (NumberOfVertices == 0)
                    return null;

                List<Point2d> points = new List<Point2d>();

                foreach (PolylineVertex vertex in Vertices)
                {
                    points.Add(vertex.Point);
                }
                return points;
            }
        }

        public Point2d StartPoint => NumberOfVertices > 0 ? Vertices[0].Point : new Point2d(0, 0);

        public Point2d EndPoint => NumberOfVertices > 0 ? Vertices[NumberOfVertices - 1].Point : new Point2d(0, 0);

        public RCPolyline(List<PolylineVertex> newVertices = null, string handle = "0")
        {
            vertices = newVertices ?? new List<PolylineVertex>();
            Handle = handle;
        }

        // for temporary polylines
        public RCPolyline(List<Point2d> points)
        {
            vertices = new List<PolylineVertex>();
            Handle = "0";

            foreach (var point in points)
            {
                Vertices.Add(new PolylineVertex(point));
            }
        }

        public Point2d GetPoint2dAt(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= NumberOfVertices)
                vertexIndex = 0;
            PolylineVertex vertex = Vertices[vertexIndex];
            return vertex.Point;

        }

        public double GetBulgeAt(int vertexIndex)
        {
            if (vertexIndex < 0 || vertexIndex >= NumberOfVertices) return 0;

            PolylineVertex vertex = Vertices[vertexIndex];
            return vertex.Bulge;
        }

        /// <summary>
        /// Inserts or updates a vertex of a polyline geometry at the given index.
        /// </summary>
        internal RCPolyline AddVertex(int index, Point2d point, double bulge, bool fromStart = false)
        {
            if (index < 0 || index > NumberOfVertices) return this;

            List<PolylineVertex> vertices = Vertices ?? new List<PolylineVertex>();
            PolylineVertex vertex = new PolylineVertex(point, bulge);
            if (fromStart)
            {
                vertices.Insert(0, vertex);
            }
            else
            {
                if (index < NumberOfVertices)
                {
                    vertices[index] = vertex;
                }
                else
                {
                    vertices.Add(vertex);
                }
            }

            Vertices = vertices;
            return this;
        }

        public RCPolyline SetBulgeAt(int vertexIndex, double bulge)
        {
            if (Math.Abs(bulge) < 1e-12 || vertexIndex < 0 || vertexIndex >= NumberOfVertices) return this;

            PolylineVertex vertex = Vertices[vertexIndex];
            vertex.Bulge = bulge;
            Vertices[vertexIndex] = vertex;
            return this;
        }

        /// <summary>
        /// Inserts or updates points of a polyline geometry at the given index.
        /// </summary>
        internal RCPolyline AddVertices(List<PolylineVertex> vertices, int vertexIndex = -1, bool fromStart = false)
        {
            if (fromStart)
            {
                vertices.Reverse();
            }
            if (vertexIndex < 0)
                vertexIndex = NumberOfVertices; // append points to the end

            foreach (var vertex in vertices)
            {
                AddVertex(vertexIndex++, vertex.Point, vertex.Bulge, fromStart);
            }
            return this;
        }

        /// <summary>
        /// Inserts or updates vertices of a polyline geometry at the given index.
        /// </summary>
        internal RCPolyline AddPoints(List<Point2d> points, int vertexIndex = -1, bool fromStart = false)
        {
            var vertices = new List<PolylineVertex>(points.Count);
            foreach (var point in points)
            {
                vertices.Add(new PolylineVertex(point));
            }

            return AddVertices(vertices, vertexIndex, fromStart);
        }

        internal RCPolyline RemovePolylinePoints(int index = 0, bool fromStart = false)
        {
            if (Vertices == null || index <= 0 || index >= NumberOfVertices) return this;

            if (fromStart)
            {
                for (int i = 0; i < index; i++)
                {
                    Vertices.RemoveAt(0);
                }
            }
            else
            {
                for (int i = NumberOfVertices - 1; i >= index; i--)
                {
                    Vertices.RemoveAt(i);
                }
            }
            return this;
        }

        internal RCPolyline RemovePolylinePoints(Point2d intersection, bool fromStart = false)
        {
            if (NumberOfVertices == 0) return this;

            double param = GetParameterAtPoint(GetClosestPointTo(intersection));
            int vertexIndex = (int)Math.Ceiling(param);
            RemovePolylinePoints(vertexIndex, fromStart);
            return this;
        }

        /// <summary>
        /// Reverses polyline including shifting bulges to the correct index and changing its sign.
        /// </summary>
        public void ReversePolyline()
        {
            if (vertices == null || vertices.Count == 0)
                return;

            var reversed = new List<PolylineVertex>(vertices.Count);

            for (int i = vertices.Count - 1; i >= 0; i--)
            {
                var vertex = vertices[i];

                // move bulge from current vertex to the previous
                double newBulge = 0;
                if (i > 0)
                {
                    newBulge = -vertices[i - 1].Bulge; // change the bulge sign
                }

                reversed.Add(new PolylineVertex
                {
                    Point = vertex.Point,
                    Bulge = newBulge
                });
            }

            vertices = reversed;
        }

        /// <summary>
        /// Returns the closest point on the polyline to a given point.
        /// Projection is always orthogonal to the curve.
        /// </summary>
        public Point2d GetClosestPointTo(Point2d point)
        {
            double bestDist = double.MaxValue;
            Point2d bestPoint = Vertices[0].Point;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                Point2d candidate = v1.GetClosestPointOnSegment(v2, point);
                double d = candidate.DistanceTo(point);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoint = candidate;
                }
            }

            return bestPoint;
        }

        /// <summary>
        /// Returns parameter the closest point on the polyline to a given point.
        /// Parameter of a vertex is its index.
        /// </summary>
        public double GetParameterAtPoint(Point2d point)
        {
            double bestDist = double.MaxValue;
            Point2d bestPoint = Vertices[0].Point;
            int bestIndex = 0;

            for (int i = 0; i < NumberOfVertices - 2; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                Point2d candidate = v1.GetClosestPointOnSegment(v2, point);
                double d = candidate.DistanceTo(point);

                if (d < bestDist)
                {
                    bestDist = d;
                    bestPoint = candidate;
                    bestIndex = i;
                }
            }

            double length = Vertices[bestIndex].Point.DistanceTo(Vertices[bestIndex + 1].Point);
            double factor = Vertices[bestIndex].Point.DistanceTo(bestPoint) / length;

            return bestIndex + factor;
        }

        /// <summary>
        /// Computes the length of the polyline from the start up to the given parameter.
        /// </summary>
        public double GetDistanceAtParameter(double param)
        {
            if (param <= 0 || param + 1 > NumberOfVertices)
                return 0;
            int vertexIndex = (int)Math.Floor(param);

            double totalLength = 0.0;

            for (int i = 0; i < vertexIndex; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                totalLength += v1.GetSegmentLength(v2);
            }
            double factor = param - vertexIndex;
            double lenght = factor == 0 ? 0 : Vertices[vertexIndex].GetSegmentLength(Vertices[vertexIndex + 1]);

            return totalLength + lenght * factor;
        }

        /// <summary>
        /// Get point at specified distance from the start of polyline.
        /// </summary>
        /// <returns>If the distance is larger than total length it returns end point</returns>
        public Point2d GetPointAtDistance(double distance)
        {
            if (distance <= 0 || NumberOfVertices == 0)
                return StartPoint;

            double remaining = distance;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                double segLength = v1.GetSegmentLength(v2);

                if (remaining > segLength)
                {
                    remaining -= segLength;
                    continue;
                }

                // point is inside of this segment
                if (Math.Abs(v1.Bulge) < 1e-12)
                {
                    // line
                    double factor = remaining / segLength;
                    double x = v1.Point.X + factor * (v2.Point.X - v1.Point.X);
                    double y = v1.Point.Y + factor * (v2.Point.Y - v1.Point.Y);
                    return new Point2d(x, y);
                }
                else
                {
                    // arc
                    GetArcCenter(v1.Point, v2.Point, v1.Bulge, out Point2d center, out double startAng, out double endAng);
                    bool ccw = v1.Bulge > 0;

                    double chordLength = v1.Point.DistanceTo(v2.Point);
                    double sweep = 4.0 * Math.Atan(Math.Abs(v1.Bulge));
                    double radius = chordLength / (2.0 * Math.Sin(sweep / 2.0));

                    // angle corresponding to the specified distance
                    double delta = remaining / radius;
                    double targetAng = ccw ? startAng + delta : startAng - delta;

                    return PolarPoint(center, targetAng, radius);
                }
            }

            return EndPoint;
        }

        /// <summary>
        /// Get parameter at specified distance from the start of polyline.
        /// </summary>
        /// <returns>If the distance is larger than total length it returns end point</returns>
        public double GetParameterAtDistance(double distance)
        {
            if (distance <= 0 || NumberOfVertices == 0)
                return 0.0;

            double remaining = distance;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                double segLength = v1.GetSegmentLength(v2);

                if (remaining > segLength)
                {
                    remaining -= segLength;
                    continue;
                }

                // distance lies inside of this segment
                double factor = remaining / segLength;
                return i + factor;
            }

            return NumberOfVertices - 1;
        }

        /// <summary>
        /// Computes the length of the polyline from the start up to the given vertex index.
        /// </summary>
        public double GetLengthToVertex(int vertexIndex)
        {
            if (vertexIndex <= 0 || vertexIndex >= NumberOfVertices)
                return 0;

            double totalLength = 0.0;

            for (int i = 0; i < vertexIndex; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                totalLength += v1.GetSegmentLength(v2);
            }

            return totalLength;
        }

        /// <summary>
        /// Returns length from start of polyline to a specified point assumed to lie on the polyline.
        /// </summary>
        public double GetLengthToPoint(Point2d point)
        {
            double totalLength = 0.0;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                Point2d closest = v1.GetClosestPointOnSegment(v2, point);

                if (closest.Equals(point))
                {
                    // point lies on this segment -> add only partial length
                    return totalLength + v1.GetPartialLength(v2, point);
                }
                else
                {
                    // full segment
                    totalLength += v1.GetSegmentLength(v2);
                }
            }

            return totalLength;
        }

        /// <summary>
        /// Returns all polyline vertices points and also midpoint of each segment.
        /// </summary>
        public List<Point2d> GetVerticesWithMidpoints()
        {
            var result = new List<Point2d>();

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                // add vertex point
                result.Add(v1.Point);

                // add midpoint of segment
                Point2d mid = v1.GetMiddlePoint(v2);
                result.Add(mid);
            }

            // add last vertex
            if (NumberOfVertices > 0)
                result.Add(Vertices[NumberOfVertices - 1].Point);

            return result;
        }

        /// <summary>
        /// Finds intersection between a polyline (with linear and arc segments) and a circle.
        /// Returns first intersection found that lies on the polyline curve.
        /// </summary>
        /// <param name="circleCenter">Center of the circle</param>
        /// <param name="circleRadius">Radius of the circle</param>
        /// <returns>First intersection point or null if no intersection found on the curve</returns>
        public Point2d? IntersectWithCircle(Point2d circleCenter, double circleRadius)
        {
            if (Vertices == null || NumberOfVertices < 2)
                return null;

            // Check each segment of the polyline
            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var v1 = Vertices[i];
                var v2 = Vertices[i + 1];

                Point2d? intersection = null;

                if (Math.Abs(v1.Bulge) < 1e-12)
                {
                    // Linear segment
                    intersection = FindLineCircleIntersection(v1.Point, v2.Point, circleCenter, circleRadius);
                }
                else
                {
                    // Arc segment
                    intersection = v1.FindArcSegmentCircleIntersection(v2, circleCenter, circleRadius);
                }

                if (intersection.HasValue)
                    return intersection;
            }

            return null;
        }

        /// <summary>
        /// Finds intersection between this polyline and another polyline.
        /// Returns first intersection found that lies on both curves.
        /// </summary>
        /// <param name="otherPolyline">The other polyline to intersect with</param>
        /// <returns>First intersection point or null if no intersection found on both curves</returns>
        public Point2d? IntersectPolylines(RCPolyline otherPolyline)
        {
            if (otherPolyline?.Vertices == null || otherPolyline.NumberOfVertices < 2)
                return null;

            if (Vertices == null || NumberOfVertices < 2)
                return null;

            // Check each segment of this polyline against each segment of the other polyline
            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var thisV1 = Vertices[i];
                var thisV2 = Vertices[i + 1];

                for (int j = 0; j < otherPolyline.NumberOfVertices - 1; j++)
                {
                    var otherV1 = otherPolyline.Vertices[j];
                    var otherV2 = otherPolyline.Vertices[j + 1];

                    Point2d? intersection = PolylineVertex.FindSegmentIntersection(thisV1, thisV2, otherV1, otherV2);

                    if (intersection.HasValue)
                        return intersection;
                }
            }

            return null;
        }

        /// <summary>
        /// Splits polyline by given index and trims overlapping part in the middle. Updates original polyline's vertices.
        /// </summary>
        public bool SplitAndTrimOverlap(int splitIndex)
        {
            if (Vertices == null || NumberOfVertices < 3 || splitIndex == 0 || splitIndex >= NumberOfVertices)
                return false;

            var vertices1 = new List<PolylineVertex>(splitIndex);
            var vertices2 = new List<PolylineVertex>(NumberOfVertices - splitIndex);

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                if (i < splitIndex)
                    vertices1.Add(Vertices[i]);
                else
                    vertices2.Add(Vertices[i]);
            }
            var polyline1 = new RCPolyline(vertices1);
            var polyline2 = new RCPolyline(vertices2);

            Point2d? intersection = polyline1.IntersectPolylines(polyline2);  // intersection between two halves of polyline

            if (intersection != null)
            {
                polyline1.RemovePolylinePoints(intersection.Value, false);
                polyline1.AddVertex(polyline1.NumberOfVertices, intersection.Value, 0);
                polyline2.RemovePolylinePoints(intersection.Value, true);

                vertices1 = polyline1.Vertices.Concat(polyline2.Vertices).ToList();
                Vertices = vertices1;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds closest distance between two polylines.
        /// </summary>
        /// <returns>List of two points on each curve showing the distance.</returns>
        public List<Point2d> GetClosestDistanceBetweenPolylines(RCPolyline other)
        {
            if (NumberOfVertices < 2 || other.NumberOfVertices < 2)
                return null;

            double bestDist = double.MaxValue;
            List<Point2d> bestPair = null;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                var segA1 = Vertices[i];
                var segA2 = Vertices[i + 1];

                for (int j = 0; j < other.NumberOfVertices - 1; j++)
                {
                    var segB1 = other.Vertices[j];
                    var segB2 = other.Vertices[j + 1];

                    List<Point2d> pair = PolylineVertex.ClosestPointsBetweenSegments(segA1, segA2, segB1, segB2);

                    double d = pair[0].DistanceTo(pair[1]);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestPair = pair;
                    }
                }
            }

            return bestPair;
        }

        /// <summary>
        /// Returns a point on the polyline at the specified parameter value.
        /// Parameter represents vertex index + fractional position along the segment.
        /// For example: param = 1.5 means halfway between vertex 1 and vertex 2.
        /// Handles both straight line segments and arc segments (with bulge).
        /// </summary>
        /// <param name="param">Parameter value (0 to NumberOfVertices-1)</param>
        /// <returns>Point at the specified parameter location</returns>
        public Point2d GetPointAtParameter(double param)
        {
            // Handle boundary cases
            if (param <= 0)
                return StartPoint;
            if (param + 1 >= NumberOfVertices)
                return EndPoint;

            // Extract integer and fractional parts
            int vertexIndex = (int)Math.Floor(param);
            double fraction = param - vertexIndex;

            PolylineVertex v1 = Vertices[vertexIndex];
            PolylineVertex v2 = Vertices[vertexIndex + 1];

            // Check if this is a straight segment or an arc segment
            if (Math.Abs(v1.Bulge) < 1e-12)
            {
                // Straight line segment - simple linear interpolation
                double segmentLength = v1.GetSegmentLength(v2);
                double angle = v1.Point.AngleTo(v2.Point);

                return PolarPoint(v1.Point, angle, segmentLength * fraction);
            }
            else
            {
                return v1.GetPointOnArcSegment(v2, fraction);
            }
        }
    }
}
