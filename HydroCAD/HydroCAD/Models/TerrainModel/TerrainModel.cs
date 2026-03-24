using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.Common;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.TerrainModel.TriangleSearch;

namespace HydroCAD.Models.TerrainModel
{
    public class TerrainModel
    {
        private IList<HCPoint> points;
        private IList<HCLine> lines;
        private IList<HCTriangle> triangles;
        private ITriangleSearchStrategy searchStrategy;

        private static int nameCounter = 1;

        public TerrainModel(IList<HCPoint> points, IList<HCLine> lines, IList<HCTriangle> triangles,
                            bool considerTriangleAreaForNormals, string name = null)
        {
            Name = string.IsNullOrEmpty(name) ? CreateTerrainModelName() : name;
            this.points = points;
            this.lines = lines;
            this.triangles = triangles;

            CreateReferences();
            CalculateNormalVectors(considerTriangleAreaForNormals);
            searchStrategy = null;
        }

        public string Name { get; private set; }

        public IEnumerable<HCPoint> Points => points;
        public IEnumerable<HCLine> Lines => lines;
        public IEnumerable<HCTriangle> Triangles => triangles;

        public IList<Point2d> Points2d => Points.Select(p => p.Point2d).ToList();
        public IList<Point3d> Points3d => Points.Select(p => p.Point3d).ToList();

        /// <summary>
        /// Reconstructs a TerrainModel from deserialized XData (point neighbour handles).
        /// </summary>
        public static TerrainModel CreateTerrainModelFromDeserializedData(
            IList<HCPoint> hcPoints,
            IDictionary<string, HCPoint> ptHandleMap,
            IList<HCLine> fixedSegments = null,
            string appName = null)
        {
            int triangleId = 0;
            var hcTriangles = new HashSet<HCTriangle>();
            var hcLines = new HashSet<HCLine>();

            if (fixedSegments != null)
                foreach (HCLine seg in fixedSegments) hcLines.Add(seg);

            foreach (HCPoint pt in hcPoints)
            {
                HashSet<string> neighbors = pt.NeighborsHandles;

                while (neighbors.Any())
                {
                    string neighborHandle = neighbors.FirstOrDefault();
                    if (!ptHandleMap.TryGetValue(neighborHandle, out HCPoint ptOther)) return null;

                    HashSet<string> otherNeighbors = ptOther.NeighborsHandles;
                    IEnumerable<string> sharedPts = neighbors.Intersect(otherNeighbors);

                    foreach (string sharedHandle in sharedPts)
                    {
                        if (!ptHandleMap.TryGetValue(sharedHandle, out HCPoint ptShared)) return null;

                        var l1 = HCLine.CreateLineAutoType(pt, ptOther);
                        var l2 = HCLine.CreateLineAutoType(ptOther, ptShared);
                        var l3 = HCLine.CreateLineAutoType(ptShared, pt);
                        hcLines.Add(l1);
                        hcLines.Add(l2);
                        hcLines.Add(l3);

                        hcTriangles.Add(new HCTriangle(
                            ++triangleId,
                            GeometryHelper.OrientClockwise(pt, ptOther, ptShared),
                            new List<HCLine> { l1, l2, l3 }));
                    }

                    neighbors.Remove(neighborHandle);
                    otherNeighbors.Remove(pt.Handle);
                }
            }

            return new TerrainModel(hcPoints, hcLines.ToList(), hcTriangles.ToList(), true, appName);
        }

        public bool IsEmpty() => triangles.Count == 0;

        /// <summary>
        /// Finds the triangle containing the given 2D point.
        /// </summary>
        public HCTriangle GetTriangle(Point2d point)
        {
            if (searchStrategy == null) InitializeSearchStrategy();
            return searchStrategy.FindTriangle(point);
        }

        /// <summary>
        /// Returns the interpolated terrain elevation at the given 2D location.
        /// Returns NaN if the point is outside the model extent.
        /// </summary>
        public double GetPointHeight(Point2d point)
        {
            HCTriangle triangle = GetTriangle(point);
            return GetPointHeight(point, triangle);
        }

        public double GetPointHeight(Point2d point, HCTriangle triangle)
        {
            if (triangle == null) return double.NaN;
            return GetHeightOnTriangle(
                triangle.Points[0].Point3d,
                triangle.Points[1].Point3d,
                triangle.Points[2].Point3d,
                point);
        }

        private static string CreateTerrainModelName()
        {
            return $"HC_DTM_{new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds()}";
        }

        private void CreateReferences()
        {
            foreach (HCPoint point in Points)
            {
                point.Lines.Clear();
                point.Triangles.Clear();
            }
            if (lines != null)
                foreach (HCLine line in lines) line.LinkPointsToLine();
            foreach (HCTriangle triangle in triangles) triangle.LinkPointsToTriangle();
        }

        private void CalculateNormalVectors(bool considerTriangleAreaForNormals)
        {
            foreach (HCPoint point in Points)
            {
                if (point.Triangles.Count > 0)
                {
                    var normals = new List<Vector3d>(point.Triangles.Count);
                    foreach (HCTriangle triangle in point.Triangles)
                    {
                        normals.Add(considerTriangleAreaForNormals
                            ? triangle.Normal * triangle.Area
                            : triangle.Normal);
                    }
                    point.SetNormal(GeometryHelper.AverageVectors(normals).Normalized());
                }
            }
        }

        private void InitializeSearchStrategy()
        {
            searchStrategy = points.Count < 1000
                ? (ITriangleSearchStrategy)new NaiveStrategy(triangles)
                : new GridStrategy(this);
        }

        private static double GetHeightOnTriangle(Point3d p1, Point3d p2, Point3d p3, Point2d point)
        {
            double detT = (p2.Y - p3.Y) * (p1.X - p3.X) + (p3.X - p2.X) * (p1.Y - p3.Y);
            double lambda1 = ((p2.Y - p3.Y) * (point.X - p3.X) + (p3.X - p2.X) * (point.Y - p3.Y)) / detT;
            double lambda2 = ((p3.Y - p1.Y) * (point.X - p3.X) + (p1.X - p3.X) * (point.Y - p3.Y)) / detT;
            double lambda3 = 1 - lambda1 - lambda2;
            return lambda1 * p1.Z + lambda2 * p2.Z + lambda3 * p3.Z;
        }
    }
}
