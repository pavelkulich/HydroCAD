using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Models.Alignment;
using RailCAD.Models.Geometry;
using static RailCAD.Common.GeometryHelper;

namespace RailCAD.MainApp
{
    partial class RCApp
    {
        private static void DrawTramProfile(ICadModel cad)
        {
            cad.DrawTramProfile();
        }

        private static void ClosestPolylineDistance(ICadModel cad)
        {
            cad.ClosestPolylineDistance();
        }
    }

    /// <summary>
    /// Profile type enumeration for XData
    /// </summary>
    internal enum ProfileType
    {
        Inner = -1,
        Outer = 1
    }

    /// <summary>
    /// Data table for tram profile widening values based on curve radius
    /// </summary>
    internal static class TramProfileData
    {
        private static List<TramDataEntry> _data;
        private static bool _initialized = false;
        private const string fileName = "tramdata.lsp";

        static TramProfileData()
        {
            LoadData();
        }

        /// <summary>
        /// Read data table from tramdata.lsp.
        /// </summary>
        private static void LoadData()
        {
            if (_initialized) return;
            _data = new List<TramDataEntry>();

            try
            {
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string parentPath = Directory.GetParent(assemblyPath)?.FullName;  // assembly is in subfolder
                string filePath = Path.Combine(parentPath, fileName);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException(String.Format(Properties.Resources.TramProfile_ErrorFileHasNotBeenFound, fileName), filePath);

                foreach (var rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
                {
                    string trimmed = rawLine.Trim();

                    // skip empty or comment lines
                    if (string.IsNullOrEmpty(trimmed))// || !trimmed.StartsWith("("))
                        continue;

                    var matches = Regex.Matches(trimmed, @"[0-9]+(\.[0-9]+)?");

                    if (matches.Count >= 3)
                    {
                        double radius = double.Parse(matches[0].Value, CultureInfo.InvariantCulture);
                        double inner = double.Parse(matches[1].Value, CultureInfo.InvariantCulture);
                        double outer = double.Parse(matches[2].Value, CultureInfo.InvariantCulture);

                        _data.Add(new TramDataEntry(radius, inner, outer));
                    }
                }

                // Sort by radius
                _data.Sort((a, b) => a.Radius.CompareTo(b.Radius));

                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(String.Format(Properties.Resources.TramProfile_ErrorWhileLoadingFile, fileName) + ex.Message, ex);
            }
        }

        internal static double MinRadius => _data.First().Radius;
        internal static double MaxRadius => _data.Last().Radius;

        /// <summary>
        /// Calculate profile widening for given radius
        /// </summary>
        /// <param name="radius">Curve radius in meters</param>
        /// <param name="wideningType">-1 = inner widening, 1 = outer widening</param>
        /// <returns>Widening value in meters</returns>
        internal static double GetWidening(double radius, int wideningType)
        {
            if (radius < MinRadius)
                return 0.0;

            if (radius >= MaxRadius)
                return 0.0;

            // Find interpolation bounds
            var lowerEntry = _data.LastOrDefault(d => d.Radius <= radius);
            var upperEntry = _data.FirstOrDefault(d => d.Radius > radius);

            //if (lowerEntry == null || upperEntry == null)
            //    return 0.0;

            // Linear interpolation
            double lowerValue = wideningType == -1 ? lowerEntry.InnerWidening : lowerEntry.OuterWidening;
            double upperValue = wideningType == -1 ? upperEntry.InnerWidening : upperEntry.OuterWidening;

            double factor = (radius - lowerEntry.Radius) / (upperEntry.Radius - lowerEntry.Radius);
            double interpolated = lowerValue - (lowerValue - upperValue) * factor;

            // Round up to 5mm precision
            return Math.Ceiling(interpolated / 0.005) * 0.005;
        }

        private readonly struct TramDataEntry
        {
            public double Radius { get; }
            public double InnerWidening { get; }
            public double OuterWidening { get; }

            public TramDataEntry(double radius, double innerWidening, double outerWidening)
            {
                Radius = radius;
                InnerWidening = innerWidening;
                OuterWidening = outerWidening;
            }
        }
    }

