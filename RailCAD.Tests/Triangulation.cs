using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System;

using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.Services.Triangulation;
using RailCAD.CadInterface.Tools;

namespace RailCAD.Tests
{
    [TestClass]
    public class Triangulation
    {
        private IList<RCPoint> GetUT1Points()
        {
            // all points are placed in XY plane (Z=0)
            RC_BOD type = RC_BOD.ZAKLAD;
            IList<RCPoint> points = new List<RCPoint>()
            {
                new RCPoint(new Point3d(-4, -1, 0), number: 1, type: type),  // A = 1
                new RCPoint(new Point3d(-6, -4, 0), number: 2, type: type),  // B = 2
                new RCPoint(new Point3d( 3,  1, 0), number: 3, type: type),  // C = 3
                new RCPoint(new Point3d( 1, -2, 0), number: 4, type: type),  // D = 4
                new RCPoint(new Point3d( 5, -4, 0), number: 5, type: type),  // E = 5
                new RCPoint(new Point3d( 0,  7, 0), number: 6, type: type),  // F = 6
                new RCPoint(new Point3d( 5,  5, 0), number: 7, type: type),  // G = 7
                new RCPoint(new Point3d(-5,  5, 0), number: 8, type: type),  // H = 8
                new RCPoint(new Point3d(-2,  3, 0), number: 9, type: type),  // I = 9
            };
            return points;
        }

        private Tuple<TerrainModel, IList<RCPoint>> GetUT6TerrainModel()
        {
            // input data
            IList<RCPoint> rcPoints = new List<RCPoint>
            {
                new RCPoint(new Point3d(5.48, 5.3, 321.18), 202, "A3E5C", RC_BOD.ZAKLAD),
                new RCPoint(new Point3d(6.87, 0.7, 320.74), 213, "A3E67", RC_BOD.ZAKLAD),
                new RCPoint(new Point3d(2.22, 8.54, 319.59), 225, "A3E73", RC_BOD.ZAKLAD),
                new RCPoint(new Point3d(2.34, 2.46, 322.65), 229, "A3E77", RC_BOD.ZAKLAD),
                new RCPoint(new Point3d(9.22, 6.7, 316.21), 234, "A3E7C", RC_BOD.ZAKLAD),
            };

            foreach (RCPoint point in rcPoints)
            {
                if (point.Number != 202)
                {
                    point.SetIsHranice();
                }
            }

            IDictionary<int, HashSet<string>> neighborsHandles = new Dictionary<int, HashSet<string>>
            {
                { 202, new HashSet<string> {"A3E7C", "A3E67", "A3E77", "A3E73"} },
                { 213, new HashSet<string> {"A3E7C", "A3E5C", "A3E77"} },
                { 225, new HashSet<string> {"A3E77", "A3E5C", "A3E7C"} },
                { 229, new HashSet<string> {"A3E73", "A3E5C", "A3E67"} },
                { 234, new HashSet<string> {"A3E73", "A3E5C", "A3E67"} },
            };

            IDictionary<string, RCPoint> ptHandleMap = new Dictionary<string, RCPoint>();
            foreach (RCPoint pt in rcPoints)
            {
                ptHandleMap.Add(pt.Handle, pt);
                pt.SetNeighborsHandles(neighborsHandles[pt.Number]);
            }

            IList<RCLine> fixedSegments = new List<RCLine>
            {
                new RCLine(rcPoints[0], rcPoints[1], RC_SPOJNICE.HRANA),  // 202-213 
            };

            TerrainModel terrainModel = TerrainModel.CreateTerrainModelFromDeserializedData(rcPoints, ptHandleMap, fixedSegments);
            return new Tuple<TerrainModel, IList<RCPoint>>(terrainModel, rcPoints);
        }

