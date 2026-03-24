using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.TerrainModel;
using HydroCAD.Profile;
using HydroCAD.Models.Network;
using HydroCAD.Services.Triangulation;

namespace HydroCAD.Tests
{
    [TestClass]
    public class TerrainModelTests
    {
        // ── Terrain model triangulation ─────────────────────────────────

        [TestMethod]
        public void UT1_Triangulate_9Points()
        {
            var points = Get9PointGrid();
            var triangulator = new DelaunatorAdapter(points);
            TerrainModel model = triangulator.Triangulate(false);

            Assert.IsNotNull(model, "Triangulation should succeed.");
            Assert.IsFalse(model.IsEmpty(), "Terrain model should have triangles.");

            // A 3×3 grid (9 points) produces 8 triangles (Delaunay)
            int triangleCount = model.Triangles.Count();
            Assert.IsTrue(triangleCount >= 4, $"Expected at least 4 triangles, got {triangleCount}.");
        }

        [TestMethod]
        public void UT2_GetPointHeight_InsideTriangle()
        {
            var points = Get9PointGrid();
            var triangulator = new DelaunatorAdapter(points);
            TerrainModel model = triangulator.Triangulate(false);
            Assert.IsNotNull(model);

            // Centre of the grid (1.5, 1.5) – inside the model extent
            double height = model.GetPointHeight(new Point2d(1.5, 1.5));
            Assert.IsFalse(double.IsNaN(height), "Height should be found for a point inside the terrain.");
        }

        [TestMethod]
        public void UT3_GetPointHeight_OutsideTerrain_ReturnsNaN()
        {
            var points = Get9PointGrid();
            var triangulator = new DelaunatorAdapter(points);
            TerrainModel model = triangulator.Triangulate(false);
            Assert.IsNotNull(model);

            // Far outside the grid
            double height = model.GetPointHeight(new Point2d(100, 100));
            Assert.IsTrue(double.IsNaN(height), "Height should be NaN for points outside the terrain.");
        }

        [TestMethod]
        public void UT4_TerrainModel_NormalVectors_Set()
        {
            var points = Get9PointGrid();
            var triangulator = new DelaunatorAdapter(points);
            TerrainModel model = triangulator.Triangulate(true);
            Assert.IsNotNull(model);

            int validNormals = model.Points.Count(p => p.IsValid);
            Assert.IsTrue(validNormals > 0, "At least some points should have valid normal vectors.");
        }

        // ── Profile generation ───────────────────────────────────────────

        [TestMethod]
        public void UT5_ProfileGenerator_GeneratesStations()
        {
            TerrainModel model = BuildFlatTerrainModel(zLevel: 100.0);
            var generator = new ProfileGenerator(model);

            var centreline = BuildStraightPolyline(0, 0, 50, 0);
            var route = new HCPipeRoute("TEST", centreline);
            var settings = new ProfileSettings
            {
                PipeDiameter = 300,
                MinCoverDepth = 0.8,
                SamplingInterval = 5.0,
                HorizontalScale = 500,
                VerticalScale = 100,
            };

            ProfileData result = generator.Generate(route, settings);

            Assert.IsNotNull(result, "Profile should be generated.");
            Assert.IsTrue(result.Stations.Count >= 2, "Profile should have at least 2 stations.");
            Assert.AreEqual(0.0, result.Stations.First().Chainage, 1e-6);
            Assert.AreEqual(50.0, result.Stations.Last().Chainage, 0.5);
        }

        [TestMethod]
        public void UT6_ProfileGenerator_InvertLevels_BelowGround()
        {
            TerrainModel model = BuildFlatTerrainModel(zLevel: 100.0);
            var generator = new ProfileGenerator(model);

            var centreline = BuildStraightPolyline(0, 0, 50, 0);
            var route = new HCPipeRoute("TEST", centreline);
            var settings = new ProfileSettings
            {
                PipeDiameter = 300,
                MinCoverDepth = 0.8,
                SamplingInterval = 5.0,
            };

            ProfileData result = generator.Generate(route, settings);
            Assert.IsNotNull(result);

            foreach (var station in result.Stations)
            {
                if (!double.IsNaN(station.CoverDepth))
                    Assert.IsTrue(station.CoverDepth >= 0,
                        $"Cover depth should not be negative at station {station.Chainage}");
            }
        }

        [TestMethod]
        public void UT7_HCPipe_GradientCalculation()
        {
            var mhStart = new HCManhole("MH-01", new Point2d(0, 0), rimLevel: 100.0, invertLevel: 98.5);
            var mhEnd   = new HCManhole("MH-02", new Point2d(50, 0), rimLevel: 100.2, invertLevel: 98.3);
            var pipe = new HCPipe("P-01", mhStart, mhEnd, diameter: 300)
            {
                Length = 50.0
            };

            Assert.AreEqual(0.004, pipe.Gradient, 1e-6, "Gradient should be (98.5 - 98.3) / 50 = 0.004 m/m");
            Assert.AreEqual(0.4, pipe.GradientPercent, 1e-4, "Gradient % should be 0.4%");
        }

        [TestMethod]
        public void UT8_HCManhole_DepthCalculation()
        {
            var mh = new HCManhole("MH-01", new Point2d(0, 0), rimLevel: 100.5, invertLevel: 98.0);
            Assert.AreEqual(2.5, mh.Depth, 1e-6, "Manhole depth should be 2.5 m");
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static IList<HCPoint> Get9PointGrid()
        {
            var points = new List<HCPoint>();
            int num = 1;
            for (int y = 0; y <= 2; y++)
                for (int x = 0; x <= 2; x++)
                    points.Add(new HCPoint(new Point3d(x, y, x + y * 0.1), num++));
            return points;
        }

        private static TerrainModel BuildFlatTerrainModel(double zLevel)
        {
            // Large flat terrain covering a 100×100 area
            var pts = new List<HCPoint>
            {
                new HCPoint(new Point3d(-10, -10, zLevel), 1),
                new HCPoint(new Point3d(100, -10, zLevel), 2),
                new HCPoint(new Point3d(100, 100, zLevel), 3),
                new HCPoint(new Point3d(-10, 100, zLevel), 4),
                new HCPoint(new Point3d(45,  45, zLevel), 5),
            };
            var t = new DelaunatorAdapter(pts);
            return t.Triangulate(false);
        }

        private static HCPolyline BuildStraightPolyline(double x1, double y1, double x2, double y2)
        {
            return new HCPolyline(new List<Point2d>
            {
                new Point2d(x1, y1),
                new Point2d(x2, y2),
            });
        }
    }
}