    /// <summary>
    /// CAD-independent part of tram profile generator class
    /// </summary>
    internal static class TramProfileGenerator
    {
        // Constants
        private const double K = 1.35;          // Half of basic vehicle width
        private const double DK = 0.4;          // Distance between vehicle outline and clearance profile
        private const double LE1_DIST = 1.5;    // Extension distance 1
        private const double LE2_DIST = 10.0;   // Extension distance 2
        private const double LE3_DIST = 3.0;    // Extension distance 3
        private const double LI_DIST = 7.5;     // Linear extension distance
        private const double LI2_DIST = 6.0;    // Linear extension distance 2
        private const double DI_COEFF = 0.67;   // Inner widening coefficient
        private const double SI_DIST = 3.75;    // Side distance
        internal const double TOL = 1e-4;       // Basic precision for equality testing

        /// <summary>
        /// Universal method for generating tram profiles for any arc configuration
        /// Handles tangent lines, spirals, and arc-to-arc connections
        /// </summary>
        /// <param name="curves">Dictionary of arc and transition configuration for start and end</param>
        /// <param name="allProfiles">All profiles of all arcs in the compound curve</param>
        /// <returns>Dictionary of arc and an array of two profile polylines [inner, outer]</returns>
        internal static Dictionary<string, RCPolyline[]> GenerateTramProfiles(List<RCCurve> curves, Dictionary<string, RCPolyline[]> allProfiles = null)
        {
            if (curves == null) return null;
            allProfiles = allProfiles ?? new Dictionary<string, RCPolyline[]>();
            var count = curves.Count;

            for (int i = 1; i < count; i++)
            {
                var curve = curves[i];
                var arc = curve.Arc;

                // For arc-to-arc, we need special transition logic
                allProfiles = GenerateArcToArcProfiles(arc, curve.Tangent1.Arc, curve, curves[i - 1], allProfiles);
                if (i == count - 1 && arc.Radius < TramProfileData.MaxRadius) // last arc (from right to left)
                {
                    allProfiles[curve.Handle] = GenerateArcProfiles(arc, curve, allProfiles[curve.Handle]); // runout on the left
                }
            }

            // 1st arc (from right to left) - runout on the right
            var curve1 = curves[0];
            var arc1 = curve1.Arc;
            allProfiles[curve1.Handle] = GenerateArcProfiles(arc1, curve1, allProfiles[curve1.Handle]);

            return allProfiles;
        }

        /// <summary>
        /// Generate profiles with spiral transitions
        /// </summary>
        private static RCPolyline[] GenerateArcProfiles(RCArc arc, RCCurve curve, RCPolyline[] profiles)
        {
            // Calculate widening values
            double innerWidening = TramProfileData.GetWidening(arc.Radius, -1);
            double outerWidening = TramProfileData.GetWidening(arc.Radius, 1);
            double innerRadius = arc.Radius - K - innerWidening;
            double outerRadius = arc.Radius + K + outerWidening;

            profiles[0] = CreateArcProfile(arc, curve, profiles[0], innerRadius, innerWidening, ProfileType.Inner);
            profiles[1] = CreateArcProfile(arc, curve, profiles[1], outerRadius, outerWidening, ProfileType.Outer);

            return profiles;
        }

