using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;

namespace RailCAD.MainApp
{
    partial class RCApp
    {
        private static void PointsHeight(ICadModel cad)
        {
            // timer start
            Stopwatch sw = new Stopwatch();
            sw.Start();

            var inputArgs = (Tuple<string, IList<Point2d>>)cad.ReadAndValidateLispArgs(ResBufIO.ReadCsPthInput);
            if (inputArgs != null)
            {
                string appName = inputArgs.Item1;

                // get terrain model from the app data or read it from CAD it if not exists
                // todo: cache invalidation
                bool USE_CACHE = true;
                TerrainModel terrainModel = null;
                if (USE_CACHE)
                {
                    terrainModel = RCAppData.Instance.GetTerrainModel(appName);
                    cad.WriteMessage($"Terrain model exists: {terrainModel != null}");
                    if (terrainModel != null) cad.WriteMessage($"Triangles: {terrainModel.Triangles.Count()}");
                }

                if (terrainModel == null)
                {
                    terrainModel = cad.LoadTerrainModel(appName);
                    if (terrainModel == null)
                        terrainModel = cad.ReadTerrainModel(appName);
                    RCAppData.Instance.SetTerrainModel(terrainModel);
                }

                cad.WriteMessage($"point heights: {inputArgs.Item2.Count}");
                var outPoints = new List<Point3d>(inputArgs.Item2.Count);
                for (int i = 0; i < inputArgs.Item2.Count; i++)
                {
                    Point2d point = inputArgs.Item2[i];

                    // add regular point
                    RCTriangle triangle = terrainModel.GetTriangle(point);
                    double height = terrainModel.GetPointHeight(point, triangle);
                    if (!Double.IsNaN(height))
                    {
                        outPoints.Add(point.ToPoint3d(height));
                    }

                    // intermediate points calcualated only at fixed segments
                    if (triangle != null && triangle.Lines.Any(l => RC_SPOJNICE.FIXED_SEGMENT.HasFlag(l.Type)))
                    {
                        // consider also virtual points on the edges between tho subsequent points
                        if (i < inputArgs.Item2.Count - 1)
                        {
                            Point2d nextPoint = inputArgs.Item2[i + 1];
                            RCTriangle nextTriangle = terrainModel.GetTriangle(nextPoint);

                            Point2d? intermediatePoint = terrainModel.GetIntermediatePoint(point, nextPoint, triangle, nextTriangle);
                            if (intermediatePoint != null)
                            {
                                double heightIntermediate = terrainModel.GetPointHeight(intermediatePoint.Value, nextTriangle);
                                if (!Double.IsNaN(heightIntermediate))
                                {
                                    outPoints.Add(intermediatePoint.Value.ToPoint3d(heightIntermediate));
                                }
                            }
                        }
                    }
                }

                cad.WriteMessage($"used fallbacks: {terrainModel.GetNumberOfUsedFallbacksInSearchStrategy()}");

                if (outPoints.Count > 0)
                {
                    cad.SetLispResp(ResBufIO.WriteCsPthResp, outPoints);
                }
            }

            // timer stop
            sw.Stop();
            cad.WriteMessage($"Elapsed={sw.Elapsed}");
        }
    }
}
