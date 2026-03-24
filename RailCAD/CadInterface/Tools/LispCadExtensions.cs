#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcGe = Autodesk.AutoCAD.Geometry;
    using _AcRn = Autodesk.AutoCAD.Runtime;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
    using _AcGe = Teigha.Geometry;
    using _AcRn = Bricscad.Runtime;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcGe = Gssoft.Gscad.Geometry;
    using _AcRn = Gssoft.Gscad.Runtime;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
    using _AcRn = ZwSoft.ZwCAD.Runtime;
#endif

using System;
using System.Collections.Generic;

using RailCAD.Models.Geometry;

namespace RailCAD.CadInterface.Tools
{
    internal static class LispCadExtensions
    {
        public static string GetAsString(this _AcDb.TypedValue tv)
        {
            switch (tv.TypeCode)
            {
                case (short)_AcRn.LispDataType.Text:
                {
                    return (string)tv.Value;
                }
                case (short)_AcDb.DxfCode.ExtendedDataHandle:
                {
                    return (string)tv.Value;
                }
                default:
                    throw new ArgumentException("string", tv.ToString());
            }
        }

        public static Point2d GetAsPoint2d(this _AcDb.TypedValue tv)
        {
            switch (tv.TypeCode)
            {
                case (short)_AcRn.LispDataType.Point2d:
                    {
                        return ((_AcGe.Point2d)tv.Value).ToPoint2d();
                    }
                case (short)_AcRn.LispDataType.Point3d:
                    {
                        return ((_AcGe.Point3d)tv.Value).ToPoint2d();
                    }
                default:
                    throw new ArgumentException("point2D/3D", tv.ToString());
            }
        }

        public static Point3d GetAsPoint3d(this _AcDb.TypedValue tv)
        {
            switch (tv.TypeCode)
            {
                case (short)_AcRn.LispDataType.Point2d:
                    {
                        return ((_AcGe.Point2d)tv.Value).ToPoint3d();
                    }
                case (short)_AcRn.LispDataType.Point3d:
                    {
                        return ((_AcGe.Point3d)tv.Value).ToPoint3d();
                    }
                default:
                    throw new ArgumentException("point2D/3D", tv.ToString());
            }
        }

        public static Vector3d GetAsVector3d(this _AcDb.TypedValue tv)
        {
            if (tv.TypeCode != (short)_AcDb.DxfCode.ExtendedDataXCoordinate)
            {
                throw new ArgumentException("vector", tv.ToString());
            }

            _AcGe.Point3d pt = (_AcGe.Point3d)tv.Value;
            return new Vector3d(pt.X, pt.Y, pt.Z);
        }

        /// <summary>
        /// Convert AutoCAD Point2d to CAD-independent Point2d
        /// </summary>
        internal static Point2d ToPoint2d(this _AcGe.Point2d point)
        {
            return new Point2d(point.X, point.Y);
        }

        /// <summary>
        /// Convert AutoCAD Point3d to CAD-independent Point2d
        /// </summary>
        internal static Point2d ToPoint2d(this _AcGe.Point3d point)
        {
            return new Point2d(point.X, point.Y);
        }

        /// <summary>
        /// Convert AutoCAD Point2d to CAD-independent Point3d
        /// </summary>
        internal static Point3d ToPoint3d(this _AcGe.Point2d point, double z = 0)
        {
            return new Point3d(point.X, point.Y, z);
        }

        /// <summary>
        /// Convert AutoCAD Point3d to CAD-independent Point3d
        /// </summary>
        internal static Point3d ToPoint3d(this _AcGe.Point3d point)
        {
            return new Point3d(point.X, point.Y, point.Z);
        }

        /// <summary>
        /// Convert AutoCAD Point3d to CAD-independent Vector3d
        /// </summary>
        internal static Vector3d ToVector3d(this _AcGe.Point3d point)
        {
            return new Vector3d(point.X, point.Y, point.Z);
        }

        /// <summary>
        /// Convert CAD-independent Point2d to AutoCAD Point2d
        /// </summary>
        internal static _AcGe.Point2d ToAcGePoint2d(this Point2d point)
        {
            return new _AcGe.Point2d(point.X, point.Y);
        }

