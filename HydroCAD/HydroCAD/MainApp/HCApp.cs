using System;
using System.Collections.Generic;
using HydroCAD.CadInterface;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.Network;
using HydroCAD.Models.TerrainModel;
using HydroCAD.Profile;
using HydroCAD.Services.Triangulation;

namespace HydroCAD.MainApp
{
    /// <summary>
    /// Main application orchestration layer.
    /// All public static methods are called by CAD commands and Lisp functions.
    /// </summary>
    internal static class HCApp
    {
        // ──────────────────────────────────────────────────────────────────
        // Public entry points (called by Commands.cs)
        // ──────────────────────────────────────────────────────────────────

        public static void FunctionImportPoints(ICadModel cad)
        {
            Execute(ImportPoints, cad);
        }

        public static void FunctionBuildTerrainModel(ICadModel cad)
        {
            Execute(BuildTerrainModel, cad);
        }

        public static void FunctionDeleteTerrainModel(ICadModel cad)
        {
            Execute(DeleteTerrainModel, cad);
        }

        public static void FunctionDrawProfile(ICadModel cad)
        {
            Execute(DrawProfile, cad);
        }

        public static void FunctionGetPointsHeight(ICadModel cad)
        {
            ExecuteFunction(GetPointsHeight, cad);
        }

        // ──────────────────────────────────────────────────────────────────
        // Implementation
        // ──────────────────────────────────────────────────────────────────

        private static void ImportPoints(ICadModel cad)
        {
            cad.ImportPoints();
        }

        private static void BuildTerrainModel(ICadModel cad)
        {
            // Prompt user for settings
            if (!cad.PromptUserSettings(out UserSettings settings)) return;

            // Select survey points from the drawing
            IList<HCPoint> points = cad.SelectAllPoints();
            if (points == null || points.Count < 3)
            {
                cad.WriteMessageNoDebug(Properties.Resources.TerrainModel_NotEnoughPoints);
                return;
            }

            // Optionally select fixed segments (break lines, ridge lines, etc.)
            IList<HCLine> segments = cad.SelectSegments(HCPoint.HandleMap(points), requireXData: false);

            // Triangulate
            var triangulator = new DelaunatorAdapter(points, segments);
            TerrainModel terrainModel = triangulator.Triangulate(settings.ConsiderTriangleAreaForNormals);

            if (terrainModel == null || terrainModel.IsEmpty())
            {
                cad.WriteMessageNoDebug(Properties.Resources.TerrainModel_TriangulationFailed);
                return;
            }

            // Draw and save the result
            cad.WriteTriangulation(terrainModel, settings.ShowTriangles);
            cad.SaveTerrainModel(terrainModel);

            cad.WriteMessageNoDebug(string.Format(Properties.Resources.TerrainModel_CreatedSuccessfully,
                                                   terrainModel.Points.Count(),
                                                   terrainModel.Triangles.Count()));
        }

        private static void DeleteTerrainModel(ICadModel cad)
        {
            cad.DeleteTerrainModel();
        }

        private static void DrawProfile(ICadModel cad)
        {
            // Load terrain model
            TerrainModel terrainModel = cad.LoadTerrainModel(null);

            // Prompt user to select the pipe route polyline and enter profile settings
            if (!cad.PromptProfileSettings(out ProfileSettings profileSettings)) return;

            HCPolyline centreline = cad.SelectPolyline(Properties.Resources.Profile_SelectCentreline);
            if (centreline == null) return;

            // Build a temporary route from the selected centreline
            var route = new HCPipeRoute("ROUTE-1", centreline);

            // Ask for manholes along the route (optional)
            cad.SelectManholes(route, Properties.Resources.Profile_SelectManholes);

            // Generate profile data
            var generator = new ProfileGenerator(terrainModel);
            ProfileData profileData = generator.Generate(route, profileSettings);

            if (profileData == null || profileData.Stations.Count == 0)
            {
                cad.WriteMessageNoDebug(Properties.Resources.Profile_GenerationFailed);
                return;
            }

            // Ask for insertion point and draw the profile
            cad.DrawLongitudinalProfile(profileData, profileSettings);

            cad.WriteMessageNoDebug(Properties.Resources.Profile_DrawnSuccessfully);
        }

        private static void GetPointsHeight(ICadModel cad)
        {
            TerrainModel terrainModel = cad.LoadTerrainModel(null);
            if (terrainModel == null) return;

            IList<HCPoint> points = cad.SelectPoints(Properties.Resources.TerrainModel_SelectPoints);
            if (points == null || points.Count == 0) return;

            cad.SetPointsHeight(points, terrainModel);
        }

        // ──────────────────────────────────────────────────────────────────
        // Helpers
        // ──────────────────────────────────────────────────────────────────

        private static void Execute(Action<ICadModel> function, ICadModel cad)
        {
            try
            {
                function(cad);
            }
            catch (Exception ex)
            {
                cad.WriteMessage(ex.ToString());
                cad.WriteMessageNoDebug(Properties.Resources.ErrorOccurred);
            }
        }

        private static void ExecuteFunction(Action<ICadModel> function, ICadModel cad)
        {
            try
            {
                function(cad);
            }
            catch (Exception ex)
            {
                cad.WriteMessage(ex.ToString());
                cad.WriteMessageNoDebug(Properties.Resources.ErrorOccurred);
            }
        }
    }

    internal class UserSettings
    {
        public bool ConsiderTriangleAreaForNormals { get; set; }
        public bool ShowTriangles { get; set; }
    }
}
