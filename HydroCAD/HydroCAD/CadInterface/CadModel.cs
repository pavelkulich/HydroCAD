#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcEd = Autodesk.AutoCAD.EditorInput;
    using _AcGe = Autodesk.AutoCAD.Geometry;
    using _AcRt = Autodesk.AutoCAD.Runtime;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
    using _AcEd = Bricscad.EditorInput;
    using _AcGe = Teigha.Geometry;
    using _AcRt = Bricscad.Runtime;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcEd = Gssoft.Gscad.EditorInput;
    using _AcGe = Gssoft.Gscad.Geometry;
    using _AcRt = Gssoft.Gscad.Runtime;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcEd = ZwSoft.ZwCAD.EditorInput;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
    using _AcRt = ZwSoft.ZwCAD.Runtime;
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using HydroCAD.CadInterface.Tools;
using HydroCAD.MainApp;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.Network;
using HydroCAD.Models.TerrainModel;
using HydroCAD.Profile;
using HydroCAD.Views;

namespace HydroCAD.CadInterface
{
    internal class CadModel : ICadModel
    {
        private readonly _AcDb.ResultBuffer inputResbuf;

        public CadModel(_AcDb.ResultBuffer inputResbuf = null)
        {
            this.inputResbuf = inputResbuf;
        }

        internal _AcAp.Document Document => _AcAp.Application.DocumentManager.MdiActiveDocument;
        internal _AcDb.Database Database => Document.Database;
        internal _AcEd.Editor Editor => Document.Editor;

        // ── Messaging ────────────────────────────────────────────────────

        public void WriteMessage(object message)
        {
#if DEBUG
            Editor.WriteMessage($"DEBUG: {message}\n");
#endif
        }

        public void WriteMessageNoDebug(object message)
        {
            Editor.WriteMessage($"{message}\n");
        }

        // ── User interaction ─────────────────────────────────────────────

        public bool PromptUserSettings(out UserSettings settings)
        {
            settings = new UserSettings();
            bool ok = PromptYesNo(
                Properties.Resources.TerrainModel_PromptConsiderTriangleArea,
                Properties.Resources.No,
                out settings.ConsiderTriangleAreaForNormals);
            if (!ok) return false;
            return PromptYesNo(
                Properties.Resources.TerrainModel_PromptShowTriangles,
                Properties.Resources.No,
                out settings.ShowTriangles);
        }

        public bool PromptProfileSettings(out ProfileSettings settings)
        {
            settings = new ProfileSettings();
            var vm = new DrawProfileViewModel();
            var dlg = new DrawProfileDialog(vm);
            bool? result = dlg.ShowDialog();
            if (result == true)
            {
                settings = vm.GetSettings();
                return true;
            }
            return false;
        }

        // ── Lisp I/O ─────────────────────────────────────────────────────

        public _AcDb.ResultBuffer GetLispInputArgs() => inputResbuf;

        public void SetLispResp<T>(Func<T, _AcDb.ResultBuffer> writeRespFunction, T resp)
        {
            // Lisp response is set via editor result buffer mechanism
        }

        // ── Point operations ─────────────────────────────────────────────

        public void ImportPoints()
        {
            IList<HCPoint> points = ImportPointsDialog.ImportPointsFromFile(this);
            if (points == null || points.Count == 0) return;

            WritePointsToDrawing(points);
            WriteMessageNoDebug(string.Format(Properties.Resources.Points_ImportedCount, points.Count));
        }

        public IList<HCPoint> SelectPoints(string message = "")
        {
            var opts = new _AcEd.PromptSelectionOptions
            {
                MessageForAdding = string.IsNullOrEmpty(message)
                    ? Properties.Resources.TerrainModel_SelectPoints
                    : message
            };
            var filter = new _AcEd.SelectionFilter(
                new[] { new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "POINT") });

