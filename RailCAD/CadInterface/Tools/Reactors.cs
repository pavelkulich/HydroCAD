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

using RailCAD.Common;
using RailCAD.Models.Geometry;

namespace RailCAD.CadInterface.Tools
{
    /// <summary>
    /// Manages reactors - methods that are triggered by a specific event such as entity modification.
    /// List of source entity - dependent entities is stored in XRecords for recovery after the document is closed.
    /// </summary>
    internal static class Reactors
    {
        /// <summary>
        /// Name of the curve tangent reactor record that is save in the drawing XRecords
        /// </summary>
        private const string REACTOR_RECORD_NAME = "CurveTangentReactor";

        /// <summary>
        /// Tracked entity relationships: source entity (tangent line) -> dependent entities (curve)
        /// When source is modified, dependent entity is marked for processing
        /// </summary>
        private static Dictionary<_AcDb.ObjectId, HashSet<_AcDb.ObjectId>> _trackedRelations
            = new Dictionary<_AcDb.ObjectId, HashSet<_AcDb.ObjectId>>();

        /// <summary>
        /// Source point coordinates for determination if its position changed
        /// </summary>
        private static Dictionary<_AcDb.ObjectId, List<Point3d>> _sourcePoints
            = new Dictionary<_AcDb.ObjectId, List<Point3d>> ();

        /// <summary>
        /// Name of the last active document so the initialize method does not execute more than once
        /// </summary>
        private static string _lastDocumentName = null;

        /// <summary>
        /// Name of the last successfully loaded document that matches tracked entity relations dictionary
        /// </summary>
        private static string _loadedDocumentName = null;

        /// <summary>
        /// Initializes the reactor system. Should be called when the application loads.
        /// If the document is not null (no drawing is openned) or the same as last document (switch to different one).
        /// </summary>
        internal static void Initialize(_AcAp.Document doc)
        {
            if (doc == null || doc.Name == _lastDocumentName) return;

            AttachCommandEndedReactor(doc);
            AttachLispEndedReactor(doc);
            LoadEntityReactors(REACTOR_RECORD_NAME, doc);
            _lastDocumentName = doc.Name;
        }

        /// <summary>
        /// Creates a relationship between source and dependent entity.
        /// When sourceEntity is modified, dependentEntity will be marked for processing in OnCommandEnded.
        /// </summary>
        /// <param name="sourceId">Entity to watch for modifications</param>
        /// <param name="dependentId">Entity to process when source is modified</param>
        internal static void AttachEntityReactor(_AcDb.ObjectId sourceId, _AcDb.ObjectId dependentId, _AcAp.Document doc)
        {
            if (doc == null) return;

            using (_AcDb.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // Verify entities exist
                    _AcDb.DBObject sourceObj = tr.GetObject(sourceId, _AcDb.OpenMode.ForRead);
                    _AcDb.DBObject dependentObj = tr.GetObject(dependentId, _AcDb.OpenMode.ForRead);

                    // Store or update relationship
                    _sourcePoints[sourceId] = sourceObj.GetPoints();
                    _trackedRelations.TryGetValue(sourceId, out HashSet<_AcDb.ObjectId> dependentIds);
                    if (dependentIds == null) dependentIds = new HashSet<_AcDb.ObjectId>();
                    dependentIds.Add(dependentId);  // add to the dependent entities list
                    _trackedRelations[sourceId] = dependentIds;
                }
                catch (Exception ex)
                {
                    CadModel.WriteMessageStatic($"AttachEntityReactor error: {ex.Message}");
                }
            }

            SaveEntityReactors(REACTOR_RECORD_NAME, doc);
        }

        /// <summary>
        /// Removes the relationship between source entity and its dependent entity.
        /// </summary>
        internal static void DetachEntityReactor(_AcDb.ObjectId sourceId, _AcAp.Document doc)
        {
            if (_trackedRelations.ContainsKey(sourceId))
            {
                _trackedRelations.Remove(sourceId);
                _sourcePoints.Remove(sourceId);
                SaveEntityReactors(REACTOR_RECORD_NAME, doc);
            }
        }

        /// <summary>
        /// Detaches all entity reactors.
        /// </summary>
        internal static void DetachAllReactors(_AcAp.Document doc)
        {
            _trackedRelations.Clear();
            _sourcePoints.Clear();
            SaveEntityReactors(REACTOR_RECORD_NAME, doc);
        }

        /// <summary>
        /// Attaches the command ended reactor to process modified entities.
        /// </summary>
        private static void AttachCommandEndedReactor(_AcAp.Document doc)
        {
            if (doc == null) return;

            try { doc.CommandEnded -= OnCommandEnded; } catch { }

            doc.CommandEnded += OnCommandEnded;
        }

        /// <summary>
        /// Attaches the command ended reactor to process modified entities.
        /// </summary>
        private static void AttachLispEndedReactor(_AcAp.Document doc)
        {
            if (doc == null) return;

            try { doc.LispEnded -= OnLispEnded; } catch { }

            doc.LispEnded += OnLispEnded;
        }