        /// <summary>
        /// Create profile polyline with spiral or tangent runouts
        /// </summary>
        private static RCPolyline CreateArcProfile(RCArc arc, RCCurve curve, RCPolyline polyline, double profileRadius, double widening,
            ProfileType profileType)
        {
            polyline = polyline ?? new RCPolyline();
            int vertexIndex = 0;
            double k = profileType == ProfileType.Inner ? -K - widening : K + widening;
            var startArc = curve.Tangent1.Arc;
            var endArc = curve.Tangent2.Arc;
            var startSpiral = startArc != null ? null : curve.Spiral1.Polyline;
            var endSpiral = endArc != null ? null : curve.Spiral2.Polyline;

            var startPoint = PolarPoint(arc.Center, arc.StartAngle, profileRadius);
            var endPoint = PolarPoint(arc.Center, arc.EndAngle, profileRadius);
            bool addArc = true;
            bool trim = false;

            // Add start transition runout if exists
            if (widening < TOL && startSpiral == null && polyline.NumberOfVertices == 0)
            {
                polyline.AddVertex(0, startPoint, 0); // no runout - curve without widening
                vertexIndex = 1;
            }
            else if (startArc == null)
            {
                vertexIndex = endArc != null ? -polyline.NumberOfVertices : 0; // 1st arc of the compound curve
                polyline = startSpiral != null ?
                    CreateSpiralRunout(curve, polyline, 0, widening, profileType, true, vertexIndex, endArc != null, out trim) :
                    CreateTangentRunout(curve, polyline, profileRadius, widening, profileType, true, vertexIndex, endArc != null, out trim);

                if (trim)
                {
                    addArc = false;
                    vertexIndex = polyline.NumberOfVertices;
                }
                else
                {
                    vertexIndex = vertexIndex < 0 ? polyline.NumberOfVertices + vertexIndex : polyline.NumberOfVertices;
                }
                startPoint = polyline.GetPoint2dAt(vertexIndex - 1); // append to the profile on the right
            }
            else if (polyline.NumberOfVertices > 0) // last arc of a compound curve
            {
                // no start runout
                vertexIndex = polyline.NumberOfVertices;
                startPoint = polyline.GetPoint2dAt(vertexIndex - 1); // append to the profile on the right
            }

            // Add end transition runout if exists
            if (widening < TOL && endSpiral == null)
            {
                polyline.AddVertex(vertexIndex, endPoint, vertexIndex); // no runout - curve without widening
            }
            else if (endArc == null)
            {
                polyline = endSpiral != null ?
                    CreateSpiralRunout(curve, polyline, 0, widening, profileType, false, vertexIndex, startArc != null, out trim) :
                    CreateTangentRunout(curve, polyline, profileRadius, widening, profileType, false, vertexIndex, startArc != null, out trim);

                if (trim)
                {
                    addArc = false;
                }
                else
                {
                    endPoint = polyline.GetPoint2dAt(vertexIndex);
                }
            }
            else if (addArc) // 1st arc of the compound curve
            {
                addArc = profileType == ProfileType.Inner;
                endPoint = polyline.GetPoint2dAt(vertexIndex);
            }

            // Check if inner runouts intersect - no arc section
            if (addArc && polyline.NumberOfVertices > vertexIndex + 1 && profileType == ProfileType.Inner)
            {
                if (polyline.SplitAndTrimOverlap(vertexIndex))
                {
                    addArc = false;
                }
            }

            // Calculate bulge for arc section
            if (addArc)
            {
                var arcMidPoint = arc.ArcMiddlePoint(profileRadius, startPoint, endPoint);
                double bulge = CalculateBulge(arcMidPoint, startPoint, endPoint);
                polyline.SetBulgeAt(vertexIndex - 1, bulge);
            }

            return polyline;
        }