        [TestMethod]
        public void UT1_Triangulation()
        {
            IList<RCPoint> points = GetUT1Points();

            // create triangulation
            ITriangulator triangulator = new DelanuatorAdapter(points);
            TerrainModel terrainModel = triangulator.Triangulate(false);

            // validate triangulation
            Assert.IsNotNull(terrainModel, "model: null");
            Assert.IsFalse(terrainModel.IsEmpty(), "model: empty");

            Assert.AreEqual(terrainModel.Points.Count(), points.Count, "model: points");
            Assert.AreEqual(terrainModel.Lines.Count(), 19, "model: lines");
            Assert.AreEqual(terrainModel.Triangles.Count(), 11, "model: triangles");

            // triangles
            RCTriangle triIHF = terrainModel.Triangles.ElementAt(0);
            ValidateTriangle(triIHF, "IHF", 1, new int[] { 9, 8, 6 }, 8.0, new Vector3d(0, 0, 1));

            RCTriangle triABH = terrainModel.Triangles.ElementAt(7);
            ValidateTriangle(triABH, "ABH", 8, new int[] { 1, 2, 8 }, 7.5, new Vector3d(0, 0, 1));

            RCTriangle triCDI = terrainModel.Triangles.ElementAt(5);
            ValidateTriangle(triCDI, "CDI", 6, new int[] { 3, 4, 9 }, 9.5, new Vector3d(0, 0, 1));

            RCTriangle triGEC = terrainModel.Triangles.ElementAt(8);
            ValidateTriangle(triGEC, "GEC", 9, new int[] { 7, 5, 3 }, 9.0, new Vector3d(0, 0, 1));

            // points
            // point A
            RCPoint ptA = terrainModel.Points.ElementAt(0);
            Assert.AreEqual(ptA.Number, 1, "pt A: id");
            Assert.IsTrue(ptA.Normal.IsEqual(new Vector3d(0, 0, 1)), "pt A: normal");
            Assert.AreEqual(ptA.Lines.Count(), 4, "pt A: lines");
            Assert.AreEqual(ptA.Triangles.Count(), 4, "pt A: triangles");

            // point G
            RCPoint ptG = terrainModel.Points.ElementAt(6);
            Assert.AreEqual(ptG.Number, 7, "pt G: id");
            Assert.IsTrue(ptG.Normal.IsEqual(new Vector3d(0, 0, 1)), "pt G: normal");
            Assert.AreEqual(ptG.Lines.Count(), 3, "pt G: lines");
            Assert.AreEqual(ptG.Triangles.Count(), 2, "pt G: triangles");
            Assert.AreEqual(ptG.Lines[0].Type, RC_SPOJNICE.HRANICE, "pt G: line 1");
            Assert.AreEqual(ptG.Lines[1].Type, RC_SPOJNICE.HRANICE, "pt G: line 2");
            Assert.AreEqual(ptG.Lines[2].Type, RC_SPOJNICE.TRIANG, "pt G: line 3");
        }

        void ValidateTriangle(RCTriangle triangle, string triangleName, int id, int[] ptIds, double area, Vector3d normal)
        {
            Assert.AreEqual(triangle.Id, id, $"tri {triangleName}: id");
            Assert.AreEqual(triangle.Points[0].Number, ptIds[0], $"tri {triangleName}: pt 1");
            Assert.AreEqual(triangle.Points[1].Number, ptIds[1], $"tri {triangleName}: pt 2");
            Assert.AreEqual(triangle.Points[2].Number, ptIds[2], $"tri {triangleName}: pt 3");
            Utils.AssertEqualsWithTol(triangle.Area, area, $"tri {triangleName}: area");
            Assert.IsTrue(triangle.Normal.IsEqual(normal), $"tri {triangleName}: normal");
        }

        [TestMethod]
        public void UT2_TriangulationNormal()
        {
            RC_BOD type = RC_BOD.ZAKLAD;
            List<RCPoint> points = new List<RCPoint>()
            {
                new RCPoint(new Point3d( 0,  0, 1), number: 1, type: type),  // A = 1
                new RCPoint(new Point3d( 2,  2, 0), number: 2, type: type),  // B = 2
                new RCPoint(new Point3d( 1, -1, 0), number: 3, type: type),  // C = 3
                new RCPoint(new Point3d(-1, -1, 0), number: 4, type: type),  // D = 4
                new RCPoint(new Point3d(-1,  1, 0), number: 5, type: type),  // E = 5
            };

            // create triangulation (triangle area not considered
            ITriangulator triangulator = new DelanuatorAdapter(points);
            TerrainModel terrainModel = triangulator.Triangulate(false);
            RCPoint ptA = terrainModel.Points.ElementAt(0);
            Assert.IsTrue(
                ptA.Normal.IsEqual(new Vector3d(-0.104394615283834, -0.104394615283834, 0.989041722375492)),
                "pt A: normal (triangle area is not considered)"
                );

            TerrainModel terrainModel2 = triangulator.Triangulate(true);
            RCPoint ptA2 = terrainModel2.Points.ElementAt(0);
            Assert.IsTrue(
                ptA2.Normal.IsEqual(new Vector3d(0.0169890619457002, 0.0169890619457002, 0.999711330109052)),
                "pt A: normal (triangle area is considered)"
                );
        }

