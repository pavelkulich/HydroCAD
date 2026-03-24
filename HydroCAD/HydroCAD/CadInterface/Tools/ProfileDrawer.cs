#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcEd = Autodesk.AutoCAD.EditorInput;
    using _AcGe = Autodesk.AutoCAD.Geometry;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
    using _AcEd = Bricscad.EditorInput;
    using _AcGe = Teigha.Geometry;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcEd = Gssoft.Gscad.EditorInput;
    using _AcGe = Gssoft.Gscad.Geometry;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcEd = ZwSoft.ZwCAD.EditorInput;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
#endif

using System;
using System.Collections.Generic;
using HydroCAD.CadInterface.Tools;
using HydroCAD.Models.Geometry;
using HydroCAD.Profile;

namespace HydroCAD.CadInterface
{
    /// <summary>
    /// Draws a longitudinal hydraulic profile in the CAD drawing.
    ///
    /// Profile layout (drawn at InsertionPoint):
    ///
    ///   ┌─────── profileWidth ────────┐
    ///   │                             │  ← profileHeight (scaled elevation range + margins)
    ///   │   ~~~~~  ← ground line      │
    ///   │   ─────  ← pipe invert      │
    ///   ├──────────────────────────────┤
    ///   │ Stations │ 0.0 │ 5.0 │ ...  │  ← header grid
    ///   │ GL       │     │     │ ...  │
    ///   │ IL       │     │     │ ...  │
    ///   │ Cover    │     │     │ ...  │
    ///   └──────────────────────────────┘
    /// </summary>
    internal static class ProfileDrawer
    {
        // Grid row heights in drawing units
        private const double ROW_HEADER_HEIGHT = 8.0;
        private const double PROFILE_MARGIN = 2.0;
        private const double TEXT_HEIGHT = 2.5;
        private const double LABEL_WIDTH = 30.0;

        // Layer names
        private const string LAYER_GROUND = "HC-GROUND";
        private const string LAYER_PIPE = "HC-PIPE";
        private const string LAYER_GRID = "HC-GRID";
        private const string LAYER_TEXT = "HC-TEXT";
        private const string LAYER_MANHOLE = "HC-MANHOLE";