        /// <summary>
        /// Create spiral runout points with linear widening
        /// </summary>
        private static RCPolyline CreateSpiralRunout(RCCurve curve, RCPolyline polyline, double startWidening, double widening,
            ProfileType profileType, bool isStart, int vertexIndex, bool intersect, out bool trim)
        {
            RCArc arc = curve.Arc;
            RCPolyline spiral = isStart ? curve.Spiral1.Polyline : curve.Spiral2.Polyline;
            bool isInner = profileType == ProfileType.Inner;
            trim = false;
            polyline = polyline ?? new RCPolyline();
            var tempPolyline = new RCPolyline();
            double profileRadius = arc.Radius + (isInner ? -1 : 1) * ( K + Math.Abs(widening));
            bool isReversed = vertexIndex < 0;
            vertexIndex = isReversed ? Math.Abs(vertexIndex) : vertexIndex;

            var testPoint = PolarPoint(arc.Center, isStart ? arc.StartAngle : arc.EndAngle, profileRadius);
            var offset = spiral.OffsetCurve(K + startWidening + widening, testPoint);
            if (offset == null) return null;

            if (widening > 1e-4)
            {
                var correctionAngle = isStart ? (isInner ? Math.PI * 0.5 : Math.PI * 1.5) : (isInner ? Math.PI * 1.5 : Math.PI * 0.5);

                double offsetLength = offset.Length;
                bool tangentArc = (isStart ? curve.Tangent1 : curve.Tangent2).Arc != null;
                double totalLength = isInner ? (tangentArc ? 2 * SI_DIST : LI2_DIST) + offsetLength : LI2_DIST - LE3_DIST + offsetLength;
                double speed = widening / totalLength;
                int index = 0;
                double angle;

                Point2d SpiralRunoutPoint(double param)
                {
                    var spiralPoint = spiral.GetPointAtParameter(param);
                    var offsetPoint = offset.GetPointAtParameter(param);
                    double dist = offset.GetDistanceAtParameter(param) + (isInner && tangentArc ? SI_DIST : LI2_DIST);
                    double tempAngle = spiralPoint.AngleTo(offsetPoint);
                    return PolarPoint(spiralPoint, tempAngle, K + startWidening + speed * dist);
                }

                // linear part of runout
                if (!tangentArc) // intermediate spiral - no linear part
                {
                    var vector = (isStart ? curve.Tangent1 : curve.Tangent2).Direction;
                    angle = vector.AngleOnXY();
                    tempPolyline.AddVertex(0, PolarPoint(PolarPoint(spiral.StartPoint, angle + Math.PI, LI2_DIST), angle + correctionAngle, K), 0);
                    index++;
                }

                // curved part of runout - spirals are oriented towards the arc
                bool isEnd = false;
                var point = SpiralRunoutPoint(0);
                var point0 = point;
                double param0 = 0;
                double bulge = 0;
                for (double param = 1; param < spiral.NumberOfVertices; param++)
                {
                    double dist = offset.GetDistanceAtParameter(param) + (isInner && tangentArc ? SI_DIST : LI2_DIST);
                    if (isInner || dist < totalLength)
                    {
                        double param1 = param - 0.5;
                        var point1 = SpiralRunoutPoint(param1);
                        point = SpiralRunoutPoint(param);
                        bulge = CalculateBulge(point1, point0, point);
                        tempPolyline.AddVertex(index++, point0, bulge);
                    }
                    else if (!isEnd)
                    {
                        point = offset.GetPointAtDistance(offset.Length - LE3_DIST);
                        param = offset.GetParameterAtPoint(point);

                        if (param - param0 > 1e-4)  // length of segment before the end of linear widening is not zero
                        {
                            double param1 = (param + param0) / 2;
                            var point1 = SpiralRunoutPoint(param1);
                            bulge = CalculateBulge(point1, point0, point);
                            tempPolyline.AddVertex(index++, point0, bulge);
                            point0 = point;
                            // remaing part of segment after the end of linear widening
                            param1 = (param + param0 + 1) / 2;
                            point1 = offset.GetPointAtParameter(param1);
                            point = offset.GetPointAtParameter(param0 + 1);
                            bulge = CalculateBulge(point1, point0, point);
                        }
                        else
                        {
                            point0 = point;
                        }
                        tempPolyline.AddVertex(index++, point0, bulge);

                        isEnd = true;
                        param = param0 + 1;
                    }
                    else // outer widening - full widening at the end of spiral
                    {
                        tempPolyline.Vertices.Add(offset.Vertices[(int)param0]);
                        index++;
                    }

                    point0 = point;
                    param0 = param;
                }

                point = profileType == ProfileType.Inner ? SpiralRunoutPoint(param0) : offset.EndPoint;
                tempPolyline.AddVertex(index++, point, 0);
            }
            else
            {
                tempPolyline = offset;
            }

            if (intersect) // intersect with arc to arc transition
            {
                var intersection = polyline.IntersectPolylines(tempPolyline);
                if (intersection != null)
                {
                    polyline.RemovePolylinePoints(intersection.Value, isReversed); // remove points after/ before intersection
                    tempPolyline.RemovePolylinePoints(intersection.Value, false);
                    vertexIndex = polyline.NumberOfVertices;
                    trim = true;
                }
            }

            if (!isStart)
            {
                tempPolyline.ReversePolyline();
            }
            polyline.AddVertices(tempPolyline.Vertices, vertexIndex, isReversed);
            polyline.RemovePolylinePoints(vertexIndex + tempPolyline.NumberOfVertices, false);

            return polyline;
        }