        /// <summary>
        /// Convert CAD-independent Point3d to AutoCAD Point2d
        /// </summary>
        internal static _AcGe.Point2d ToAcGePoint2d(this Point3d point)
        {
            return new _AcGe.Point2d(point.X, point.Y);
        }

        /// <summary>
        /// Convert CAD-independent Point2d to AutoCAD Point3d
        /// </summary>
        internal static _AcGe.Point3d ToAcGePoint3d(this Point2d point, double z = 0)
        {
            return new _AcGe.Point3d(point.X, point.Y, z);
        }

        /// <summary>
        /// Convert CAD-independent Point3d to AutoCAD Point3d
        /// </summary>
        internal static _AcGe.Point3d ToAcGePoint3d(this Point3d point)
        {
            return new _AcGe.Point3d(point.X, point.Y, point.Z);
        }

        #region CAD Specific Utilities
        internal static _AcDb.Polyline OffsetCurve(this _AcDb.Polyline polyline, double distance, Point2d point2d)
        {
            if (polyline == null) return null;
            var point = point2d.ToAcGePoint3d();

            var offsets1 = polyline.GetOffsetCurves(distance);
            if (offsets1 == null && offsets1.Count == 0) return null;
            var offset1 = (_AcDb.Polyline)offsets1[0];
            double distance1 = point.DistanceTo(offset1.GetClosestPointTo(point, false));

            var offsets2 = polyline.GetOffsetCurves(-distance);
            if (offsets2 == null && offsets1.Count == 0) return null;
            var offset2 = (_AcDb.Polyline)offsets2[0];
            double distance2 = point.DistanceTo(offset2.GetClosestPointTo(point, false));

            if (distance1 < distance2)
            {
                offset2.Dispose();
                return offset1;
            }
            else
            {
                offset1.Dispose();
                return offset2;
            }
        }
        internal static RCPolyline OffsetCurve(this RCPolyline rcPolyline, double distance, Point2d point)
        {
            _AcDb.Polyline polyline = GetPolylineFromGeometry(rcPolyline);
            _AcDb.Polyline offset = polyline.OffsetCurve(distance, point);
            return GetPolylineGeometry(offset);
        }

        /// <summary>
        /// Inserts or updates a vertex of a polyline entity at the given index.
        /// </summary>
        internal static void AddVertex(this _AcDb.Polyline polyline, int vertexIndex, Point2d point, double bulge, bool fromStart = false)
        {
            if (fromStart)
            {
                polyline.AddVertexAt(0, point.ToAcGePoint2d(), bulge, 0, 0);
            }
            else if (vertexIndex < polyline.NumberOfVertices)
            {
                polyline.SetPointAt(vertexIndex, point.ToAcGePoint2d());
                polyline.SetBulgeAt(vertexIndex, bulge);
            }
            else
            {
                polyline.AddVertexAt(vertexIndex++, point.ToAcGePoint2d(), bulge, 0, 0);
            }
        }

