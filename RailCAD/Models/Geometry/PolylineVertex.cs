using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData;
using RailCAD.Common;
using static RailCAD.Common.GeometryHelper;

namespace RailCAD.Models.Geometry
{
    public struct PolylineVertex
    {
        public Point2d Point { get; set; }
        public double Bulge { get; set; }

        public PolylineVertex(Point2d point, double bulge = 0)
        {
            Point = point;
            Bulge = bulge;
        }

        public double GetSegmentLength(PolylineVertex other)
        {
            double chordLength = Point.DistanceTo(other.Point);

            if (Math.Abs(Bulge) < 1e-12)
            {
                // Straight line segment
                return chordLength;
            }
            else
            {
                // Arc segment
                double theta = 4.0 * Math.Atan(Math.Abs(Bulge));
                double radius = chordLength / (2.0 * Math.Sin(theta / 2.0));
                return radius * theta;
            }
        }

        public double GetPartialLength(PolylineVertex v2, Point2d proj)
        {
            if (Math.Abs(Bulge) < 1e-12)
            {
                // line
                return Point.DistanceTo(proj);
            }
            else
            {
                // arc
                double chordLength = Point.DistanceTo(v2.Point);
                double theta = 4.0 * Math.Atan(Math.Abs(Bulge));
                double radius = chordLength / (2.0 * Math.Sin(theta / 2.0));

                GetArcCenter(Point, v2.Point, Bulge, out Point2d center, out double startAng, out double endAng);

                double projAng = center.AngleTo(proj);
                double delta = Math.Abs(NormalizeAngle(projAng - startAng));
                return radius * delta;
            }
        }

        /// <summary>
        /// Returns closest point on segment (line or arc) to specified point.
        /// </summary>
        public Point2d GetClosestPointOnSegment(PolylineVertex v2, Point2d p)
        {
            if (Math.Abs(Bulge) < 1e-12)
            {
                return ClosestPointOnLine(Point, v2.Point, p);
            }
            else
            {
                return ClosestPointOnArc(v2, p);
            }
        }

        public Point2d ClosestPointOnArc(PolylineVertex v2, Point2d p)
        {
            GetArcCenter(Point, v2.Point, Bulge, out Point2d center, out double startAng, out double endAng);
            double radius = Point.DistanceTo(center);

            double angleToP = center.AngleTo(p);

            // clamp to arc
            double arcAngle = NormalizeAngle(endAng - startAng);
            bool ccw = Bulge > 0;

            if (!AngleInRange(angleToP, startAng, endAng, ccw))
            {
                double d1 = p.DistanceTo(Point);
                double d2 = p.DistanceTo(v2.Point);
                return (d1 < d2) ? Point : v2.Point;
            }

            return PolarPoint(center, angleToP, radius);
        }

        /// <summary>
        /// Returns middle point of given polyline segment.
        /// </summary>
        public Point2d GetMiddlePoint(PolylineVertex v2)
        {
            if (Math.Abs(Bulge) < 1e-12)
            {
                // straight line midpoint
                return new Point2d(
                    (Point.X + v2.Point.X) / 2.0,
                    (Point.Y + v2.Point.Y) / 2.0);
            }
            else
            {
                // arc midpoint
                GetArcCenter(Point, v2.Point, Bulge, out Point2d center, out double startAng, out double endAng);
                bool ccw = Bulge > 0;

                // angle between start and end including direction
                double sweep = 4.0 * Math.Atan(Math.Abs(Bulge));
                double midAng = ccw ? startAng + sweep / 2.0 : startAng - sweep / 2.0;

                double radius = Point.DistanceTo(center);
                return PolarPoint(center, midAng, radius);
            }
        }

        /// <summary>
        /// Calculates a point at a fractional position along an arc segment.
        /// </summary>
        /// <param name="other">End vertex of the arc segment</param>
        /// <param name="fraction">Fractional position along the arc (0 = start, 1 = end)</param>
        /// <returns>Point at the specified position on the arc</returns>
        internal Point2d GetPointOnArcSegment(PolylineVertex other, double fraction)
        {
            // Get arc parameters: center, start angle, and end angle
            GetArcCenter(Point, other.Point, Bulge, out Point2d center, out double startAngle, out double endAngle);

            // Calculate the radius
            double radius = Point.DistanceTo(center);

            // Calculate the swept angle (the total angle of the arc)
            double sweptAngle = 4.0 * Math.Atan(Math.Abs(Bulge));

            // Calculate the angle at the fractional position
            double angleAtFraction;
            if (Bulge > 0)
            {
                angleAtFraction = startAngle + sweptAngle * fraction;
            }
            else
            {
                angleAtFraction = startAngle - sweptAngle * fraction;
            }

            return PolarPoint(center, angleAtFraction, radius);
        }

