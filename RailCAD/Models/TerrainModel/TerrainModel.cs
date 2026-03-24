using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Common;
using RailCAD.Models.Geometry;

namespace RailCAD.Models.TerrainModel
{
    public class TerrainModel
    {
        private IList<RCPoint> points;
        private IList<RCLine> lines;
        private IList<RCTriangle> triangles;
        private ITriangleSearchStrategy searchStrategy;

        public TerrainModel(IList<RCPoint> points, IList<RCLine> lines, IList<RCTriangle> triangles, bool considerTriangleAreaForNormals, string name = null)
        {
            if (String.IsNullOrEmpty(name))
            {
                name = CreateTerrainModelApplicationName();
            }
            this.Name = name;
            this.points = points;
            this.lines = lines;
            this.triangles = triangles;

            CreateReferences();
            CalculateNormalVectors(considerTriangleAreaForNormals);

            this.searchStrategy = null;
        }

        public string Name
        {
            get; private set;
        }

        public IEnumerable<RCPoint> Points
        {
            get { return this.points; }
        }

        public IEnumerable<RCLine> Lines
        {
            get { return this.lines; }
        }

        public IEnumerable<RCTriangle> Triangles
        {
            get { return this.triangles; }
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

        public static TerrainModel CreateTerrainModelFromDeserializedData(
                IList<RCPoint> rcPoints,
                IDictionary<string, RCPoint> ptHandleMap,
                IList<RCLine> fixedSegments = null,
                string appName = null
            )
        {
            int rcTriangleId = 0;
            var rcTriangles = new HashSet<RCTriangle>();   // HashSet → to avoid duplicities
            var rcLines = new HashSet<RCLine>();

            if (fixedSegments != null)
            {
                foreach (RCLine segment in fixedSegments)
                {
                    rcLines.Add(segment);
                }
            }

            foreach (RCPoint rcPt in rcPoints)
            {
                HashSet<string> rcPtNeighbors = rcPt.NeighborsHandles;

                while (rcPtNeighbors.Any())
                {
                    string rcPtNeighborHandle = rcPtNeighbors.FirstOrDefault();  // deque first
                    RCPoint rcPtOther = ptHandleMap[rcPtNeighborHandle];

                    HashSet<string> rcPtOtherNeighbors = rcPtOther.NeighborsHandles;

                    // find shared point(s) between this and other
                    IEnumerable<string> sharedPts = rcPtNeighbors.Intersect(rcPtOtherNeighbors);

                    // build triangle from each shared point
                    foreach (string sharedPtHandle in sharedPts)
                    {
                        ptHandleMap.TryGetValue(sharedPtHandle, out RCPoint rcPtShared);
                        if (rcPtShared == null)
                        {
                            //cad.WriteMessageNoDebug(Properties.Resources.TerrainModel_WarningDeletedPointFoundFixTerrainModel);  todo: show message to user!
                            return null;
                        }

                        // create and add lines
                        var line1 = RCLine.CreateLineAutoType(rcPt, rcPtOther);
                        var line2 = RCLine.CreateLineAutoType(rcPtOther, rcPtShared);
                        var line3 = RCLine.CreateLineAutoType(rcPtShared, rcPt);
                        rcLines.Add(line1);
                        rcLines.Add(line2);
                        rcLines.Add(line3);

                        // create and add triangle
                        rcTriangles.Add(new RCTriangle(
                            ++rcTriangleId,
                            GeometryHelper.OrientClockwise(rcPt, rcPtOther, rcPtShared),
                            new List<RCLine> { line1, line2, line3 }
                            ));
                    }

                    // remove precessed references
                    rcPtNeighbors.Remove(rcPtNeighborHandle);  // deque first
                    rcPtOtherNeighbors.Remove(rcPt.Handle);
                }
            }

            return new TerrainModel(rcPoints, rcLines.ToList(), rcTriangles.ToList(), true, appName);
        }

        public bool IsEmpty()
        {
            return this.triangles.Count == 0;
        }

        public RCTriangle GetTriangle(Point2d point)
        {
            if (this.searchStrategy == null)
                InitializeSearchStrategy();  // lazy initialization

            return this.searchStrategy.FindTriangle(point);
        }

        public double GetPointHeight(Point2d point)
        {
            RCTriangle triangle = this.GetTriangle(point);
            return GetPointHeight(point, triangle);
        }

        public double GetPointHeight(Point2d point, RCTriangle triangle)
        {
            double height = double.NaN;
            if (triangle != null)
            {
                height = GetHeightOnTriangle(triangle.Points[0].Point3d, triangle.Points[1].Point3d, triangle.Points[2].Point3d, point);
            }
            return height;
        }

        public Point2d? GetIntermediatePoint(Point2d point1, Point2d point2, RC_SPOJNICE type = RC_SPOJNICE.FIXED_SEGMENT)
        {
            return GetIntermediatePoint(point1, point2, this.GetTriangle(point1), this.GetTriangle(point2), type);
        }

        public Point2d? GetIntermediatePoint(Point2d point1, Point2d point2, RCTriangle triangle1, RCTriangle triangle2, RC_SPOJNICE type = RC_SPOJNICE.FIXED_SEGMENT)
        {
            // finds intermediate point on common edge between two points in neighboring triangles
            // not applicable to points in more distant triangles
            if (triangle1 == null || triangle2 == null)
                return null;  // first or second point outside of the terrain model

            if (triangle1.Equals(triangle2))
                return null;  // both points lie in the same triangle

            // find common edge between point1 and point2
            IEnumerable<RCLine> edges = triangle1.Lines.Intersect(triangle2.Lines);

            if (!edges.Any())
                return null;  // no common edge found

            //if (edges.Count() > 1)
            //    CadModel.WriteMessageStatic("WARNING: More than one common edge found between two triangles.");

            RCLine edge = edges.FirstOrDefault();
            if (type.HasFlag(edge.Type))  // intermediate points calcualated only at fixed segments
            {
                return GeometryHelper.FindIntersectionBetweenTwo2DLines(edge.Pt1.Point2d, edge.Pt2.Point2d, point1, point2);
            }
            else
            {
                return null;
            }
        }

        public int GetNumberOfUsedFallbacksInSearchStrategy()
        {
            if (this.searchStrategy is GridStrategy)
            {
                return ((GridStrategy)this.searchStrategy).used_fallbacks;
            }
            return 0;
        }

        private string CreateTerrainModelApplicationName()
        {
            //return $"RC_D1";
            return XDataAppNames.RC_D + new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        private void CreateReferences()
        {
            // note: triangles are used in the terrain model (contrary to lines, that are used for visualization only) -> reference between trianges and points is created
            // remove any previous references - why? is this necessary?
            foreach (RCPoint point in this.Points)
            {
                point.Lines.Clear();
                point.Triangles.Clear();
            }

            // references: line -> point, triangle <-> point, (triangle <-> line is not used)
            if (lines != null)
            {
                foreach (RCLine line in lines)
                {
                    line.LinkPointsToLine();
                }
            }

            foreach (RCTriangle triangle in triangles)
            {
                triangle.LinkPointsToTriangle();
            }
        }

        private void CalculateNormalVectors(bool considerTriangleAreaForNormals)
        {
            // references must be already created
            foreach (RCPoint point in this.Points)
            {
                if (point.Triangles.Count > 0)  // two points can be placed in one location -> only one is used for triangulation
                {
                    var normals = new List<Vector3d>(point.Triangles.Count);

                    foreach (RCTriangle triangle in point.Triangles)
                    {
                        if (considerTriangleAreaForNormals)
                        {
                            normals.Add(triangle.Normal * triangle.Area);  // weighted by triangle area
                        }
                        else
                        {
                            normals.Add(triangle.Normal);
                        }
                    }

                    point.SetNormal(GeometryHelper.AverageVectors(normals).Normalized());  // normalized vectors are used
                }
            }
        }

        private void InitializeSearchStrategy()
        {
            // determine optimal strategy according to number of points (todo: optimize)
            if (this.points.Count < 1000)
            {
                this.searchStrategy = new NaiveStrategy(this);
            }
            else
            {
                this.searchStrategy = new GridStrategy(this);
            }
        }

        private double GetHeightOnTriangle(Point3d p1, Point3d p2, Point3d p3, Point2d point)
        {
            // Barycentric coordinate system: https://stackoverflow.com/questions/36090269/finding-height-of-point-on-height-map-triangles

            // Determinant of linear projection T
            double detT = (p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y);
            double lambda1 = ((p2.Y - p3.Y) * (point.X - p3.X) + (p3.X - p2.X) * (point.Y - p3.Y)) / detT;
            double lambda2 = ((p3.Y - p1.Y) * (point.X - p3.X) + (p1.X - p3.X) * (point.Y - p3.Y)) / detT;
            double lambda3 = 1 - lambda1 - lambda2;

            return lambda1 * p1.Z + lambda2 * p2.Z + lambda3 * p3.Z;
        }
    }
}