        [TestMethod]
        public void UT3_ConstrainedTriangulation1()
        {
            RC_BOD type = RC_BOD.ZAKLAD;
            List<RCPoint> points = new List<RCPoint>()
            {
                new RCPoint(new Point3d( 2,  2, 0), number: 2, type: type),  // B = 2
                new RCPoint(new Point3d( 1, -1, 0), number: 3, type: type),  // C = 3
                new RCPoint(new Point3d(-1, -1, 0), number: 4, type: type),  // D = 4
                new RCPoint(new Point3d(-1,  1, 0), number: 5, type: type),  // E = 5
            };

            // add fixed segments; triangles: CDE, EBC -> CDB, EBD
            var segments = new List<RCLine>();
            segments.Add(new RCLine(points[2], points[0], RC_SPOJNICE.HRANA));  // D-B

            ITriangulator triangulator = new DelanuatorAdapter(points, segments);
            TerrainModel terrainModel = triangulator.Triangulate(false);

            RCTriangle triCDB = terrainModel.Triangles.ElementAt(0);
            ValidateTriangle(triCDB, "CDB", 1, new int[] { 3, 4, 2 }, 3, new Vector3d(0, 0, 1));

            RCTriangle triEBD = terrainModel.Triangles.ElementAt(1);
            ValidateTriangle(triEBD, "EBD", 2, new int[] { 5, 2, 4 }, 3, new Vector3d(0, 0, 1));
        }

        [TestMethod]
        public void UT4_ConstrainedTriangulation2()
        {
            RC_BOD type = RC_BOD.ZAKLAD;
            List<RCPoint> points = new List<RCPoint>()
            {
                new RCPoint(new Point3d( 1,  2, 0), number: 1, type: type),  // A = 1
                new RCPoint(new Point3d( 1,  1, 0), number: 2, type: type),  // B = 2
                new RCPoint(new Point3d( 1, -1, 0), number: 3, type: type),  // C = 3
                new RCPoint(new Point3d(-1, -1, 0), number: 4, type: type),  // D = 4
                new RCPoint(new Point3d(-1,  1, 0), number: 5, type: type),  // E = 5
            };

            //// add fixed segments; triangles: BEA, BCE, CDE -> DEA, BDA, CDB
            var segments = new List<RCLine>();
            segments.Add(new RCLine(points[0], points[3], RC_SPOJNICE.HRANA));  // A-D

            ITriangulator triangulator = new DelanuatorAdapter(points, segments);
            TerrainModel terrainModel = triangulator.Triangulate(false);

            RCTriangle triDEA = terrainModel.Triangles.ElementAt(0);
            ValidateTriangle(triDEA, "DEA", 1, new int[] { 4, 5, 1 }, 2, new Vector3d(0, 0, 1));

            RCTriangle triBDA = terrainModel.Triangles.ElementAt(1);
            ValidateTriangle(triBDA, "BDA", 2, new int[] { 2, 4, 1 }, 1, new Vector3d(0, 0, 1));

            RCTriangle triCDB = terrainModel.Triangles.ElementAt(2);
            ValidateTriangle(triCDB, "CDB", 3, new int[] { 3, 4, 2 }, 2, new Vector3d(0, 0, 1));
        }

        [TestMethod]
        public void UT5_ConstrainedTriangulation3()
        {
            RC_BOD type = RC_BOD.ZAKLAD;
            List<RCPoint> points = new List<RCPoint>()
            {
                new RCPoint(new Point3d( 0.0,  0.0, 0.0),   number: 1, type: type),  // A = 1
                new RCPoint(new Point3d(27.9,  1.1, 0.474), number: 2, type: type),  // B = 2
                new RCPoint(new Point3d(-2.5, -7.3, 0.021), number: 3, type: type),  // C = 3
                new RCPoint(new Point3d(29.4, -9.2, 0.644), number: 4, type: type),  // D = 4
                new RCPoint(new Point3d(10.2,-17.2, 0.560), number: 5, type: type),  // E = 5
                new RCPoint(new Point3d( 5.7,-13.7, 0.413), number: 6, type: type),  // F = 6
            };

            // pevne spojnice (fixed segments)
            var segments = new List<RCLine>
            {
                new RCLine(points[2], points[3], RC_SPOJNICE.HRANA)  // C-D
            };

            ITriangulator triangulator = new DelanuatorAdapter(points, segments);
            TerrainModel terrainModel = triangulator.Triangulate(false);

            RCTriangle triFDE = terrainModel.Triangles.ElementAt(0);
            ValidateTriangle(triFDE, "FDE", 1, new int[] { 6, 4, 5 }, 51.6, new Vector3d(-0.0142387484123358, 0.0236770045027983, 0.999618255886428));

            RCTriangle triBDA = terrainModel.Triangles.ElementAt(1);
            ValidateTriangle(triBDA, "BDA", 2, new int[] { 2, 4, 1 }, 144.51, new Vector3d(-0.0175348677914375, 0.0139470896660372, 0.999748972043175));

            RCTriangle triCAD = terrainModel.Triangles.ElementAt(2);
            ValidateTriangle(triCAD, "CAD", 3, new int[] { 3, 1, 4 }, 118.81, new Vector3d(-0.0189672209097242, 0.00937169207061218, 0.999776182912303));
            
            RCTriangle triFCD = terrainModel.Triangles.ElementAt(3);
            ValidateTriangle(triFCD, "FCD", 4, new int[] { 6, 3, 4 }, 94.29, new Vector3d(-0.0171780199866502, 0.0391845766793926, 0.999084323057766));
        }

