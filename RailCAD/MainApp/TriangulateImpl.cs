using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RailCAD.CadInterface;
using RailCAD.CadInterface.Tools;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.Services.Triangulation;
using RailCAD.Views;

namespace RailCAD.MainApp
{
    partial class RCApp
    {
        private static void Triangulate(ICadModel cad)
        {
            // Obtain terrain data and user settings
            IDictionary<string, RCPoint> handleMap;
            IList<RCPoint> terrainPoints = cad.SelectPoints(Properties.Resources.TerrainModel_PromptSelectTerrainPoints);
            //IList<RCPoint> terrainPoints = cad.SelectPointsManually();  // prompt: user points selection
            //IList<RCPoint> terrainPoints = cad.SelectPointsManually(false);  // deve: all points (w/o xdata)

            if (terrainPoints != null)
            {
                handleMap = RCPoint.HandleMap(terrainPoints);
            }
            else
            {
                return;
            }

            IList<RCLine> terrainSegments = cad.SelectSegments(handleMap);

            UserSettings settings;
            if (!cad.PromptUserSettings(out settings))
                return;

            // timer start
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // triangulate
            ITriangulator triangulator = new DelanuatorAdapter(terrainPoints, terrainSegments);
            TerrainModel terrainModel = triangulator.Triangulate(settings.considerTriangleAreaForNormals);
            if (terrainModel == null)
                return;

            cad.WriteMessageNoDebug(Properties.Resources.TerrainModel_TerrainModelHasBeenCreated);
            cad.WriteMessage($"Created terrain model: {terrainModel.Name}");
            cad.WriteMessage($"Triangles: {terrainModel.Triangles.Count()}");

            // save to app data
            RCAppData.Instance.SetTerrainModel(terrainModel);

            //((DelanuatorAdapter)triangulator).ExploreTriangulation(cad);  // debug

            // visualize triangles
            if (terrainModel.IsEmpty())
            {
                cad.WriteMessage("Empty triangulation");
            }
            else
            {
                cad.WriteTriangulation(terrainModel, settings.showTriangles);
            }

            // timer stop
            cad.SaveTerrainModel(terrainModel);
            sw.Stop();
            cad.WriteMessage($"Elapsed={sw.Elapsed}");
        }
    }
}