        public static void Draw(
            _AcDb.Database db,
            _AcDb.Transaction tr,
            ProfileData profileData,
            ProfileSettings settings,
            Point2d insertionPoint)
        {
            if (profileData == null || profileData.Stations.Count == 0) return;

            EnsureLayers(db, tr);

            _AcDb.BlockTableRecord modelSpace = GetModelSpace(db, tr);

            double hScale = settings.HorizontalScale;
            double vScale = settings.VerticalScale;

            double totalLength = profileData.TotalLength;
            double profileWidth = totalLength / hScale;

            // Determine elevation range for drawing
            double minElev = Math.Min(profileData.MinInvertLevel, profileData.MinGroundLevel);
            double maxElev = profileData.MaxGroundLevel;
            double elevRange = maxElev - minElev;
            double profileHeight = (elevRange / vScale) + 2 * PROFILE_MARGIN;

            double gridTop = insertionPoint.Y; // top of grid (header)
            double graphBottom = insertionPoint.Y + 4 * ROW_HEADER_HEIGHT; // bottom of graph area
            double graphTop = graphBottom + profileHeight;

            // Helper: convert (chainage, elevation) → drawing coordinates
            Point2d ToDrawPt(double chainage, double elev)
            {
                double x = insertionPoint.X + LABEL_WIDTH + chainage / hScale;
                double y = graphBottom + (elev - minElev) / vScale;
                return new Point2d(x, y);
            }

            // 1. Draw ground line
            var groundPts = new List<_AcGe.Point2d>();
            foreach (var station in profileData.Stations)
            {
                if (!double.IsNaN(station.GroundLevel))
                {
                    var pt = ToDrawPt(station.Chainage, station.GroundLevel);
                    groundPts.Add(pt.ToAcGePoint2d());
                }
            }
            AddPolyline(modelSpace, tr, groundPts, LAYER_GROUND);

            // 2. Draw pipe invert line
            var invertPts = new List<_AcGe.Point2d>();
            foreach (var station in profileData.Stations)
            {
                if (!double.IsNaN(station.InvertLevel))
                {
                    var pt = ToDrawPt(station.Chainage, station.InvertLevel);
                    invertPts.Add(pt.ToAcGePoint2d());
                }
            }
            AddPolyline(modelSpace, tr, invertPts, LAYER_PIPE);

            // 3. Draw pipe crown line (invert + diameter)
            var crownPts = new List<_AcGe.Point2d>();
            foreach (var station in profileData.Stations)
            {
                if (!double.IsNaN(station.CrownLevel))
                {
                    var pt = ToDrawPt(station.Chainage, station.CrownLevel);
                    crownPts.Add(pt.ToAcGePoint2d());
                }
            }
            AddPolyline(modelSpace, tr, crownPts, LAYER_PIPE);

            // 4. Draw manholes (vertical lines from invert to ground)
            foreach (var station in profileData.ManholeStations)
            {
                if (!double.IsNaN(station.InvertLevel) && !double.IsNaN(station.GroundLevel))
                {
                    var ptBottom = ToDrawPt(station.Chainage, station.InvertLevel);
                    var ptTop = ToDrawPt(station.Chainage, station.GroundLevel);
                    AddLine(modelSpace, tr, ptBottom, ptTop, LAYER_MANHOLE);

                    // Manhole ID label
                    AddText(modelSpace, tr,
                        new Point2d(ptTop.X, ptTop.Y + TEXT_HEIGHT),
                        station.ManholeId ?? string.Empty,
                        TEXT_HEIGHT, LAYER_TEXT);
                }
            }

            // 5. Draw header grid
            DrawHeaderGrid(modelSpace, tr, profileData, settings, insertionPoint,
                           profileWidth, gridTop, graphBottom);
        }

        private static void DrawHeaderGrid(
            _AcDb.BlockTableRecord modelSpace,
            _AcDb.Transaction tr,
            ProfileData profileData,
            ProfileSettings settings,
            Point2d origin,
            double profileWidth,
            double gridTop,
            double graphBottom)
        {
            double hScale = settings.HorizontalScale;

            // Row labels
            string[] rowLabels = { "Station (m)", "Terrain (m)", "Invert (m)", "Cover (m)" };
            double rowY = gridTop;

            // Outer border
            DrawRectangle(modelSpace, tr, origin,
                new Point2d(origin.X + LABEL_WIDTH + profileWidth, gridTop - 4 * ROW_HEADER_HEIGHT),
                LAYER_GRID);

            for (int row = 0; row < rowLabels.Length; row++)
            {
                double y = gridTop - row * ROW_HEADER_HEIGHT;
                AddLine(modelSpace, tr,
                    new Point2d(origin.X, y),
                    new Point2d(origin.X + LABEL_WIDTH + profileWidth, y),
                    LAYER_GRID);

                AddText(modelSpace, tr,
                    new Point2d(origin.X + 1, y - ROW_HEADER_HEIGHT + (ROW_HEADER_HEIGHT - TEXT_HEIGHT) / 2),
                    rowLabels[row], TEXT_HEIGHT, LAYER_TEXT);
            }

            // Vertical separator after label column
            AddLine(modelSpace, tr,
                new Point2d(origin.X + LABEL_WIDTH, gridTop),
                new Point2d(origin.X + LABEL_WIDTH, gridTop - 4 * ROW_HEADER_HEIGHT),
                LAYER_GRID);

            // Station columns and values
            foreach (var station in profileData.Stations)
            {
                double xCol = origin.X + LABEL_WIDTH + station.Chainage / hScale;

                // Vertical column line
                AddLine(modelSpace, tr,
                    new Point2d(xCol, gridTop),
                    new Point2d(xCol, gridTop - 4 * ROW_HEADER_HEIGHT),
                    LAYER_GRID);

                // Row values
                string[] values = {
                    $"{station.Chainage:F1}",
                    double.IsNaN(station.GroundLevel)  ? "-" : $"{station.GroundLevel:F3}",
                    double.IsNaN(station.InvertLevel)  ? "-" : $"{station.InvertLevel:F3}",
                    double.IsNaN(station.CoverDepth)   ? "-" : $"{station.CoverDepth:F3}",
                };

                for (int row = 0; row < values.Length; row++)
                {
                    double yText = gridTop - row * ROW_HEADER_HEIGHT - (ROW_HEADER_HEIGHT - TEXT_HEIGHT) / 2 - TEXT_HEIGHT;
                    AddText(modelSpace, tr, new Point2d(xCol + 0.5, yText), values[row], TEXT_HEIGHT * 0.8, LAYER_TEXT);
                }
            }
        }