        /// <summary>
        /// Create extended profile with widening runout at start or end of an arc.
        /// </summary>
        private static RCPolyline CreateTangentRunout(RCCurve curve, RCPolyline polyline, double profileRadius, double widening, ProfileType profileType,
            bool isStart, int vertexIndex, bool intersect, out bool trim)
        {
            RCArc arc = curve.Arc;
            trim = false;
            if (arc == null) return null;
            polyline = polyline ?? new RCPolyline();
            bool isReversed = vertexIndex < 0;
            vertexIndex = isReversed ? Math.Abs(vertexIndex) : vertexIndex;
            var points = new List<Point2d>();

            // Calculate parameters
            var startPoint = isStart ? arc.StartPoint : arc.EndPoint;
            var endPoint = isStart ? arc.EndPoint : arc.StartPoint;
            var startAngle = isStart ? arc.StartAngle : arc.EndAngle;
            var correctionAngle = isStart ? Math.PI * 1.5 : Math.PI * 0.5;
            if (profileType == ProfileType.Inner)
            {
                double k = -K;

                // linear section of runout
                var basicPnt = PolarPoint(startPoint, startAngle, k);
                var startPnt = PolarPoint(basicPnt, startAngle + correctionAngle, LI_DIST);
                var middlePnt = PolarPoint(startPoint, startAngle, k - widening * DI_COEFF);
                points.Add(startPnt);

                // nonlinear section of runout
                Point2d? intersection;
                if (intersect) // intersection with arc to arc transition
                {
                    intersection = polyline.IntersectWithCircle(middlePnt, SI_DIST);
                    if (intersection == null)
                    {
                        intersection = arc.IntersectArcWithCircle(profileRadius, middlePnt, SI_DIST, endPoint);
                    }
                    else
                    {
                        trim = true;
                    }
                }
                else
                {
                    intersection = arc.IntersectArcWithCircle(profileRadius, middlePnt, SI_DIST, endPoint);
                }
                if (intersection == null) return null;
                var endPnt = intersection.Value;
                double fullWidening = arc.DistanceTo(endPnt) - K;
                var arcCenter = arc.Center;
                var diAngle = isStart ? AngleBetween3Points(startPoint, arcCenter, endPnt) : -AngleBetween3Points(startPoint, arcCenter, endPnt);
                //var diAngle = start ? SubtractAngles(endAngle, startAngle) : - SubtractAngles(startAngle, endAngle);

                var delta = fullWidening - widening * DI_COEFF;
                var radius = profileRadius + widening * (1 - DI_COEFF);
                double ang = startAngle;
                int count = 10;
                for (int i = 0; i <= count; i++)
                {
                    var point = PolarPoint(arcCenter, ang, radius);
                    points.Add(point);
                    radius -= delta / count;
                    ang += diAngle / count;
                }
                if (trim)
                {
                    polyline.RemovePolylinePoints(endPnt, isReversed); // trim profile before/ after intersection point
                    points.Add(intersection.Value);
                    vertexIndex = polyline.NumberOfVertices;
                }
            }
            else
            {
                double k = K;

                // linear section of runout
                var basicPnt = PolarPoint(startPoint, startAngle, k);
                var startPnt = PolarPoint(basicPnt, startAngle + correctionAngle, LE2_DIST);
                var middlePnt = PolarPoint(basicPnt, startAngle + correctionAngle, LE1_DIST);
                middlePnt = PolarPoint(middlePnt, startAngle, widening);
                var endPnt = PolarPoint(basicPnt, startAngle, widening);

                points.Add(startPnt);
                points.Add(middlePnt);
                points.Add(endPnt);

                if (intersect) // intersection with arc to arc transition
                {
                    var pointsPolyline = new RCPolyline(points);
                    var intersection = polyline.IntersectPolylines(pointsPolyline);
                    if (intersection != null)
                    {
                        polyline.RemovePolylinePoints(intersection.Value, isReversed); // remove points after/ before intersection
                        pointsPolyline.RemovePolylinePoints(intersection.Value, false);
                        points = pointsPolyline.Points;
                        points.Add(intersection.Value);
                        vertexIndex = polyline.NumberOfVertices;
                        trim = true;
                    }
                }
            }

            if (!isStart)
            {
                points.Reverse();
            }
            polyline.AddPoints(points, vertexIndex, isReversed);
            if (!intersect)
            {
                polyline.RemovePolylinePoints(vertexIndex + points.Count, false);
            }

            return polyline;
        }

        /// <summary>
        /// Generate inner and outer profiles for two connected arcs in a compound curve
        /// </summary>
        private static Dictionary<string, RCPolyline[]> GenerateArcToArcProfiles(RCArc arc1, RCArc arc2,
            RCCurve curve1, RCCurve curve2, Dictionary<string, RCPolyline[]> allProfiles)
        {
            if (arc1.Radius >= TramProfileData.MaxRadius && arc2.Radius >= TramProfileData.MaxRadius)
            {
                allProfiles[curve1.Handle] = GenerateArcProfiles(arc1, curve1, allProfiles[curve1.Handle]);
                allProfiles[curve2.Handle] = GenerateArcProfiles(arc2, curve2, allProfiles[curve2.Handle]);
            }
            else
            {
                // Calculate widening values
                double di1 = TramProfileData.GetWidening(arc1.Radius, -1);
                double de1 = TramProfileData.GetWidening(arc1.Radius, 1);
                double di2 = TramProfileData.GetWidening(arc2.Radius, -1);
                double de2 = TramProfileData.GetWidening(arc2.Radius, 1);

                // Calculate profile radii
                double innerRadius1 = arc1.Radius - K - di1;
                double outerRadius1 = arc1.Radius + K + de1;
                double innerRadius2 = arc2.Radius - K - di2;
                double outerRadius2 = arc2.Radius + K + de2;

                allProfiles = GenerateProfilesArcArc(arc1, arc2, curve1, curve2, innerRadius1, innerRadius2,
                    di1, di2, allProfiles, ProfileType.Inner, arc1.Radius > arc2.Radius);
                allProfiles = GenerateProfilesArcArc(arc1, arc2, curve1, curve2, outerRadius1, outerRadius2,
                    de1, de2, allProfiles, ProfileType.Outer, arc1.Radius > arc2.Radius);
            }

            return allProfiles;
        }

