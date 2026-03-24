#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;

namespace RailCAD.CadInterface.Tools
{
    internal class CadXRecordsWriter
    {
        private const int MAX_CHUNK_SIZE = 127;

        /// <summary>
        /// Saves a TerrainModel (triangles with points and edge types) into an XRecord inside RailCAD -> TerrainModels -> [model.Name].
        /// </summary>
        internal static void SaveTerrainModel(TerrainModel model, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // Prepare binary data (body, triangles, lines)
            byte[] data = SerializeTerrainModelBinary(model);

            // Save into RCAD -> TerrainModels -> [model.Name]
            SaveBinaryXRecord("TerrainModels", model.Name, data, tr, db);

            tr.Commit();
        }

        /// <summary>
        /// Converts terrain model into binary structure for saving into XRecords.
        /// </summary>
        private static byte[] SerializeTerrainModelBinary(TerrainModel model)
        {
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    // --- POINTS ---
                    int pointCount = model.Points.Count();
                    bw.Write(pointCount);

                    // create handle → index map
                    var indexMap = new Dictionary<string, int>(pointCount);
                    int i = 0;
                    foreach (var pt in model.Points)
                    {
                        indexMap[pt.Handle] = i;

                        bw.Write(pt.Handle ?? "0");
                        bw.Write(pt.Number);
                        Point3d point = pt.Point3d;
                        bw.Write(point.X);
                        bw.Write(point.Y);
                        bw.Write(point.Z);
                        i++;
                    }

                    // --- TRIANGLES ---
                    int triCount = model.Triangles.Count();
                    bw.Write(triCount);

                    foreach (var tri in model.Triangles)
                    {
                        bw.Write(indexMap[tri.Points[0].Handle]);
                        bw.Write(indexMap[tri.Points[1].Handle]);
                        bw.Write(indexMap[tri.Points[2].Handle]);
                    }

                    // --- LINES ---
                    var validLines = model.Lines?
                        .Where(l => l.Type.HasFlag(RC_SPOJNICE.FIXED_SEGMENT_INSIDE))
                        .ToList() ?? new List<RCLine>();

                    bw.Write(validLines.Count);

                    foreach (var line in validLines)
                    {
                        bw.Write(indexMap[line.Pt1.Handle]);
                        bw.Write(indexMap[line.Pt2.Handle]);
                        bw.Write((short)line.Type);
                    }

                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Saves raw binary data into an XRecord under RCAD -> [dictName] -> [recordName].
        /// If the record already exists, it will be replaced.
        /// </summary>
        internal static void SaveBinaryXRecord(string dictName, string recordName, byte[] data, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // 1. Access Named Objects Dictionary (NOD)
            _AcDb.DBDictionary nod = (_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForWrite);

            // 2. Ensure RCAD dictionary
            _AcDb.DBDictionary rcadDict = GetOrCreateDictionary(nod, "RCAD", tr);

            // 3. Ensure target dictionary
            _AcDb.DBDictionary targetDict = GetOrCreateDictionary(rcadDict, dictName, tr);

            // 4. Split binary data into BinaryChunks
            int chunkCount = (int)Math.Ceiling((double)data.Length / MAX_CHUNK_SIZE);

            var rb = new List<_AcDb.TypedValue>(chunkCount);
            rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.Int32, data.Length));   // Total size
            rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.Int32, chunkCount));   // Chunk count

            for (int i = 0; i < chunkCount; i++)
            {
                int offset = i * MAX_CHUNK_SIZE;
                int size = Math.Min(MAX_CHUNK_SIZE, data.Length - offset);

                byte[] chunk = new byte[size];
                Array.Copy(data, offset, chunk, 0, size);

                rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.BinaryChunk, chunk));
            }

            var newData = new _AcDb.ResultBuffer(rb.ToArray());
            _AcDb.Xrecord xrec = new _AcDb.Xrecord { Data = newData };

            // 5. Replace old record if exists
            if (targetDict.Contains(recordName))
            {
                _AcDb.ObjectId oldId = targetDict.GetAt(recordName);
                _AcDb.DBObject oldObj = tr.GetObject(oldId, _AcDb.OpenMode.ForWrite);
                oldObj.Erase();
            }

            targetDict.SetAt(recordName, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        /// <summary>
        /// Saves tracked entity relationships to XRecords as lists of handles.
        /// </summary>
        internal static void SaveEntityReactors(_AcDb.ResultBuffer resultBuffer, string recordName, _AcAp.Document doc)
        {
            if (doc == null || resultBuffer == null) return;

            using (_AcDb.Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    // Get or create dictionary structure
                    _AcDb.DBDictionary nod = (_AcDb.DBDictionary)tr.GetObject(
                        doc.Database.NamedObjectsDictionaryId, _AcDb.OpenMode.ForWrite);

                    _AcDb.DBDictionary rcadDict = GetOrCreateDictionary(nod, "RCAD", tr);
                    _AcDb.DBDictionary reactorDict = GetOrCreateDictionary(rcadDict, "EntityReactors", tr);

                    // Replace existing record
                    if (reactorDict.Contains(recordName))
                    {
                        _AcDb.ObjectId oldId = reactorDict.GetAt(recordName);
                        _AcDb.DBObject oldObj = tr.GetObject(oldId, _AcDb.OpenMode.ForWrite);
                        oldObj.Erase();
                    }

                    _AcDb.Xrecord xrec = new _AcDb.Xrecord { Data = resultBuffer };
                    reactorDict.SetAt(recordName, xrec);
                    tr.AddNewlyCreatedDBObject(xrec, true);

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    doc.Editor.WriteMessage($"\nSaveEntityReactors error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Deletes an XRecord under RCAD -> [dictName] -> [recordName].
        /// </summary>
        internal static void DeleteXRecord(string dictName, string recordName, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // 1. Access Named Objects Dictionary (NOD)
            _AcDb.DBDictionary nod = (_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForRead);
            if (!nod.Contains("RCAD"))
                return;

            _AcDb.DBDictionary rcadDict = (_AcDb.DBDictionary)tr.GetObject(nod.GetAt("RCAD"), _AcDb.OpenMode.ForRead);
            if (!rcadDict.Contains(dictName))
                return;

            _AcDb.DBDictionary targetDict = (_AcDb.DBDictionary)tr.GetObject(rcadDict.GetAt(dictName), _AcDb.OpenMode.ForWrite);
            if (!targetDict.Contains(recordName))
                return;

            // 2. Erase the XRecord
            _AcDb.ObjectId recId = targetDict.GetAt(recordName);
            _AcDb.DBObject recObj = tr.GetObject(recId, _AcDb.OpenMode.ForWrite);
            recObj.Erase();
        }

        /// <summary>
        /// Returns or creates a DBDictionary with the given name inside the parent dictionary.
        /// </summary>
        internal static _AcDb.DBDictionary GetOrCreateDictionary(_AcDb.DBDictionary parentDict, string name, _AcDb.Transaction tr)
        {
            if (parentDict.Contains(name))
            {
                _AcDb.ObjectId dictObjectId = parentDict.GetAt(name);
                return (_AcDb.DBDictionary)tr.GetObject(dictObjectId, _AcDb.OpenMode.ForWrite);
            }
            else
            {
                parentDict.UpgradeOpen();
                _AcDb.DBDictionary newDict = new _AcDb.DBDictionary();
                parentDict.SetAt(name, newDict);
                tr.AddNewlyCreatedDBObject(newDict, true);
                return newDict;
            }
        }
    }
}