        /// <summary>
        /// Finds intersection between an arc segment (defined by two vertices with bulge) and a circle.
        /// Returns intersection point if it lies on the arc segment, null otherwise.
        /// </summary>
        /// <param name="v1">Start vertex of arc segment</param>
        /// <param name="v2">End vertex of arc segment</param>
        /// <param name="circleCenter">Center of the circle</param>
        /// <param name="circleRadius">Radius of the circle</param>
        /// <returns>Intersection point or null</returns>
        public Point2d? FindArcSegmentCircleIntersection(PolylineVertex v2, Point2d circleCenter, double circleRadius)
        {
            // Get arc parameters from polyline segment
            GetArcCenter(Point, v2.Point, Bulge, out Point2d arcCenter, out double startAngle, out double endAngle);
            double arcRadius = Point.DistanceTo(arcCenter);

            // Find intersections between two circles
            var intersections = FindCircleCircleIntersections(arcCenter, arcRadius, circleCenter, circleRadius);

            if (intersections.Count == 0)
                return null;

            // Check which intersection lies on the arc segment
            bool isCounterClockwise = Bulge > 0;

            foreach (var intersection in intersections)
            {
                double intersectionAngle = arcCenter.AngleTo(intersection);

                // Check if intersection angle is within arc segment bounds
                if (AngleInRange(intersectionAngle, startAngle, endAngle, isCounterClockwise))
                {
                    return intersection;
                }
            }

            return null; // No intersection on arc segment
        }

        /// <summary>
        /// Finds intersection between two polyline segments.
        /// Handles all combinations: line-line, line-arc, arc-line, arc-arc.
        /// </summary>
        /// <param name="seg1V1">First vertex of first segment</param>
        /// <param name="seg1V2">Second vertex of first segment</param>
        /// <param name="seg2V1">First vertex of second segment</param>
        /// <param name="seg2V2">Second vertex of second segment</param>
        /// <returns>Intersection point or null</returns>
        public static Point2d? FindSegmentIntersection(PolylineVertex seg1V1, PolylineVertex seg1V2,
                                                PolylineVertex seg2V1, PolylineVertex seg2V2)
        {
            bool seg1IsArc = Math.Abs(seg1V1.Bulge) >= 1e-12;
            bool seg2IsArc = Math.Abs(seg2V1.Bulge) >= 1e-12;

            if (!seg1IsArc && !seg2IsArc)
            {
                // Line-Line intersection
                return FindLineLineIntersection(seg1V1.Point, seg1V2.Point, seg2V1.Point, seg2V2.Point);
            }
            else if (!seg1IsArc && seg2IsArc)
            {
                // Line-Arc intersection
                return FindLineArcIntersection(seg1V1.Point, seg1V2.Point, seg2V1, seg2V2);
            }
            else if (seg1IsArc && !seg2IsArc)
            {
                // Arc-Line intersection (swap parameters)
                return FindLineArcIntersection(seg2V1.Point, seg2V2.Point, seg1V1, seg1V2);
            }
            else
            {
                // Arc-Arc intersection
                return FindArcArcIntersection(seg1V1, seg1V2, seg2V1, seg2V2);
            }
        }