        /// <summary>
        /// Commands that can modify entity position and should trigger reactor processing
        /// </summary>
        private static readonly HashSet<string> _modifyingCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MOVE", "ROTATE", "SCALE", "STRETCH", "MIRROR", "ALIGN", "TRIM", "EXTEND", "BREAK", "BREAKATPOINT", "FILLET", "CHAMFER",
            "GRIP_MOVE", "GRIP_STRETCH", "GRIP_ROTATE", "GRIP_SCALE", "GRIP_MIRROR", "CHANGE", "LENGTHEN", "OFFSET",
        };

        /// <summary>
        /// Event handler called after any LISP functions processing completes.
        /// </summary>
        private static void OnLispEnded(object sender, EventArgs e)
        {
            _AcAp.Document doc = sender as _AcAp.Document;
            if (e == null || doc == null) return;

            ProcessReactors(doc);
        }

        /// <summary>
        /// Event handler called after any command from the list completes.
        /// </summary>
        private static void OnCommandEnded(object sender, _AcAp.CommandEventArgs e)
        {
            _AcAp.Document doc = sender as _AcAp.Document;
            if (e == null || doc == null) return;

            string commandName = e.GlobalCommandName?.ToUpper() ?? "";

            // Check if this command could modify entities
            bool shouldProcess = string.IsNullOrEmpty(commandName) || // Process if unknown
                                 _modifyingCommands.Contains(commandName);

            if (shouldProcess)
            {
                ProcessReactors(doc);
            }
        }

        /// <summary>
        /// Processes all marked dependent entities (e.g., changes their color) and clears the list.
        /// Only for curve tangent reactors which uses LISP function to update the curves
        /// It is possible to add another reactors which need their on XRecord and list of entities for modification.
        /// </summary>
        private static void ProcessReactors(_AcAp.Document doc)
        {
            if (_trackedRelations.Count == 0) return;

            if (doc == null || doc.Name != _loadedDocumentName) return;

            try
            {
                HashSet<_AcDb.ObjectId> modifiedEntities = new HashSet<_AcDb.ObjectId>(_trackedRelations.Count);

                // check if the source position changed and if so save dependent entity to list

                using (_AcDb.Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (_AcDb.ObjectId sourceId in _trackedRelations.Keys)
                    {
                        _AcDb.DBObject sourceObj = tr.GetObject(sourceId, _AcDb.OpenMode.ForRead);
                        List<Point3d> points = sourceObj.GetPoints();
                        if (!_sourcePoints[sourceId].IsEqualTo(points, 1e-4))  // check if source position changed
                        {
                            HashSet<_AcDb.ObjectId> dependentIds = _trackedRelations[sourceId];
                            foreach (_AcDb.ObjectId dependentId in dependentIds)
                            {
                                modifiedEntities.Add(dependentId);  // add all dependent entites (curves)
                                _sourcePoints[sourceId] = points;  // update source (tangent) coordinates
                            }
                        }
                    }
                }

                if (modifiedEntities.Count > 0)
                {
                    var args = new _AcDb.ResultBuffer(
                        new _AcDb.TypedValue((int)_AcRt.LispDataType.Text, "Sm:oprav_tecny"),  // function name
                        new _AcDb.TypedValue((int)_AcRt.LispDataType.ListBegin)
                    );
                    foreach ( _AcDb.ObjectId objId in modifiedEntities.ToList())
                    {
                        args.Add(new _AcDb.TypedValue((int)_AcRt.LispDataType.ObjectId, objId));  // add curves for refreshing
                    }
                    args.Add(new _AcDb.TypedValue((int)_AcRt.LispDataType.ListEnd));

                    _AcDb.ResultBuffer result = CadModel.InvokeLisp(args);  // calls LISP function
                }
            }
            catch (Exception ex)
            {
                CadModel.WriteMessageStatic($"\nOnCommandEnded: processing failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves tracked entity relationships to XRecords as handle pairs.
        /// Format: lists of { sourceHandle, dependentHandle, dependentHandle, ... } as string handles with control strings.
        /// </summary>
        private static void SaveEntityReactors(string recordName, _AcAp.Document doc)
        {
            if (doc == null) return;

            try
            {
                var validRelations = _trackedRelations
                    .Where(kvp => kvp.Key.IsValid && !kvp.Key.IsErased)
                    .Select(kvp => (kvp.Key, Value: new HashSet<_AcDb.ObjectId>(kvp.Value.Where(id => id.IsValid && !id.IsErased))))
                    .Where(x => x.Value.Count > 0)
                    .ToDictionary(x => x.Key, x => x.Value);

                // Build ResultBuffer with handle pairs
                var rb = new List<_AcDb.TypedValue>();
                rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.Int32, validRelations.Count)); // Count

                int count = 0;
                foreach (var relation in validRelations)
                {
                    string sourceHandleStr = relation.Key.Handle.Value.ToString("X");
                    rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ControlString, "{"));
                    rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.Handle, sourceHandleStr));
                    foreach (var dependentId in relation.Value)
                    {
                        string dependentHandleStr = dependentId.Handle.Value.ToString("X");
                        rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.Handle, dependentHandleStr));
                        count++;
                    }
                    rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ControlString, "}"));
                    count += 3;
                }

                var resultBuffer = new _AcDb.ResultBuffer(rb.ToArray());

                CadXRecordsWriter.SaveEntityReactors(resultBuffer, recordName, doc);
                _loadedDocumentName = doc.Name;
            }
            catch (Exception ex)
            {
                CadModel.WriteMessageStatic($"SaveEntityReactors error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads tracked entity relationships from XRecords and restores them.
        /// </summary>
        private static void LoadEntityReactors(string recordName, _AcAp.Document doc)
        {
            if (doc == null) return;

            using (_AcDb.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    _AcDb.TypedValue[] values = CadXRecordsReader.LoadEntityReactors(tr, recordName, doc);
                    if (values.Length < 1)
                    {
                        if (_trackedRelations.Count > 0)
                        {
                            _trackedRelations.Clear();  // clear old relations from different drawing
                            _sourcePoints.Clear();
                        }
                        return;
                    }

                    int count;
                    try
                    {
                        count = Convert.ToInt32(values[0].Value);
                    }
                    catch
                    {
                        CadModel.WriteMessageStatic($"LoadEntityReactors: invalid count value in XRecord.");
                        return;
                    }

                    // Expect 1 + count*2 typed values
                    if (values.Length <= 1 + count * 2)
                    {
                        CadModel.WriteMessageStatic($"LoadEntityReactors: XRecord length mismatch. Skipping.");
                        return;
                    }
                    _trackedRelations = new Dictionary<_AcDb.ObjectId, HashSet<_AcDb.ObjectId>>(count);
                    _sourcePoints = new Dictionary<_AcDb.ObjectId, List<Point3d>>(count);

                    // Read handle pairs and restore relationships
                    int loadedCount = 0;
                    for (int i = 2; i < values.Length; i++)
                    {
                        try
                        {
                            string sourceHandleStr = values[i++].Value.ToString();
                            List<string> dependentHandleStrs = new List<string>();
                            while (values[i].TypeCode == (int)_AcDb.DxfCode.Handle)
                            {
                                dependentHandleStrs.Add(values[i++].Value.ToString());
                            }
                            i++;
                            _AcDb.Handle sourceHandle = new _AcDb.Handle(Convert.ToInt64(sourceHandleStr, 16));
                            _AcDb.ObjectId sourceId = doc.Database.GetObjectId(false, sourceHandle, 0);
                            HashSet<_AcDb.ObjectId> dependentIds = new HashSet<_AcDb.ObjectId>(dependentHandleStrs.Count);

                            if (sourceId.IsValid && !sourceId.IsErased)
                            {
                                foreach (string dependentHandleStr in dependentHandleStrs)
                                {
                                    _AcDb.Handle dependentHandle = new _AcDb.Handle(Convert.ToInt64(dependentHandleStr, 16));
                                    _AcDb.ObjectId dependentId = doc.Database.GetObjectId(false, dependentHandle, 0);
                                    if (dependentId.IsValid && !dependentId.IsErased)
                                    {
                                        dependentIds.Add(dependentId);
                                    }
                                }
                            }

                            if (dependentIds.Count > 0)  // only load if any dependent entity exists
                            {
                                _AcDb.DBObject sourceObj = tr.GetObject(sourceId, _AcDb.OpenMode.ForRead);
                                _sourcePoints[sourceId] = sourceObj.GetPoints();
                                _trackedRelations[sourceId] = dependentIds;
                                loadedCount += dependentIds.Count;
                            }
                        }
                        catch (Exception ex)
                        {
                            CadModel.WriteMessageStatic($"LoadEntityReactors: failed to restore relation #{i}: {ex.Message}");
                        }
                    }

                    if (loadedCount > 0)
                    {
                        CadModel.WriteMessageStatic($"Loaded {loadedCount} entity reactor relationship(s).");
                        _loadedDocumentName = doc.Name;
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    CadModel.WriteMessageStatic($"LoadEntityReactors error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Cleans up reactors and saves state.
        /// </summary>
        internal static void Cleanup(_AcAp.Document doc)
        {
            if (doc != null)
            {
                SaveEntityReactors(REACTOR_RECORD_NAME, doc);

                try
                {
                    doc.CommandEnded -= OnCommandEnded;
                    doc.LispEnded -= OnLispEnded;
                }
                catch { }
            }

            _trackedRelations.Clear();
            _sourcePoints.Clear();
        }
    }
}