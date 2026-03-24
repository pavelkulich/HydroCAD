using System;
using System.Collections.Generic;

using static RailCAD.Common.GeometryHelper;

namespace RailCAD.Models.Geometry
{
    public class RCArc
    {
        public string Handle { get; }
        public Point2d Center { get; }
        public double Radius { get; }
        public double StartAngle { get; }
        public double EndAngle { get; }
        public double TotalAngle { get; }
        public Point2d StartPoint { get; }
        public Point2d EndPoint { get; }

        public Point2d MiddlePoint => ArcMiddlePoint(Radius, StartPoint, EndPoint);

        public RCArc(Point2d center, double radius, double startAngle, double endAngle, string handle = "0")
        {
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            EndAngle = endAngle;
            TotalAngle = NormalizeAngle(endAngle - startAngle);
            StartPoint = PolarPoint(center, startAngle, radius);
            EndPoint = PolarPoint(center, endAngle, radius);
            Handle = handle;
        }

        public Point2d ArcMiddlePoint(double radius, Point2d startPoint, Point2d endPoint)
        {
            double startAngle = Center.AngleTo(startPoint);
            double endAngle = Center.AngleTo(endPoint);
            double totalAngle = NormalizeAngle(endAngle - startAngle);
            var angle = startAngle + totalAngle / 2;
            return PolarPoint(Center, angle, radius);
        }

        /// <summary>
        /// Calculate distance from point to arc
        /// </summary>
        public double DistanceTo(Point2d point)
        {
            double distanceToCenter = Center.DistanceTo(point);
            return Math.Abs(distanceToCenter - Radius);
        }

        /// <summary>
        /// Calculate closest point to arc
        /// </summary>
        public Point2d GetClosestPointTo(Point2d point)
        {
            double angle = Center.AngleTo(point);
            Point2d result = PolarPoint(Center, angle, Radius);
            if (AngleInRange(angle, StartAngle, EndAngle, true)) // point lies on arc
            {
                return result;
            }
            else
            {
                if (point.DistanceTo(StartPoint) < point.DistanceTo(EndPoint)) // return closest end point
                {
                    return StartPoint;
                }
                else
                {
                    return EndPoint;
                }
            }
        }

        /// <summary>
        /// Finds intersection between this arc and a circle.
        /// If no test point is provided, returns intersection only if it lies on the arc.
        /// If test point is provided, returns the intersection closest to the test point (even if outside arc bounds).
        /// </summary>
        /// <param name="arcRadius">Radius of the arc</param>
        /// <param name="circleCenter">Center of the circle</param>
        /// <param name="circleRadius">Radius of the circle</param>
        /// <param name="testPoint">Optional test point to find closest intersection</param>
        /// <returns>Intersection point or null if no valid intersection found</returns>
        public Point2d? IntersectArcWithCircle(double arcRadius, Point2d circleCenter, double circleRadius, Point2d? testPoint = null)
        {
            // Get all intersection points between the arc circle and the given circle
            var intersections = FindCircleCircleIntersections(Center, arcRadius, circleCenter, circleRadius);

            if (intersections.Count == 0)
                return null;

            if (testPoint == null)
            {
                // No test point provided - return first intersection that lies on the arc
                foreach (var intersection in intersections)
                {
                    if (IsPointOnArc(intersection))
                        return intersection;
                }
                return null;
            }
            else
            {
                // Test point provided - find closest intersection
                Point2d closestIntersection = intersections[0];
                double minDistance = testPoint.Value.DistanceTo(intersections[0]);

                for (int i = 1; i < intersections.Count; i++)
                {
                    double distance = testPoint.Value.DistanceTo(intersections[i]);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestIntersection = intersections[i];
                    }
                }

                return closestIntersection;
            }
        }

        /// <summary>
        /// Checks if a point lies on this arc (within the arc's angular bounds)
        /// </summary>
        /// <param name="point">Point to check</param>
        /// <param name="tolerance">Angular tolerance in radians</param>
        /// <returns>True if point lies on the arc</returns>
        public bool IsPointOnArc(Point2d point, double tolerance = 1e-6)
        {
            // Check if point is at the correct distance from center
            double distanceFromCenter = Center.DistanceTo(point);
            if (!EqualsWithTol(distanceFromCenter, Radius, tolerance))
                return false;

            // Calculate angle from center to point
            double pointAngle = Center.AngleTo(point);

            // Check if angle is within arc bounds
            // Arc direction is determined by comparing start and end angles
            bool isCounterClockwise = TotalAngle > 0;

            return AngleInRange(pointAngle, StartAngle, EndAngle, isCounterClockwise);
        }
    }
}