        /// <summary>
        /// Finds intersection between a line segment and an arc segment.
        /// Returns intersection point if it lies on both segments, null otherwise.
        /// </summary>
        public static Point2d? FindLineArcIntersection(Point2d lineStart, Point2d lineEnd,
                                                PolylineVertex arcV1, PolylineVertex arcV2)
        {
            // Get arc parameters
            GetArcCenter(arcV1.Point, arcV2.Point, arcV1.Bulge,
                                       out Point2d arcCenter, out double startAngle, out double endAngle);
            double arcRadius = arcV1.Point.DistanceTo(arcCenter);

            // Find intersections between line and arc circle
            var intersections = FindLineCircleIntersections(lineStart, lineEnd, arcCenter, arcRadius);

            bool isCounterClockwise = arcV1.Bulge > 0;

            // Check which intersection lies on both the line segment and arc segment
            foreach (var intersection in intersections)
            {
                if (IsPointOnLineSegment(lineStart, lineEnd, intersection))
                {
                    double intersectionAngle = arcCenter.AngleTo(intersection);
                    if (AngleInRange(intersectionAngle, startAngle, endAngle, isCounterClockwise))
                    {
                        return intersection;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds intersection between two arc segments.
        /// Returns intersection point if it lies on both arc segments, null otherwise.
        /// </summary>
        public static Point2d? FindArcArcIntersection(PolylineVertex arc1V1, PolylineVertex arc1V2,
                                                      PolylineVertex arc2V1, PolylineVertex arc2V2)
        {
            // Get parameters for first arc
            GetArcCenter(arc1V1.Point, arc1V2.Point, arc1V1.Bulge,
                                       out Point2d arc1Center, out double arc1StartAngle, out double arc1EndAngle);
            double arc1Radius = arc1V1.Point.DistanceTo(arc1Center);
            bool arc1IsCounterClockwise = arc1V1.Bulge > 0;

            // Get parameters for second arc
            GetArcCenter(arc2V1.Point, arc2V2.Point, arc2V1.Bulge,
                                       out Point2d arc2Center, out double arc2StartAngle, out double arc2EndAngle);
            double arc2Radius = arc2V1.Point.DistanceTo(arc2Center);
            bool arc2IsCounterClockwise = arc2V1.Bulge > 0;

            // Find intersections between two circles
            var intersections = FindCircleCircleIntersections(arc1Center, arc1Radius, arc2Center, arc2Radius);

            // Check which intersection lies on both arc segments
            foreach (var intersection in intersections)
            {
                double intersection1Angle = arc1Center.AngleTo(intersection);
                double intersection2Angle = arc2Center.AngleTo(intersection);

                if (AngleInRange(intersection1Angle, arc1StartAngle, arc1EndAngle, arc1IsCounterClockwise) &&
                    AngleInRange(intersection2Angle, arc2StartAngle, arc2EndAngle, arc2IsCounterClockwise))
                {
                    return intersection;
                }
            }

            return null;
        }

        /// <summary>
        /// Closest point between two polyline segments.
        /// </summary>
        public static List<Point2d> ClosestPointsBetweenSegments(PolylineVertex a1, PolylineVertex a2,
                                                                 PolylineVertex b1, PolylineVertex b2)
        {
            bool arcA = Math.Abs(a1.Bulge) > 1e-12;
            bool arcB = Math.Abs(b1.Bulge) > 1e-12;

            if (!arcA && !arcB)
            {
                // line-line
                return ClosestPointsLineLine(a1.Point, a2.Point, b1.Point, b2.Point);
            }
            else if (arcA && !arcB)
            {
                // arc-line
                return ClosestPointsArcLine(a1, a2, b1.Point, b2.Point);
            }
            else if (!arcA && arcB)
            {
                // line-arc (swap order)
                var swapped = ClosestPointsArcLine(b1, b2, a1.Point, a2.Point);
                return new List<Point2d> { swapped[1], swapped[0] };
            }
            else
            {
                // arc-arc
                return ClosestPointsArcArc(a1, a2, b1, b2);
            }
        }

        /// <summary>
        /// Closest points between an arc (defined by arcV1->arcV2; bulge stored in arcV1) and a line segment (lineP1-lineP2).
        /// Returns list (pointOnArc, pointOnLine).
        /// </summary>
        public static List<Point2d> ClosestPointsArcLine(PolylineVertex arcV1, PolylineVertex arcV2,
                                                         Point2d lineP1, Point2d lineP2)
        {
            // Get arc centre and angles
            GetArcCenter(arcV1.Point, arcV2.Point, arcV1.Bulge, out Point2d center, out double startAng, out double endAng);
            double radius = arcV1.Point.DistanceTo(center);
            bool ccw = arcV1.Bulge > 0;

            // Multiple candidate pairs to consider
            var candidates = new List<List<Point2d>>();

            // 1) Project circle center to the infinite line -> closest direction
            Point2d projInfinite = ClosestPointOnLine(lineP1, lineP2, center);

            // 2) If projection is on the line segment, check if the corresponding arc point is valid
            if (IsPointOnLineSegment(lineP1, lineP2, projInfinite))
            {
                double ang = center.AngleTo(projInfinite);
                if (AngleInRange(ang, startAng, endAng, ccw))
                {
                    Point2d pointOnArc = PolarPoint(center, ang, radius);
                    candidates.Add(new List<Point2d> { pointOnArc, projInfinite });
                }
            }

            // 3) Check arc endpoints against line segment
            Point2d closestToStart = ClosestPointOnLine(lineP1, lineP2, arcV1.Point);
            if (IsPointOnLineSegment(lineP1, lineP2, closestToStart))
            {
                candidates.Add(new List<Point2d> { arcV1.Point, closestToStart });
            }

            Point2d closestToEnd = ClosestPointOnLine(lineP1, lineP2, arcV2.Point);
            if (IsPointOnLineSegment(lineP1, lineP2, closestToEnd))
            {
                candidates.Add(new List<Point2d> { arcV2.Point, closestToEnd });
            }

            // 4) Check line endpoints against arc
            Point2d arcClosestToLineP1 = arcV1.ClosestPointOnArc(arcV2, lineP1);
            candidates.Add(new List<Point2d> { arcClosestToLineP1, lineP1 });

            Point2d arcClosestToLineP2 = arcV1.ClosestPointOnArc(arcV2, lineP2);
            candidates.Add(new List<Point2d> { arcClosestToLineP2, lineP2 });

            // 5) If no good candidates, fallback to endpoint combinations
            if (candidates.Count == 0)
            {
                candidates.Add(new List<Point2d> { arcV1.Point, lineP1 });
                candidates.Add(new List<Point2d> { arcV1.Point, lineP2 });
                candidates.Add(new List<Point2d> { arcV2.Point, lineP1 });
                candidates.Add(new List<Point2d> { arcV2.Point, lineP2 });
            }

            // Return the pair with minimum distance
            return candidates.OrderBy(c => c[0].DistanceTo(c[1])).First();
        }

        /// <summary>
        /// Closest points between two arc segments (each arc defined by v1->v2, bulge stored in v1).
        /// Returns list (pointOnArcA, pointOnArcB).
        /// </summary>
        public static List<Point2d> ClosestPointsArcArc(PolylineVertex a1, PolylineVertex a2,
                                                        PolylineVertex b1, PolylineVertex b2)
        {
            // Arc A parameters
            GetArcCenter(a1.Point, a2.Point, a1.Bulge, out Point2d centerA, out double startA, out double endA);
            double radiusA = a1.Point.DistanceTo(centerA);
            bool ccwA = a1.Bulge > 0;

            // Arc B parameters
            GetArcCenter(b1.Point, b2.Point, b1.Bulge, out Point2d centerB, out double startB, out double endB);
            double radiusB = b1.Point.DistanceTo(centerB);
            bool ccwB = b1.Bulge > 0;

            double distCenters = centerA.DistanceTo(centerB);

            var candidates = new List<List<Point2d>>();

            // Handle concentric case (same center)
            if (distCenters < 1e-12)
            {
                // Fallback to all endpoint combinations for concentric arcs
                candidates.Add(new List<Point2d> { a1.Point, b1.Point });
                candidates.Add(new List<Point2d> { a1.Point, b2.Point });
                candidates.Add(new List<Point2d> { a2.Point, b1.Point });
                candidates.Add(new List<Point2d> { a2.Point, b2.Point });
                return candidates.OrderBy(c => c[0].DistanceTo(c[1])).First();
            }

            // Base direction from A center to B center
            double baseAng = centerA.AngleTo(centerB);

            // Two natural candidate directions (towards each other and away from each other)
            double[] directions = { baseAng, baseAng + Math.PI };

            foreach (double dir in directions)
            {
                double dirA = dir;
                double dirB = dir + Math.PI; // opposite direction for arc B

                // Check if these directions give valid points on both arcs
                if (AngleInRange(dirA, startA, endA, ccwA) &&
                    AngleInRange(dirB, startB, endB, ccwB))
                {
                    Point2d pointA = PolarPoint(centerA, dirA, radiusA);
                    Point2d pointB = PolarPoint(centerB, dirB, radiusB);
                    candidates.Add(new List<Point2d> { pointA, pointB });
                }
            }

            // Add endpoint-to-arc candidates
            Point2d b1ClosestToA1 = b1.ClosestPointOnArc(b2, a1.Point);
            candidates.Add(new List<Point2d> { a1.Point, b1ClosestToA1 });

            Point2d b1ClosestToA2 = b1.ClosestPointOnArc(b2, a2.Point);
            candidates.Add(new List<Point2d> { a2.Point, b1ClosestToA2 });

            Point2d a1ClosestToB1 = a1.ClosestPointOnArc(a2, b1.Point);
            candidates.Add(new List<Point2d> { a1ClosestToB1, b1.Point });

            Point2d a1ClosestToB2 = a1.ClosestPointOnArc(a2, b2.Point);
            candidates.Add(new List<Point2d> { a1ClosestToB2, b2.Point });

            // If still no candidates, use all endpoint combinations
            if (candidates.Count == 0)
            {
                candidates.Add(new List<Point2d> { a1.Point, b1.Point });
                candidates.Add(new List<Point2d> { a1.Point, b2.Point });
                candidates.Add(new List<Point2d> { a2.Point, b1.Point });
                candidates.Add(new List<Point2d> { a2.Point, b2.Point });
            }

            return candidates.OrderBy(c => c[0].DistanceTo(c[1])).First();
        }
    }
}