        // ── Drawing helpers ────────────────────────────────────────────────

        private static void AddPolyline(
            _AcDb.BlockTableRecord ms, _AcDb.Transaction tr,
            List<_AcGe.Point2d> pts, string layer)
        {
            if (pts == null || pts.Count < 2) return;
            var pline = new _AcDb.Polyline(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                pline.AddVertexAt(i, pts[i], 0, 0, 0);
            pline.Layer = layer;
            ms.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }

        private static void AddLine(
            _AcDb.BlockTableRecord ms, _AcDb.Transaction tr,
            Point2d start, Point2d end, string layer)
        {
            var line = new _AcDb.Line(start.ToAcGePoint3d(), end.ToAcGePoint3d());
            line.Layer = layer;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private static void AddText(
            _AcDb.BlockTableRecord ms, _AcDb.Transaction tr,
            Point2d position, string text, double height, string layer)
        {
            var dbText = new _AcDb.DBText
            {
                Position = position.ToAcGePoint3d(),
                TextString = text,
                Height = height,
                Layer = layer,
            };
            ms.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
        }

        private static void DrawRectangle(
            _AcDb.BlockTableRecord ms, _AcDb.Transaction tr,
            Point2d topLeft, Point2d bottomRight, string layer)
        {
            var pline = new _AcDb.Polyline(4);
            pline.AddVertexAt(0, topLeft.ToAcGePoint2d(), 0, 0, 0);
            pline.AddVertexAt(1, new _AcGe.Point2d(bottomRight.X, topLeft.Y), 0, 0, 0);
            pline.AddVertexAt(2, bottomRight.ToAcGePoint2d(), 0, 0, 0);
            pline.AddVertexAt(3, new _AcGe.Point2d(topLeft.X, bottomRight.Y), 0, 0, 0);
            pline.Closed = true;
            pline.Layer = layer;
            ms.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }

        private static void EnsureLayers(_AcDb.Database db, _AcDb.Transaction tr)
        {
            var layerTable = (_AcDb.LayerTable)tr.GetObject(db.LayerTableId, _AcDb.OpenMode.ForWrite);
            foreach (string name in new[] { LAYER_GROUND, LAYER_PIPE, LAYER_GRID, LAYER_TEXT, LAYER_MANHOLE })
            {
                if (!layerTable.Has(name))
                {
                    var ltr = new _AcDb.LayerTableRecord { Name = name };
                    layerTable.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
            }
        }

        private static _AcDb.BlockTableRecord GetModelSpace(_AcDb.Database db, _AcDb.Transaction tr)
        {
            var bt = (_AcDb.BlockTable)tr.GetObject(db.BlockTableId, _AcDb.OpenMode.ForRead);
            return (_AcDb.BlockTableRecord)tr.GetObject(bt[_AcDb.BlockTableRecord.ModelSpace], _AcDb.OpenMode.ForWrite);
        }
    }
}