        /// <summary>
        /// Generate profiles when transitioning from one arc to another. Profiles on the right already exist.
        /// </summary>
        private static Dictionary<string, RCPolyline[]> GenerateProfilesArcArc(RCArc arc1, RCArc arc2, RCCurve curve1, RCCurve curve2,
            double profileRadius1, double profileRadius2, double widening1, double widening2,
            Dictionary<string, RCPolyline[]> allProfiles, ProfileType profileType, bool arc1IsLarger)
        {
            int index = profileType == ProfileType.Inner ? 0 : 1;
            var profiles1 = allProfiles[arc1.Handle] ?? new RCPolyline[2];
            var profiles2 = allProfiles[arc2.Handle] ?? new RCPolyline[2];
            var profile1 = profiles1[index] ?? new RCPolyline();
            var profile2 = profiles2[index] ?? new RCPolyline();
            var vertexIndex = 0;
            bool trim;
            bool isSpiral = curve1.Spiral1.Polyline != null;
            var center1 = arc1.Center;
            var center2 = arc2.Center;
            double delta = widening2 - widening1;
            int sign = Math.Sign(delta);

            if (profileType == ProfileType.Inner)
            {
                // Calculate intersection points for transition curves
                var auxCenter1 = PolarPoint(center1, arc1.StartAngle, profileRadius1 - delta / 2);
                var auxCenter2 = PolarPoint(center2, arc2.EndAngle, profileRadius2 + delta / 2);
                var transitionEnd = arc1.IntersectArcWithCircle(profileRadius1, auxCenter1, SI_DIST, arc1.MiddlePoint);
                var transitionStart = arc2.IntersectArcWithCircle(profileRadius2, auxCenter2, SI_DIST, arc2.MiddlePoint);

                if (transitionEnd == null || transitionStart == null)
                    throw new InvalidOperationException(Properties.Resources.TramProfile_ErrorTramProfileCouldNotBeCreated);

                double endAngle = center1.AngleTo(transitionEnd.Value);
                double startAngle = center2.AngleTo(transitionStart.Value);

                // Generate transition polyline for both profiles
                var spiralProfile = isSpiral ? (arc1IsLarger ?
                        CreateSpiralRunout(curve2, null, widening1, delta, ProfileType.Inner, false, 0, false, out trim) :
                        CreateSpiralRunout(curve1, null, widening2, -delta, ProfileType.Inner, true, 0, false, out trim)) : null;
                double delta1 = isSpiral ? Math.Abs(arc1.DistanceTo(spiralProfile.EndPoint) - K - widening1) * sign : delta / 2;
                double delta2 = isSpiral ? Math.Abs(arc2.DistanceTo(spiralProfile.StartPoint) - K - widening2) * sign : delta / 2;
                var transition1 = CreateTransition(arc1, arc1.StartAngle, endAngle, profileRadius1, delta1, 11, ProfileType.Inner, true);
                var transition2 = CreateTransition(arc2, startAngle, arc2.EndAngle, profileRadius2, delta2, 11, ProfileType.Inner, false);

                // Arc1 (left) inner profile
                vertexIndex = 0;
                if (isSpiral)
                {
                    profile1.AddVertices(spiralProfile.Vertices, 0); // add spiral transition
                    vertexIndex += spiralProfile.NumberOfVertices;
                }
                profile1.AddVertices(transition1.Vertices, vertexIndex); // add half of arc to arc transition
                vertexIndex += transition1.NumberOfVertices;

                // Arc2 (right) inner profile
                if (curve2.Tangent1.Arc != null && profile2.NumberOfVertices > 0) // previous arc exists - append
                {
                    vertexIndex = profile2.NumberOfVertices;
                    var intersection = transition2.GetClosestPointTo(profile2.EndPoint);
                    if (intersection.IsEqualTo(transitionStart.Value, TOL))  // arc between transitions
                    {
                        profile2.CreateTrimmedArcProfile(arc2, profileRadius2, transitionStart.Value, true,
                            vertexIndex++, (Point2d?)profile2.EndPoint); // add trimmed arc
                    }
                    else
                    {
                        transition2.RemovePolylinePoints(intersection, true);  // trim transitions overlap
                    }
                }
                else
                {
                    vertexIndex = 0;
                }
                profile2.AddVertices(transition2.Vertices, vertexIndex);
                vertexIndex += transition2.NumberOfVertices;
            }
            else // outer profile
            {
                // Calculate intersection points for transition curves
                var auxCenter = arc1IsLarger ?
                    PolarPoint(center1, arc1.StartAngle, profileRadius1) :
                    PolarPoint(center2, arc2.EndAngle, profileRadius2);
                var transitionEnd = arc1IsLarger ?
                    arc1.IntersectArcWithCircle(profileRadius1, auxCenter, isSpiral ? LI2_DIST : LE2_DIST, arc1.EndPoint) :
                    arc2.IntersectArcWithCircle(profileRadius2, auxCenter, isSpiral ? LI2_DIST : LE2_DIST, arc2.StartPoint);

                if (transitionEnd == null)
                    throw new InvalidOperationException(Properties.Resources.TramProfile_ErrorTramProfileCouldNotBeCreated);

                double endAngle = (arc1IsLarger ? center1 : center2).AngleTo(transitionEnd.Value);
                double startAngle = isSpiral ?
                    (arc1IsLarger ? arc1.StartAngle : arc2.EndAngle) :
                    (arc1.StartAngle + (arc1IsLarger ? LE3_DIST / profileRadius1 : -LE3_DIST / profileRadius2));
                if (!arc1IsLarger)
                {
                    double temp = endAngle;
                    endAngle = startAngle;
                    startAngle = temp;
                }

                // Generate transition polyline for both profiles
                var spiralProfile = isSpiral ? (arc1IsLarger ?
                        CreateSpiralRunout(curve2, null, widening1, delta, ProfileType.Outer, false, 0, false, out trim) :
                        CreateSpiralRunout(curve1, null, widening2, -delta, ProfileType.Outer, true, 0, false, out trim)) : null;
                delta = isSpiral ? (arc1IsLarger ?
                        arc1.DistanceTo(spiralProfile.EndPoint) - K - widening1 :
                        K + widening2 - arc2.DistanceTo(spiralProfile.StartPoint)) : delta;
                var transition = arc1IsLarger ?
                    CreateTransition(arc1, startAngle, endAngle, profileRadius1, delta, 11, ProfileType.Outer, true) :
                    CreateTransition(arc2, startAngle, endAngle, profileRadius2, delta, 11, ProfileType.Outer, false);

                // Arc1 (left) outer profile (two trimmed arcs with transition in the middle/ untrimmed arc)
                if (arc1IsLarger)
                {
                    vertexIndex = 0;
                    if (isSpiral)
                    {
                        profile1.AddVertices(spiralProfile.Vertices, 0); // add spiral transition
                    }
                    else
                    {
                        profile1.CreateTrimmedArcProfile(arc1, profileRadius1 + delta, transition.StartPoint, true, 0); // or trimmed arc
                    }
                    vertexIndex = isSpiral ? spiralProfile.NumberOfVertices : 1;
                    profile1.AddVertices(transition.Vertices, vertexIndex); // add arc to arc transition
                    vertexIndex += transition.NumberOfVertices;
                    var tempPoint = arc1.GetClosestPointTo(transitionEnd.Value);
                    if (!tempPoint.IsEqualTo(arc1.EndPoint, 1e-4)) // Arc2 is not short - add trimmed arc
                    {
                        profile1.CreateTrimmedArcProfile(arc1, profileRadius1, transitionEnd.Value, false, vertexIndex++); // add arc
                        vertexIndex++;
                    }
                }
                else
                {
                    var arc1StartPoint = PolarPoint(center1, arc1.StartAngle, profileRadius1);
                    profile1.CreateTrimmedArcProfile(arc1, profileRadius1, arc1StartPoint, false, 0); // add full arc
                    vertexIndex = 2;
                }

                // Arc2 (right) outer profile
                vertexIndex = 0;
                if (arc1IsLarger && curve2.Tangent1.Arc == null)
                {
                    var arc2EndPoint = PolarPoint(center2, arc2.EndAngle, profileRadius2);
                    profile2.CreateTrimmedArcProfile(arc2, profileRadius2, arc2EndPoint, true, 0); // add full arc
                    vertexIndex = 2;
                }
                else if (!arc1IsLarger)
                {
                    var tempPoint = arc2.GetClosestPointTo(transitionEnd.Value);
                    if (!tempPoint.IsEqualTo(arc2.StartPoint, 1e-4)) // Arc2 is not short - add trimmed arc
                    {
                        if (curve2.Tangent1.Arc != null) // previous arc exists - append
                        {
                            vertexIndex = profile2.NumberOfVertices - 1;
                            vertexIndex = vertexIndex < 0 ? 0 : vertexIndex;
                        }
                        profile2.CreateTrimmedArcProfile(arc2, profileRadius2, transitionEnd.Value, true,
                            vertexIndex, vertexIndex > 0 ? (Point2d?)profile2.GetPoint2dAt(vertexIndex - 2) : null); // add trimmed arc
                        vertexIndex++;
                    }
                    profile2.AddVertices(transition.Vertices, vertexIndex); // add arc to arc transition
                    vertexIndex += transition.NumberOfVertices;
                    if (isSpiral)
                    {
                        profile2.AddVertices(spiralProfile.Vertices, vertexIndex); // add spiral transition
                        vertexIndex += spiralProfile.NumberOfVertices;
                    }
                    else
                    {
                        profile2.CreateTrimmedArcProfile(arc2, profileRadius2 - delta, transition.EndPoint, false, vertexIndex++); // add trimmed arc
                    }
                    vertexIndex++;
                }
            }

            profiles1[index] = profile1;
            profiles2[index] = profile2;
            allProfiles[arc1.Handle] = profiles1;
            allProfiles[arc2.Handle] = profiles2;
            return allProfiles;
        }

