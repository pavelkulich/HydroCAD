using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.Common;

namespace HydroCAD.Models.Geometry
{
    /// <summary>
    /// 2D polyline with optional arc segments (bulge).
    /// Used to represent pipe routes, alignment lines, etc.
    /// </summary>
    public class HCPolyline
    {
        private List<PolylineVertex> vertices;

        public string Handle { get; internal set; }

        public List<PolylineVertex> Vertices
        {
            get => vertices;
            set { if (value != null && value.Count > 0) vertices = value; }
        }

        public int NumberOfVertices => Vertices?.Count ?? 0;

        public double Length
        {
            get
            {
                if (NumberOfVertices == 0) return 0;
                return GetLengthToVertex(NumberOfVertices - 1);
            }
        }

        public List<Point2d> Points
        {
            get
            {
                if (NumberOfVertices == 0) return null;
                return Vertices.Select(v => v.Point).ToList();
            }
        }

        public Point2d StartPoint => NumberOfVertices > 0 ? Vertices[0].Point : new Point2d(0, 0);
        public Point2d EndPoint => NumberOfVertices > 0 ? Vertices[NumberOfVertices - 1].Point : new Point2d(0, 0);

        public HCPolyline(List<PolylineVertex> newVertices = null, string handle = "0")
        {
            vertices = newVertices ?? new List<PolylineVertex>();
            Handle = handle;
        }

        public HCPolyline(List<Point2d> points)
        {
            vertices = new List<PolylineVertex>();
            Handle = "0";
            foreach (var point in points)
                vertices.Add(new PolylineVertex(point));
        }

        /// <summary>
        /// Returns the cumulative 2D length from the first vertex to the given vertex index.
        /// </summary>
        public double GetLengthToVertex(int vertexIndex)
        {
            if (vertexIndex <= 0 || NumberOfVertices < 2) return 0;
            vertexIndex = Math.Min(vertexIndex, NumberOfVertices - 1);

            double total = 0;
            for (int i = 0; i < vertexIndex; i++)
            {
                total += SegmentLength(i);
            }
            return total;
        }

        private double SegmentLength(int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= NumberOfVertices - 1) return 0;
            Point2d p1 = Vertices[segmentIndex].Point;
            Point2d p2 = Vertices[segmentIndex + 1].Point;
            double bulge = Vertices[segmentIndex].Bulge;

            if (Math.Abs(bulge) < 1e-10)
            {
                return p1.DistanceTo(p2);
            }
            else
            {
                // Arc segment length: L = r * theta
                double theta = 4.0 * Math.Atan(Math.Abs(bulge));
                double chordLength = p1.DistanceTo(p2);
                double radius = chordLength / (2.0 * Math.Sin(theta / 2.0));
                return radius * theta;
            }
        }

        /// <summary>
        /// Returns 2D point on the polyline at the specified distance from the start.
        /// </summary>
        public Point2d GetPointAtDistance(double distance)
        {
            if (NumberOfVertices == 0) return new Point2d(0, 0);
            if (distance <= 0) return StartPoint;
            if (distance >= Length) return EndPoint;

            double accumulated = 0;
            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                double segLen = SegmentLength(i);
                if (accumulated + segLen >= distance)
                {
                    double t = (distance - accumulated) / segLen;
                    Point2d p1 = Vertices[i].Point;
                    Point2d p2 = Vertices[i + 1].Point;
                    return new Point2d(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                }
                accumulated += segLen;
            }
            return EndPoint;
        }

        /// <summary>
        /// Returns the closest point on the polyline to the given point, plus its distance parameter.
        /// </summary>
        public (Point2d closestPoint, double distanceAlongPolyline) GetClosestPointTo(Point2d point)
        {
            double bestDist = double.MaxValue;
            Point2d bestPoint = StartPoint;
            double bestDistAlong = 0;
            double accumulated = 0;

            for (int i = 0; i < NumberOfVertices - 1; i++)
            {
                Point2d p1 = Vertices[i].Point;
                Point2d p2 = Vertices[i + 1].Point;
                Point2d closest = GeometryHelper.ClosestPointOnLine(p1, p2, point);
                double dist = point.DistanceTo(closest);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPoint = closest;
                    bestDistAlong = accumulated + p1.DistanceTo(closest);
                }
                accumulated += SegmentLength(i);
            }

            return (bestPoint, bestDistAlong);
        }

        /// <summary>
        /// Samples the polyline at regular intervals and returns (station, point) pairs.
        /// </summary>
        public IList<(double station, Point2d point)> SampleAtInterval(double interval)
        {
            var result = new List<(double, Point2d)>();
            double totalLength = Length;
            if (totalLength <= 0 || interval <= 0) return result;

            result.Add((0, StartPoint));

            double distance = interval;
            while (distance < totalLength)
            {
                result.Add((distance, GetPointAtDistance(distance)));
                distance += interval;
            }

            if (result.Last().Item1 < totalLength - 1e-6)
                result.Add((totalLength, EndPoint));

            return result;
        }

        public void ReversePolyline()
        {
            vertices.Reverse();
        }

        public override string ToString() => $"HCPolyline[{NumberOfVertices} pts, L={Length:F2}]";
    }
}
