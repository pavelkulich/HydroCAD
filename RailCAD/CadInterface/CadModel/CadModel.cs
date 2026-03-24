#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcEd = Autodesk.AutoCAD.EditorInput;
    using _AcGe = Autodesk.AutoCAD.Geometry;
    using _AcRn = Autodesk.AutoCAD.Runtime;
    using _AcRt = Autodesk.AutoCAD.Runtime;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
    using _AcEd = Bricscad.EditorInput;
    using _AcGe = Teigha.Geometry;
    using _AcRn = Teigha.Runtime;
    using _AcRt = Bricscad.Runtime;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcEd = Gssoft.Gscad.EditorInput;
    using _AcGe = Gssoft.Gscad.Geometry;
    using _AcRn = Gssoft.Gscad.Runtime;
    using _AcRt = Gssoft.Gscad.Runtime;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcEd = ZwSoft.ZwCAD.EditorInput;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
    using _AcRn = ZwSoft.ZwCAD.Runtime;
    using _AcRt = ZwSoft.ZwCAD.Runtime;
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using RailCAD.CadInterface.Tools;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.Views;

namespace RailCAD.CadInterface
{
    internal class CadModel : ICadModel
    {
        private _AcDb.ResultBuffer inputResbuf;
        private _AcDb.ResultBuffer outputResbuf;

        public CadModel(_AcDb.ResultBuffer inputResbuf=null)
        {
            this.inputResbuf = inputResbuf;
            this.outputResbuf = null;
        }

        internal _AcAp.Document Document
        {
            get { return _AcAp.Application.DocumentManager.MdiActiveDocument; }
        }

        internal _AcDb.Database Database
        {
            get { return Document.Database; }
        }

        internal _AcEd.Editor Editor
        {
            get { return Document.Editor; }
        }

        public static void WriteMessageStatic(object message)
        {
            // defaultly: only for debugging/developement
#if DEBUG
            _AcAp.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage(string.Format("{0}\n", message));
#endif
        }

        public static _AcDb.Entity GetEntityByHandle(string handle, _AcDb.Transaction tr, _AcDb.Database db, bool write = false)
        {
            // https://forums.autodesk.com/t5/net/get-entity-by-using-entity-handle/m-p/11668933#M75380
            if (long.TryParse(handle, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.CurrentCulture, out long value))
            {
                if (db.TryGetObjectId(new _AcDb.Handle(value), out _AcDb.ObjectId id) && id.IsValid && !id.IsErased)
                {
                    if (id.IsValid && !id.IsErased)
                    {
                        _AcDb.OpenMode mode = write ? _AcDb.OpenMode.ForWrite : _AcDb.OpenMode.ForRead;

                        return (_AcDb.Entity)tr.GetObject(id, mode);
                    }
                }
            }

            return null;
        }

        public void WriteMessage(object message)
        {
            // only for debugging/developement
#if DEBUG
            Editor.WriteMessage(string.Format("DEBUG: {0}\n", message));
#endif
        }

        public void WriteMessageNoDebug(object message)
        {
            // will be shown to users in the production app!
            Editor.WriteMessage(string.Format("{0}\n", message));
        }

        public bool PromptUserSettings(out UserSettings settings)
        {
            settings = new UserSettings();
            if (PromptYesNo(Properties.Resources.TerrainModel_PromptConsiderTriangleAreaForNormals, Properties.Resources.No, out settings.considerTriangleAreaForNormals))
            {
                if (PromptYesNo(Properties.Resources.TerrainModel_PromptDrawTriangles, Properties.Resources.No, out settings.showTriangles))
                {
                    return true;
                }
            }
            return false;
        }

        public string PromptActivateLicence()
        {
            // https://help.autodesk.com/view/OARX/2025/ENU/?guid=GUID-41E19C3B-B40A-41EC-88CB-347B1161B74A
            _AcEd.PromptStringOptions pStrOpts = new _AcEd.PromptStringOptions("\n" + Properties.Resources.LicencePasteKey);
            pStrOpts.AllowSpaces = true;
            return Editor.GetString(pStrOpts).StringResult;
        }