        /// <summary>
        /// Finds intersection point between first and second entity.
        /// If test point is provided it extends the first entity if no intersection is found.
        /// </summary>
        /// <returns>Intersection point or null if no intersection exists.</returns>
        internal static Point2d? IntersectWith(this _AcDb.Curve curve1, _AcDb.Curve curve2, Point2d? testPoint = null)
        {
            _AcGe.Point3dCollection intersections = new _AcGe.Point3dCollection();
            curve1.IntersectWith(curve2, _AcDb.Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);

            if (intersections != null && intersections.Count > 0)
            {
                return intersections[0].ToPoint2d();
            }
            else if (testPoint.HasValue)
            {
                var testPnt = testPoint.Value.ToAcGePoint3d();
                curve1.IntersectWith(curve2, _AcDb.Intersect.ExtendThis, intersections, IntPtr.Zero, IntPtr.Zero);
                if (intersections.Count == 1)
                {
                    return intersections[0].ToPoint2d();
                }
                else if (intersections.Count > 1)
                {
                    if (testPnt.DistanceTo(intersections[0]) < testPnt.DistanceTo(intersections[1]))
                    {
                        return intersections[0].ToPoint2d();
                    }
                    else
                    {
                        return intersections[1].ToPoint2d();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Get coordinates of a DBObject
        /// </summary>
        /// <returns>Coordinates as a list of Point3d</returns>
        internal static List<Point3d> GetPoints(this _AcDb.DBObject obj)
        {
            if (obj == null) return null;
            var points = new List<Point3d>();

            if (obj is _AcDb.DBPoint point)
            {
                points.Add(point.Position.ToPoint3d());
            }
            else if (obj is _AcDb.Line line)
            {
                points.Add(line.StartPoint.ToPoint3d());
                points.Add(line.EndPoint.ToPoint3d());
            }
            else if (obj is _AcDb.Arc arc)
            {
                points.Add(arc.StartPoint.ToPoint3d());
                points.Add(arc.EndPoint.ToPoint3d());
                points.Add(arc.Center.ToPoint3d());
            }
            else if (obj is _AcDb.Polyline polyline && polyline.NumberOfVertices > 0)
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    points.Add(polyline.GetPoint3dAt(i).ToPoint3d());
                }
            }
            return points;
        }

        #endregion

        #region Arc and Polyline Geometry Utilities

        /// <summary>
        /// Get arc geometry from arc entity
        /// </summary>
        internal static RCArc GetArcGeometry(_AcDb.Arc arc)
        {
            if (arc == null) return null;

            return new RCArc(
                arc.Center.ToPoint2d(),
                arc.Radius,
                arc.StartAngle,
                arc.EndAngle,
                arc.Handle.ToString());
        }

        /// <summary>
        /// Get arc entity from arc geometry
        /// </summary>
        internal static _AcDb.Arc GetArcFromGeometry(RCArc arcGeometry, _AcDb.Arc arc)
        {
            var handle = arcGeometry.Handle;
            var center = arcGeometry.Center.ToAcGePoint3d();
            var radius = arcGeometry.Radius;
            var startAngle = arcGeometry.StartAngle;
            var endAngle = arcGeometry.EndAngle;

            if (arc != null)
            {
                arc.Center = center;
                arc.Radius = radius;
                arc.StartAngle = startAngle;
                arc.EndAngle = endAngle;
            }
            else
            {
                arc = new _AcDb.Arc(center, radius, startAngle, endAngle);
            }

            return arc;
        }

        /// <summary>
        /// Convert polyline entity to polyline geometry
        /// </summary>
        internal static RCPolyline GetPolylineGeometry(_AcDb.Polyline polyline, bool blank = false)
        {
            if (polyline == null) return null;

            var vertices = new List<PolylineVertex>();

            if (!blank && polyline != null) // read no vertices
            {
                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    var point = polyline.GetPoint2dAt(i).ToPoint2d();
                    var bulge = polyline.GetBulgeAt(i);
                    vertices.Add(new PolylineVertex(point, bulge));
                }
            }

            var polylineGeometry = new RCPolyline(vertices, polyline != null ? polyline.Handle.ToString() : "");
            return polylineGeometry;
        }

        /// <summary>
        /// Convert polyline geometry to polyline entity
        /// </summary>
        internal static _AcDb.Polyline GetPolylineFromGeometry(RCPolyline polylineGeometry, _AcDb.Polyline polyline = null)
        {
            var vertices = polylineGeometry.Vertices;
            var handle = polylineGeometry.Handle;
            polyline = polyline ?? new _AcDb.Polyline();

            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];
                polyline.AddVertex(i, vertex.Point, vertex.Bulge);
            }
            for (int i = polyline.NumberOfVertices - 1; i >= vertices.Count; i--)
            {
                polyline.RemoveVertexAt(i);
            }

            return polyline;
        }

        /// <summary>
        /// Get points from polyline entity
        /// </summary>
        internal static List<Point2d> GetPolylinePoints(this _AcDb.Polyline polyline)
        {
            var points = new List<Point2d>();

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var point = polyline.GetPoint2dAt(i).ToPoint2d();
                points.Add(point);
            }

            return points;
        }

        #endregion
    }
}