        /// <summary>
        /// Generate linear transition points between two arc profiles
        /// </summary>
        private static RCPolyline CreateTransition(RCArc arc, double startAngle, double endAngle,
            double radius, double delta, int numPoints, ProfileType profileType, bool isStart)
        {
            var points = new List<Point2d>();
            var center = arc.Center;

            double increment = 1.0 / (numPoints - 1);
            double t = 0;
            double incrementAngle = NormalizeAngle(endAngle - startAngle) / (numPoints - 1);
            double angle = startAngle;

            // Calculate points of the transition curve
            for (int i = 0; i < numPoints; i++)
            {
                var point = profileType == ProfileType.Inner ?
                    (isStart ? PolarPoint(center, angle, radius + (t - 1) * delta) : PolarPoint(center, angle, radius + t * delta)) :
                    PolarPoint(center, angle, radius + (isStart ? (1 - t) : -t) * delta);
                points.Add(point);
                angle += incrementAngle;
                t += increment;
            }

            RCPolyline polyline = new RCPolyline();
            return polyline.AddPoints(points);
        }

        /// <summary>
        /// Create modified arc profile with trimming at specified point
        /// </summary>
        private static RCPolyline CreateTrimmedArcProfile(this RCPolyline existingProfile, RCArc arc,
            double profileRadius, Point2d trimPoint, bool trimAtEnd, int vertexIndex = 0, Point2d? trimStartPoint = null)
        {
            var polyline = existingProfile ?? new RCPolyline();

            var center = arc.Center;
            var startPoint = PolarPoint(center, arc.StartAngle, profileRadius);
            var endPoint = PolarPoint(center, arc.EndAngle, profileRadius);

            if (trimStartPoint != null)
            {
                // Truncate at both sides, use trim start point as start and trim point as end
                var middle = arc.ArcMiddlePoint(profileRadius, trimStartPoint.Value, trimPoint);
                double bulge = CalculateBulge(middle, trimStartPoint.Value, trimPoint);
                polyline.AddVertex(vertexIndex++, trimStartPoint.Value, bulge);
                polyline.AddVertex(vertexIndex++, trimPoint, 0);
            }
            else if (trimAtEnd)
            {
                // Truncate at end, use truncation point as end
                var middle = arc.ArcMiddlePoint(profileRadius, startPoint, trimPoint);
                double bulge = CalculateBulge(middle, startPoint, trimPoint);
                polyline.AddVertex(vertexIndex++, startPoint, bulge);
                polyline.AddVertex(vertexIndex++, trimPoint, 0);
            }
            else
            {
                // Truncate at start, use truncation point as start
                var middle = arc.ArcMiddlePoint(profileRadius, trimPoint, endPoint);
                double bulge = CalculateBulge(middle, trimPoint, endPoint);
                polyline.AddVertex(vertexIndex++, trimPoint, bulge);
                polyline.AddVertex(vertexIndex++, endPoint, 0);
            }

            return polyline;
        }
    }
}