            _AcEd.PromptSelectionResult selRes = Editor.GetSelection(opts, filter);
            if (selRes.Status != _AcEd.PromptStatus.OK) return null;

            using var tr = Database.TransactionManager.StartTransaction();
            var points = CadXDataReader.CreatePointsFromSelection(selRes.Value, tr, requireXData: false);
            tr.Commit();
            return points;
        }

        public IList<HCPoint> SelectAllPoints()
        {
            var filter = new _AcEd.SelectionFilter(
                new[] { new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "POINT") });
            _AcEd.PromptSelectionResult selRes = Editor.SelectAll(filter);
            if (selRes.Status != _AcEd.PromptStatus.OK) return null;

            using var tr = Database.TransactionManager.StartTransaction();
            var points = CadXDataReader.CreatePointsFromSelection(selRes.Value, tr, requireXData: false);
            tr.Commit();
            return points;
        }

        public IList<HCLine> SelectSegments(IDictionary<string, HCPoint> handleMap, bool requireXData = false)
        {
            var opts = new _AcEd.PromptSelectionOptions
            {
                MessageForAdding = Properties.Resources.TerrainModel_SelectSegments,
                AllowDuplicates = false,
            };
            var filter = new _AcEd.SelectionFilter(
                new[] { new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "LINE,LWPOLYLINE") });

            _AcEd.PromptSelectionResult selRes = Editor.GetSelection(opts, filter);
            if (selRes.Status != _AcEd.PromptStatus.OK) return new List<HCLine>();

            using var tr = Database.TransactionManager.StartTransaction();
            var segments = CadXDataReader.CreateSegmentsFromSelection(selRes.Value, tr, requireXData, handleMap);
            tr.Commit();
            return segments;
        }

        public HCPolyline SelectPolyline(string message = "")
        {
            var opts = new _AcEd.PromptEntityOptions(
                string.IsNullOrEmpty(message) ? Properties.Resources.Profile_SelectCentreline : message);
            opts.SetRejectMessage(Properties.Resources.Profile_SelectPolylineOnly);
            opts.AddAllowedClass(typeof(_AcDb.Polyline), true);

            _AcEd.PromptEntityResult res = Editor.GetEntity(opts);
            if (res.Status != _AcEd.PromptStatus.OK) return null;

            using var tr = Database.TransactionManager.StartTransaction();
            var entity = tr.GetObject(res.ObjectId, _AcDb.OpenMode.ForRead) as _AcDb.Polyline;
            if (entity == null) return null;
            var poly = CadXDataReader.ReadPolyline(entity);
            tr.Commit();
            return poly;
        }

        public void SelectManholes(HCPipeRoute route, string message = "")
        {
            // Interactive manhole selection is optional – skipped for now
            // Users can pre-define manholes as CAD blocks or points
        }

        public void SetPointsHeight(IList<HCPoint> points, TerrainModel terrainModel)
        {
            if (points == null || terrainModel == null) return;
            using var tr = Database.TransactionManager.StartTransaction();
            foreach (var pt in points)
            {
                double height = terrainModel.GetPointHeight(pt.Point2d);
                if (!double.IsNaN(height))
                {
                    CadXDataWriter.UpdatePointHeight(pt.Handle, height, tr, Database);
                }
            }
            tr.Commit();
        }

        // ── Terrain model ─────────────────────────────────────────────────

        public void WriteTriangulation(TerrainModel terrainModel, bool showTriangles)
        {
            using var tr = Database.TransactionManager.StartTransaction();
            var bt = ((_AcDb.BlockTable)tr.GetObject(Database.BlockTableId, _AcDb.OpenMode.ForRead));
            var ms = (_AcDb.BlockTableRecord)tr.GetObject(bt[_AcDb.BlockTableRecord.ModelSpace], _AcDb.OpenMode.ForWrite);

            // Draw boundary lines
            foreach (var line in terrainModel.Lines)
            {
                if (line.Type == HC_SPOJNICE.HRANICE || (showTriangles && line.Type == HC_SPOJNICE.TRIANG))
                {
                    var cadLine = new _AcDb.Line(
                        line.Pt1.Point2d.ToAcGePoint3d(line.Pt1.Point3d.Z),
                        line.Pt2.Point2d.ToAcGePoint3d(line.Pt2.Point3d.Z));
                    cadLine.Layer = line.Type == HC_SPOJNICE.HRANICE ? "HC-DTM-BOUNDARY" : "HC-DTM-TRIANG";
                    ms.AppendEntity(cadLine);
                    tr.AddNewlyCreatedDBObject(cadLine, true);
                }
            }

            tr.Commit();
        }

        public void SaveTerrainModel(TerrainModel terrainModel)
        {
            using var tr = Database.TransactionManager.StartTransaction();
            CadXDataWriter.SaveTerrainModel(terrainModel, tr, Database);
            tr.Commit();
        }

        public TerrainModel LoadTerrainModel(string appName)
        {
            using var tr = Database.TransactionManager.StartTransaction();
            var model = CadXDataReader.LoadTerrainModel(appName, tr, Database);
            tr.Commit();
            return model;
        }

        public TerrainModel ReadTerrainModel(string appName)
        {
            return LoadTerrainModel(appName);
        }

        public void DeleteTerrainModel()
        {
            using var tr = Database.TransactionManager.StartTransaction();
            CadXDataWriter.DeleteTerrainModel(tr, Database);
            tr.Commit();
            WriteMessageNoDebug(Properties.Resources.TerrainModel_Deleted);
        }

        // ── Profile drawing ───────────────────────────────────────────────

        public void DrawLongitudinalProfile(ProfileData profileData, ProfileSettings settings)
        {
            var ptOpts = new _AcEd.PromptPointOptions(Properties.Resources.Profile_SelectInsertionPoint);
            _AcEd.PromptPointResult ptRes = Editor.GetPoint(ptOpts);
            if (ptRes.Status != _AcEd.PromptStatus.OK) return;

            Point2d insertionPt = ptRes.Value.ToPoint2d();

            using var tr = Database.TransactionManager.StartTransaction();
            ProfileDrawer.Draw(Database, tr, profileData, settings, insertionPt);
            tr.Commit();
        }

        // ── Private helpers ───────────────────────────────────────────────

        private bool PromptYesNo(string message, string defaultKeyword, out bool result)
        {
            result = false;
            var opts = new _AcEd.PromptKeywordOptions($"\n{message}");
            opts.Keywords.Add(Properties.Resources.Yes);
            opts.Keywords.Add(Properties.Resources.No);
            opts.Keywords.Default = defaultKeyword;
            opts.AllowNone = true;

            _AcEd.PromptResult res = Editor.GetKeywords(opts);
            if (res.Status == _AcEd.PromptStatus.Cancel) return false;
            result = res.StringResult == Properties.Resources.Yes;
            return true;
        }

        private void WritePointsToDrawing(IList<HCPoint> points)
        {
            using var tr = Database.TransactionManager.StartTransaction();
            var bt = (_AcDb.BlockTable)tr.GetObject(Database.BlockTableId, _AcDb.OpenMode.ForRead);
            var ms = (_AcDb.BlockTableRecord)tr.GetObject(bt[_AcDb.BlockTableRecord.ModelSpace], _AcDb.OpenMode.ForWrite);

            foreach (var pt in points)
            {
                var dbPt = new _AcDb.DBPoint(pt.Point3d.ToAcGePoint3d());
                dbPt.Layer = "HC-SURVEY-POINTS";
                ms.AppendEntity(dbPt);
                tr.AddNewlyCreatedDBObject(dbPt, true);
                CadXDataWriter.WritePointXData(pt, dbPt, tr, Database);
            }

            tr.Commit();
        }
    }
}