        [TestMethod]
        public void UT6_1_PointsHeight()
        {
            var terrainModelData = GetUT6TerrainModel();
            var terrainModel = terrainModelData.Item1;
            var rcPoints = terrainModelData.Item2;

            Assert.IsNotNull(terrainModel);
            Assert.AreEqual(terrainModel.Triangles.Count(), 4);

            // inside point
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(new Point2d(4, 6)), 320.846349114862);

            // outside point
            Assert.IsTrue(double.IsNaN(terrainModel.GetPointHeight(new Point2d(0, 6))));

            // middle vertex
            var pt202 = rcPoints[0];
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(pt202.Point2d), pt202.Point3d.Z);

            // edge vertex
            var pt213 = rcPoints[1];
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(pt213.Point2d), pt213.Point3d.Z);
        }

        [TestMethod]
        public void UT6_2_PointsHeight()
        {
            var terrainModelData = GetUT6TerrainModel();
            var terrainModel = terrainModelData.Item1;

            Assert.IsNotNull(terrainModel);
            Assert.AreEqual(terrainModel.Triangles.Count(), 4);

            Point2d point1 = new Point2d(4, 6);
            Point2d point2 = new Point2d(5, 3);  // next triangle (common edge with point1)
            Point2d point3 = new Point2d(7, 3);  // two triangles away (no common edge)
            Point2d point4 = new Point2d(0, 6);  // outside of the terrain model (no common edge)

            // first point
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(point1), 320.846349114862);

            // second point
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(point2), 321.471843993997);

            // intermediate point - on non-fixed segment 202-229
            Point2d point1_2 = terrainModel.GetIntermediatePoint(point1, point2, RC_SPOJNICE.POSSIBLE_TYPES_FOR_DEFINITION).Value;
            Utils.AssertEqualsWithTol(point1_2.X, 4.5221207177814);
            Utils.AssertEqualsWithTol(point1_2.Y, 4.43363784665579);
            Utils.AssertEqualsWithTol(terrainModel.GetPointHeight(point1_2), 321.628433931485);

            // no intermediate points
            Assert.IsNull(terrainModel.GetIntermediatePoint(point1, point3));
            Assert.IsNull(terrainModel.GetIntermediatePoint(point1, point4));
        }

        [TestMethod]
        public void UT7_SortedNeighborsForXDataWrite()
        {
            // directly testing XDataWrite (ResultBuffer) is not possible
            // https://forums.autodesk.com/t5/net/acdbmgd-dll-exceptions-when-unit-testing/m-p/8773136/highlight/true#M62538

            TerrainModel terrainModel = GetUT6TerrainModel().Item1;
            RCPoint rcPoint202 = terrainModel.Points.ElementAt(0);  // 202 (middle)
            RCPoint rcPoint213 = terrainModel.Points.ElementAt(1);  // 213 (hranice)

            SortedDictionary<double, IRCEntity> neighborEntities202 = CadXDataWriter.GetSortedNeighborEntitiesForXDataWrite(rcPoint202);
            SortedDictionary<double, IRCEntity> neighborEntities213 = CadXDataWriter.GetSortedNeighborEntitiesForXDataWrite(rcPoint213);

            Assert.IsTrue(neighborEntities202.Count == 7);  // 1x segment point (1x point + 1x line) (2), 3x point (3), + duplicated first point (2)
            Assert.IsTrue(neighborEntities213.Count == 9);  // 2x hranice points (1x point + 1x segment) (4), 1x segment point (2x point + 2x line + 1x normal) (5)

            // visual validation
            PrintNeighborsInfo(rcPoint202, neighborEntities202);
            PrintNeighborsInfo(rcPoint213, neighborEntities213);
        }

        private void PrintNeighborsInfo(RCPoint rcPoint, SortedDictionary<double, IRCEntity> neighborEntities)
        {
            Console.WriteLine($"XData neighbors for point: {rcPoint.Number}");
            Console.WriteLine($"point.IsHranice: {rcPoint.IsHranice}");
            foreach (var entry in neighborEntities)
            {
                Console.WriteLine($"{entry.Value.GetType().Name}: {entry.Value}  (angle={entry.Key})");
            }
            Console.WriteLine();
        }
    }
}