        public object ReadAndValidateLispArgs(Func<_AcDb.ResultBuffer, object> readArgsFunction)
        {
            if (this.inputResbuf != null)
            {
                try
                {
                    object args = readArgsFunction(this.inputResbuf);

                    if (args != null)
                    {
                        return args;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public _AcDb.ResultBuffer GetLispInputArgs()
        {
            return this.inputResbuf;
        }

        public _AcDb.ResultBuffer GetLispResp()
        {
            return this.outputResbuf;
        }

        public _AcDb.TypedValue GetLispRespSingleValue()
        {
            if (this.outputResbuf != null)
            {
                var arr = this.outputResbuf.AsArray();
                if (arr != null && arr.Length > 0)
                {
                    return arr[0];
                }
            }
            return new _AcDb.TypedValue((int)_AcRt.LispDataType.Nil);
        }

        public void SetLispResp<T>(Func<T, _AcDb.ResultBuffer> writeRespFunction, T resp)
        {
            this.outputResbuf = writeRespFunction(resp);
        }

        /// <summary>
        /// Generate tram profile for a circular arc
        /// </summary>
        public void DrawTramProfile()
        {
            _AcDb.ObjectId objId = _AcDb.ObjectId.Null;
            bool update = false;

            try
            {
                if (this.inputResbuf != null && this.inputResbuf.AsArray().Length > 0)
                {
                    // update existing profile
                    _AcDb.TypedValue tv = this.inputResbuf.AsArray()[0];
                    if (tv.TypeCode == (short)_AcRt.LispDataType.ObjectId)
                    {
                        objId = (_AcDb.ObjectId)tv.Value;
                        update = true;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    // Select arc entity
                    var selOpts = new _AcEd.PromptEntityOptions("\n" + Properties.Resources.TramProfile_PromptSelectArc);
                    selOpts.SetRejectMessage("\n" + Properties.Resources.TramProfile_WarningSelectedEntityIsNotArc);
                    selOpts.AddAllowedClass(typeof(_AcDb.Arc), true);

                    var selRes = Editor.GetEntity(selOpts);
                    if (selRes.Status == _AcEd.PromptStatus.OK)
                    {
                        objId = selRes.ObjectId;
                    }
                    else
                    {
                        return;
                    }
                }

                using (var tr = StartTransaction())
                {
                    var arc = tr.GetObject(objId, _AcDb.OpenMode.ForWrite) as _AcDb.Arc;

                    // Generate profiles
                    string message = TramProfile.GenerateTramProfile(arc, tr, this.Database, update);
                    if (message != null)
                    {
                        WriteMessageNoDebug("\n" + message);
                    }
                }
            }
            catch (Exception ex)
            {
                WriteMessage("\n" + Properties.Resources.Error + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Command for selection of two polylines for finding their closest distance which is showed by line or dimension.
        /// </summary>
        public void ClosestPolylineDistance()
        {
            try
            {
                // Ask user for first curve
                var opt1 = new _AcEd.PromptEntityOptions("\n" + String.Format(Properties.Resources.TramProfile_PromptSelectPolyline, 1));
                opt1.SetRejectMessage("\n" + Properties.Resources.TramProfile_WarningSelectedEntityIsNotPolyline);
                opt1.AddAllowedClass(typeof(_AcDb.Polyline), false);
                var res1 = Editor.GetEntity(opt1);
                if (res1.Status != _AcEd.PromptStatus.OK) return;

                // Ask user for second curve
                var opt2 = new _AcEd.PromptEntityOptions("\n" + String.Format(Properties.Resources.TramProfile_PromptSelectPolyline, 2));
                opt2.SetRejectMessage("\n" + Properties.Resources.TramProfile_WarningSelectedEntityIsNotPolyline);
                opt2.AddAllowedClass(typeof(_AcDb.Polyline), false);
                var res2 = Editor.GetEntity(opt2);
                if (res2.Status != _AcEd.PromptStatus.OK) return;

                // Ask user whether to draw line or dimension
                var optMode = new _AcEd.PromptKeywordOptions("\n" + Properties.Resources.TramProfile_PromptHowToShowDistance);
                optMode.Keywords.Add(Properties.Resources.EntityType_Line);
                optMode.Keywords.Add(Properties.Resources.EntityType_Dimension);
                optMode.Keywords.Default = Properties.Resources.EntityType_Line;
                //optMode.AllowNone = true;
                var resMode = Editor.GetKeywords(optMode);
                if (resMode.Status != _AcEd.PromptStatus.OK) return;
                bool asDimension = (resMode.StringResult == Properties.Resources.EntityType_Dimension);

                using (var tr = StartTransaction())
                {
                    var curve1 = (_AcDb.Polyline)tr.GetObject(res1.ObjectId, _AcDb.OpenMode.ForRead);
                    var curve2 = (_AcDb.Polyline)tr.GetObject(res2.ObjectId, _AcDb.OpenMode.ForRead);
                    RCPolyline poly1 = LispCadExtensions.GetPolylineGeometry(curve1);
                    RCPolyline poly2 = LispCadExtensions.GetPolylineGeometry(curve2);

                    // Find closest points
                    List<Point2d> points = poly1.GetClosestDistanceBetweenPolylines(poly2);
                    if (points == null || points.Count == 0) return;
                    _AcGe.Point3d p1 = new _AcGe.Point3d(points[0].X, points[0].Y, 0);
                    _AcGe.Point3d p2 = new _AcGe.Point3d(points[1].X, points[1].Y, 0);

                    // Open model space for writing
                    var btr = (_AcDb.BlockTableRecord)tr.GetObject(Database.CurrentSpaceId, _AcDb.OpenMode.ForWrite);

                    if (asDimension)
                    {
                        // Create aligned dimension
                        var dim = new _AcDb.AlignedDimension(
                            p1,       // first point
                            p2,       // second point
                            new _AcGe.Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, (p1.Z + p2.Z) / 2), // dimension line position
                            "",       // no text override
                            Database.Dimstyle
                        );
                        btr.AppendEntity(dim);
                        tr.AddNewlyCreatedDBObject(dim, true);
                    }
                    else
                    {
                        // Create line between points
                        var line = new _AcDb.Line(p1, p2);
                        btr.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                WriteMessage("\nChyba: " + ex.Message);
            }
        }

        /// <summary>
        /// Imports points from a text file.
        /// </summary>
        public void ImportPoints()
        {
            // Show import dialog
            IList<RCPoint> points = ImportPointsDialog.ImportPointsFromFile(this);

            // timer start
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int result = CreatePoints(points, true);
            sw.Stop();
            WriteMessage($"Elapsed={sw.Elapsed}");

            // Process result
            if (result < 0)
            {
                WriteMessageNoDebug(Properties.Resources.PointTools_ImportDialogPointsImportWasCancelled);
            }
            else if (result > 0)
            {
                WriteMessageNoDebug(string.Format(Properties.Resources.PointTools_InfoPointsImportHasBeenCompleted, result));
            }
        }

        /// <summary>
        /// Creates points in the drawing from a list of RCPoint objects
        /// </summary>
        /// <param name="points">A list of RCPoint objects</param>
        /// <param name="zoom">If true zoom to see all the created points at the end</param>
        /// <returns>Number of created points</returns>
        internal int CreatePoints(IList<RCPoint> points, bool zoom = false)
        {
            if (points == null || points.Count == 0)
                return 0;

            int createdCount = 0;
            var ext = new _AcDb.Extents3d();

            using (Document.LockDocument())
            {
                List<_AcDb.DBPoint> newPointIds = new List<_AcDb.DBPoint>(points.Count);

                using (var tr = StartTransaction())
                {
                    try
                    {
                        var btr = (_AcDb.BlockTableRecord)tr.GetObject(Database.CurrentSpaceId, _AcDb.OpenMode.ForWrite);

                        XDataTools.AddRegAppTableRecord(XDataAppNames.RC_BOD, tr, Database);

                        foreach (var rcPoint in points)
                        {
                            ext.AddPoint(rcPoint.Point3d.ToAcGePoint3d());
                            var handle = CadXDataWriter.WriteNewPoint(btr, tr, rcPoint);

                            //rcPoint.SetHandle(handle);
                            createdCount++;
                        }

                        tr.Commit();
                    }
                    catch (Exception ex)
                    {
                        WriteMessageNoDebug(Properties.Resources.PointTools_ErrorWhileCreatingPoint);
                        WriteMessage(ex.Message);
                    }
                }
            }

            if (zoom)
            {
                Editor.Regen();
                ZoomToExtents(Editor, ext);
            }

            return createdCount;
        }

        internal void ZoomToExtents(_AcEd.Editor ed, _AcDb.Extents3d ext)
        {
            // enlarge the box so the points are not just at the edge
            double margin = 0.1;
            var min = ext.MinPoint;
            var max = ext.MaxPoint;

            double dx = (max.X - min.X) * margin;
            double dy = (max.Y - min.Y) * margin;

            var center = new _AcGe.Point2d(
                (min.X + max.X) / 2.0,
                (min.Y + max.Y) / 2.0);

            using (var view = ed.GetCurrentView())
            {
                view.CenterPoint = center;
                view.Width = (max.X - min.X) + dx;
                view.Height = (max.Y - min.Y) + dy;
                ed.SetCurrentView(view);
            }
        }

        /// <summary>
        /// Prompts a selection of points using a dialog.
        /// </summary>
        /// <returns>Returns a list of points as objects.</returns>
        public IList<RCPoint> SelectPoints(string message = "")
        {
            if (message == "")
            {
                message = Properties.Resources.PointTools_PromptSelectPoints;
            }
            return PointSelectionDialog.SelectPointsDialog(this, message);
        }

        /// <summary>
        /// Prompts a manual selection of points by a user.
        /// </summary>
        /// <param name="requireXData">Trigger for selection with or without XData.</param>
        /// <returns>Returns a list of points as RCPoint objects.</returns>
        public IList<RCPoint> SelectPointsManually(string message, bool requireXData = true)
        {
            using (var tr = StartTransaction())
            {
                // Request for objects to be selected in the drawing area: https://help.autodesk.com/view/OARX/2022/ENU/?guid=GUID-CBECEDCF-3B4E-4DF3-99A0-47103D10DADD
                var options = new _AcEd.PromptSelectionOptions();
                options.MessageForAdding = message;
                _AcDb.TypedValue[] filterCriteria = new _AcDb.TypedValue[]
                {
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "POINT"),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_BOD)
                };
                _AcEd.SelectionFilter filter = new _AcEd.SelectionFilter(filterCriteria);
                _AcEd.PromptSelectionResult acSSPrompt = Editor.GetSelection(options, filter);

                if (acSSPrompt.Status == _AcEd.PromptStatus.OK)  // if OK, objects were selected
                {
                    return CadXDataReader.CreatePointsFromSelection(acSSPrompt.Value, tr, requireXData);
                }
            }
            return null;
        }


        /// <summary>
        /// Selects all points in a drawing.
        /// </summary>
        /// <returns>Returns a list of points as RCPoint objects.</returns>
        public IList<RCPoint> SelectAllPoints()
        {
            IList<RCPoint> points = new List<RCPoint>();
            IList<_AcDb.Entity> ents;
            using (_AcDb.Transaction tr = StartTransaction())
            {
                ents = SelectEntitiesByXData("POINT", XDataAppNames.RC_BOD, tr, false);
                if (ents != null)
                {
                    try
                    {
                        foreach (var ent in ents)
                        {
                            RCPoint pt = CadXDataReader.ReadPoint(ent as _AcDb.DBPoint);
                            if (pt != null)
                            {
                                points.Add(pt);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteMessageNoDebug(Properties.Resources.PointTools_ErrorWhileReadingPoint);
                        WriteMessage(ex.Message);
                    }
                    return points;
                }
            }
            return null;
        }

        /// <summary>
        /// Prompts user to select fixed segments (connecting lines between points).
        /// </summary>
        /// <param name="handleMap">Dictionary of handles and RCPoint objects.</param>
        /// <param name="requireXData">Trigger for selection with or without XData.</param>
        /// <returns>Returns a list of segments as RCLine objects.</returns>
        public IList<RCLine> SelectSegments(IDictionary<string, RCPoint> handleMap, bool requireXData = true)
        {
            using (var tr = StartTransaction())
            {
                var options = new _AcEd.PromptSelectionOptions();
                options.MessageForAdding = Properties.Resources.TerrainModel_PromptSelectTerrainSegments;
                _AcDb.TypedValue[] filterCriteria = new _AcDb.TypedValue[]
                {
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "LINE"),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_SPOJNICE)
                };
                _AcEd.SelectionFilter filter = new _AcEd.SelectionFilter(filterCriteria);
                _AcEd.PromptSelectionResult acSSPrompt = Editor.GetSelection(options, filter);

                if (acSSPrompt.Status == _AcEd.PromptStatus.OK)
                {
                    return CadXDataReader.CreateSegmentsFromSelection(acSSPrompt.Value, tr, requireXData, handleMap);
                }
            }
            return null;
        }

        internal IList<_AcDb.Entity> SelectEntitiesByXData(string entType, string appName, _AcDb.Transaction tr, bool write)
        {
            IList<_AcDb.Entity> ents = new List<_AcDb.Entity>();
            var mode = write ? _AcDb.OpenMode.ForWrite : _AcDb.OpenMode.ForRead;

            _AcDb.TypedValue[] filterCriteria = new _AcDb.TypedValue[]
            {
                new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, entType),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, appName)
            };
            _AcEd.SelectionFilter filter = new _AcEd.SelectionFilter(filterCriteria);
            _AcEd.PromptSelectionResult result = Editor.SelectAll(filter);
            if (result.Value == null) return null;

            foreach (_AcEd.SelectedObject obj in result.Value)
            {
                _AcDb.Entity ent = tr.GetObject(obj.ObjectId, mode) as _AcDb.Entity;
                if (ent != null)
                {
                    ents.Add(ent);
                }
            }
            ents.Reverse();

            return ents;
        }

        internal IList<_AcDb.Entity> SelectEntitiesByXData(string appName, _AcDb.Transaction tr, out IList<_AcDb.Entity> lines, bool write)
        {
            IList<_AcDb.Entity> ents = new List<_AcDb.Entity>();
            lines = new List<_AcDb.Entity>();
            var mode = write ? _AcDb.OpenMode.ForWrite : _AcDb.OpenMode.ForRead;

            _AcDb.TypedValue[] filterCriteria = new _AcDb.TypedValue[]
            {
                new _AcDb.TypedValue(-4, "<OR"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "POINT"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.Start, "LINE"),
                new _AcDb.TypedValue(-4, "OR>"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, appName)
            };
            _AcEd.SelectionFilter filter = new _AcEd.SelectionFilter(filterCriteria);
            _AcEd.PromptSelectionResult result = Editor.SelectAll(filter);
            if (result.Value == null) return null;

            foreach (_AcEd.SelectedObject obj in result.Value)
            {
                _AcDb.Entity ent = tr.GetObject(obj.ObjectId, mode) as _AcDb.Entity;
                if (ent != null)
                {
                    if (ent is _AcDb.DBPoint dbPt)
                    {
                        ents.Add(ent);
                    }
                    else if (ent is _AcDb.Line line)
                    {
                        lines.Add(ent);
                    }
                }
            }
            ents.Reverse();
            lines.Reverse();

            return ents;
        }

        internal TerrainModel CreateTerrainModelFromXData(string appName, _AcDb.Transaction tr)
        {
            // search for all entites that match the type and application name
            IList<_AcDb.Entity> ents = SelectEntitiesByXData(appName, tr, out IList<_AcDb.Entity> lines, false);

            // create RC points and map references
            IList<RCPoint> rcPoints = new List<RCPoint>();
            IDictionary<string, RCPoint> ptHandleMap = new Dictionary<string, RCPoint>(rcPoints.Count);
            IList<RCLine> rcLines = new List<RCLine>();

            foreach (_AcDb.DBPoint dbPt in ents)
            {
                RCPoint pt = CadXDataReader.ReadPoint(dbPt, appName);
                if (pt != null)
                {
                    rcPoints.Add(pt);
                    ptHandleMap.Add(pt.Handle, pt);
                }
            }
            //IDictionary<string, RCPoint> ptHandleMap = RCPoint.HandleMap(rcPoints);
            foreach (var ent in lines)
            {
                RCLine rcLine = CadXDataReader.ReadLine(ent, ptHandleMap, appName, true); // only fixed segments (even HRANICE?)
                if (rcLine != null)
                {
                    rcLines.Add(rcLine);
                }
            }

            return TerrainModel.CreateTerrainModelFromDeserializedData(rcPoints, ptHandleMap, rcLines.Count > 0 ? rcLines : null, appName);
        }

        public void DeletePointsPercentage(IList<RCPoint> points)
        {
            if (points == null) return;

            // Prompt user for the interval
            _AcEd.PromptIntegerOptions intOpts = new _AcEd.PromptIntegerOptions("\n" + Properties.Resources.PointTools_PromptEnterWhatPercentageOfPointsYouWannaDelete);
            intOpts.AllowNegative = false;
            intOpts.AllowZero = false;
            intOpts.DefaultValue = 100;
            intOpts.UseDefaultValue = true;

            _AcEd.PromptIntegerResult intResult = Editor.GetInteger(intOpts);
            if (intResult.Status != _AcEd.PromptStatus.OK)
            {
                WriteMessage(Properties.Resources.InfoCommandCancelled);
                return;
            }
            double percentage = intResult.Value;

            // timer start
            Stopwatch sw = new Stopwatch();
            sw.Start();

            RCPoint[] pointArray = points.ToArray();
            int count = pointArray.Length;

            // Pre-calculate which indices to delete
            double step = 100.0 / percentage;
            var indicesToDelete = new List<int>(percentage == 100 ? count : (int)(count / step) + 1);
            int lastAdded = -1;

            for (double k = 0; k < count; k += step)
            {
                int idx = (int)Math.Round(k);
                if (idx < count && idx != lastAdded)
                {
                    indicesToDelete.Add(idx);
                    lastAdded = idx;
                }
            }

            int deletedCount = 0;
            using (var tr = StartTransaction())
            {
                foreach (int idx in indicesToDelete)
                {
                    try
                    {
                        _AcDb.Entity dbPnt = GetEntityByHandle(pointArray[idx].Handle, tr, Database, true);
                        if (dbPnt != null)
                        {
                            dbPnt.Erase();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteMessageNoDebug(Properties.Resources.PointTools_ErrorWhileDeletingPoint);
                        WriteMessage(ex.Message);
                    }
                }

                tr.Commit();
            }

            if (deletedCount > 0)
            {
                WriteMessageNoDebug(string.Format(Properties.Resources.PointTools_InfoDeletedPointsCount, deletedCount));
            }
            sw.Stop();
            WriteMessage($"Elapsed={sw.Elapsed}");
        }

        public void WriteTriangulation(TerrainModel terrainModel, bool showTriangles)
        {
            using (var tr = StartTransaction())
            {
                _AcDb.BlockTableRecord btr = (_AcDb.BlockTableRecord)tr.GetObject(Database.CurrentSpaceId, _AcDb.OpenMode.ForWrite);

                CadXDataWriter.WriteTriangulation(this.Database, btr, tr, terrainModel, showTriangles);

                tr.Commit();
            }
        }

        public void SaveTerrainModel(TerrainModel terrainModel)
        {
            using (var tr = StartTransaction())
            {
                // timer start
                Stopwatch sw = new Stopwatch();
                sw.Start();
                CadXRecordsWriter.SaveTerrainModel(terrainModel, tr, this.Database);
                sw.Stop();
                WriteMessage($"Elapsed saving terrain model to XRecords={sw.Elapsed}");
            }
        }

        public TerrainModel LoadTerrainModel(string appName)
        {
            using (var tr = StartTransaction())
            {
                // timer start
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var terrainModel = CadXRecordsReader.LoadTerrainModel(appName, tr, this.Database);
                sw.Stop();
                WriteMessage($"Elapsed loading terrain model from XRecords={sw.Elapsed}");
                return terrainModel;
            }
        }

        public TerrainModel ReadTerrainModel(string appName)
        {
            using (var tr = StartTransaction())
            {
                // timer start
                Stopwatch sw = new Stopwatch();
                sw.Start();
                var terrainModel = CreateTerrainModelFromXData(appName, tr);
                sw.Stop();
                WriteMessage($"Elapsed reading terrain model from XData={sw.Elapsed}");
                WriteMessage($"Triangles: {terrainModel.Triangles.Count()}");
                return terrainModel;
            }
        }

        public void DeleteTerrainModel()
        {
            var appName = SelectTerrainModel();
            if (String.IsNullOrEmpty(appName)) return;

            // timer start
            Stopwatch sw = new Stopwatch();
            sw.Start();

            using (var tr = StartTransaction())
            {
                EraseTerrainModelFromEnts(appName, tr);
                CadXRecordsWriter.DeleteXRecord("TerrainModels", appName, tr, this.Database);
                tr.Commit();
            }
            sw.Stop();
            WriteMessage($"\nElapsed erasing the terrain model={sw.Elapsed}");
        }
        /// <summary>
        /// Erase terrain model triangulation lines and XData from other connection lines.
        /// </summary>
        /// <param name="appName">Xdata application name of terrain model</param>
        internal void EraseTerrainModelFromEnts(string appName, _AcDb.Transaction tr)
        {
            // search for all entites that match the type and application name
            IList<_AcDb.Entity> toClean = SelectEntitiesByXData(appName, tr, out IList<_AcDb.Entity> lines, true);
            IList<_AcDb.Entity> toErase = new List<_AcDb.Entity>();

            foreach (var ent in lines)
            {
                if (CadXDataReader.TestLineType(ent, RC_SPOJNICE.TYPES_TO_DELETE))
                {
                    toErase.Add(ent);
                }
                else
                {
                    toClean.Add(ent);
                }
            }
            foreach (_AcDb.Entity ent in toClean)
            {
                CadXDataWriter.DeleteXData(ent, appName);
            }
            foreach (var ent in toErase)
            {
                ent.Erase(true);
            }
        }

        public string SelectTerrainModel()
        {
            IList<string> apps = null;
            using (var tr = StartTransaction())
            {
                while (apps == null)
                {
                    var objId = SelectEntity("\n" + Properties.Resources.TerrainModel_PromptSelectEntityOfTerrainModel);
                    if (objId == _AcDb.ObjectId.Null) return null;

                    var ent = tr.GetObject(objId, _AcDb.OpenMode.ForRead) as _AcDb.Entity;
                    if (ent != null)
                    {
                        apps = CadXDataReader.ReadAppNames(ent, XDataAppNames.RC_D);
                        if (apps == null)
                        {
                            _AcAp.Application.ShowAlertDialog(Properties.Resources.TerrainModel_WarningSelectedEntityIsNotPartOfAnyModel);
                            apps = null;
                        }
                        else if (apps.Count == 1)
                        {
                            return apps[0];
                        }
                        else
                        {
                            _AcAp.Application.ShowAlertDialog(Properties.Resources.TerrainModel_WarningSelectedEntityBelongsToMoreModels);
                            apps = null;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Initialization of all reactors (subscribe methods to events)
        /// </summary>
        public void InitializeReactors()
        {
            Reactors.Initialize(this.Document);

            try { _AcAp.Application.DocumentManager.DocumentActivated -= OnDocumentActivated; } catch { }
            _AcAp.Application.DocumentManager.DocumentActivated += OnDocumentActivated;
        }

        /// <summary>
        /// Executes when user switches to different drawing
        /// </summary>
        private void OnDocumentActivated(object sender, _AcAp.DocumentCollectionEventArgs e)
        {
            Reactors.Initialize(this.Document);
        }

        /// <summary>
        /// Defines reactor for refreshing of curve after tangent modification
        /// </summary>
        /// <param name="resbuf"></param>
        public void DefineCurveTangentReactor()
        {
            if (this.inputResbuf != null)
            {
                _AcDb.TypedValue[] rvArr = this.inputResbuf.AsArray();
                if (rvArr.Length >= 2)
                {
                    // read tangent and curve objectId
                    if (rvArr[1].TypeCode == (short)_AcRt.LispDataType.ObjectId && rvArr[1].TypeCode == (short)_AcRt.LispDataType.ObjectId)
                    {
                        _AcDb.ObjectId sourceEntityId = (_AcDb.ObjectId)rvArr[0].Value;
                        _AcDb.ObjectId dependentEntityId = (_AcDb.ObjectId)rvArr[1].Value;

                        Reactors.AttachEntityReactor(sourceEntityId, dependentEntityId, this.Document);
                    }
                }
            }
        }

        /// <summary>
        /// Invokes a LISP function. It has to be registered using (vl-acad-defun 'lisp-function).
        /// </summary>
        /// <param name="args">Function name and arguments.</param>
        /// <returns>ResultBuffer containing the LISP function result.</returns>
        internal static _AcDb.ResultBuffer InvokeLisp(_AcDb.ResultBuffer args)
        {
            if (args == null) return null;

            try
            {
#if BCAD
                return Bricscad.Global.Editor.Invoke(args);
#else
                return _AcAp.Application.Invoke(args);
#endif
            }
            catch {}
#if ZCAD
            // plan B for ZWCAD 2021-2025
            try
            {
                IntPtr ip = IntPtr.Zero;
                int status = zcedInvoke(args.UnmanagedObject, out ip);
                if (status == (int)_AcEd.PromptStatus.OK && ip != IntPtr.Zero)
                {
                    return _AcDb.ResultBuffer.Create(ip, true);
                }
            }
            catch { }
#endif
            return null;
        }

#if ZCAD
        [DllImport("zwcad.exe", EntryPoint = "zcedInvoke",
        CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        extern static private int zcedInvoke(IntPtr args, out IntPtr result);
#endif

        public _AcDb.ObjectId SelectEntity(string message)
        {
            using (var tr = StartTransaction())
            {
                var options = new _AcEd.PromptEntityOptions(message);
                _AcEd.PromptEntityResult acPrompt = Editor.GetEntity(options);

                if (acPrompt.Status == _AcEd.PromptStatus.OK)  // if OK entity was selected
                {
                    return acPrompt.ObjectId;
                }
            }
            return _AcDb.ObjectId.Null;
        }

        public Point3d TransformFromUCSToGCS(Point3d point)
        {
            // todo: ensure the same entity (or id)?
            // use _AcGe.Point3d as a attribute of RCPoint that carries the geometric information?
            return point
                .ToAcGePoint3d()
                .TransformBy(Editor.CurrentUserCoordinateSystem)
                .ToPoint3d();
        }

        public Point3d TransformFromGCSToUCS(Point3d point)
        {
            return point
                .ToAcGePoint3d()
                .TransformBy(Editor.CurrentUserCoordinateSystem.Inverse())
                .ToPoint3d();
        }

        private bool PromptYesNo(string message, string defaultOption, out bool promptResult)
        {
            // https://help.autodesk.com/view/OARX/2025/ENU/?guid=GUID-41E19C3B-B40A-41EC-88CB-347B1161B74A
            _AcEd.PromptKeywordOptions pKeyOpts = new _AcEd.PromptKeywordOptions("");
            pKeyOpts.Message = message;
            pKeyOpts.Keywords.Add(Properties.Resources.No);
            pKeyOpts.Keywords.Add(Properties.Resources.Yes);
            pKeyOpts.Keywords.Default = defaultOption;
            pKeyOpts.AllowNone = false;
            pKeyOpts.AllowArbitraryInput = false;

            _AcEd.PromptResult pKeyRes = Editor.GetKeywords(pKeyOpts);

            if (pKeyRes.Status == _AcEd.PromptStatus.OK)
            {
                promptResult = pKeyRes.StringResult == Properties.Resources.No ? false : true;
                return true;
            }
            else
            {
                promptResult = false;
                return false;
            }
        }

        private _AcDb.Transaction StartTransaction()
        {
            // OpenCloseTransaction?: https://help.autodesk.com/view/OARX/2024/CSY/?guid=GUID-BF06F786-DDA6-4603-B5E5-25A35A4130A3
            return Database.TransactionManager.StartTransaction();
        }
    }
}
