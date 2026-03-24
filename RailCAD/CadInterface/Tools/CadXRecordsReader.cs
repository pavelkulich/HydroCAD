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
using System.IO;

using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;

namespace RailCAD.CadInterface.Tools
{
    internal class CadXRecordsReader
    {
        /// <summary>
        /// Loads a TerrainModel from XRecord RailCAD -> TerrainModels -> [modelName].
        /// Returns null if the model does not exist.
        /// </summary>
        internal static TerrainModel LoadTerrainModel(string modelName, _AcDb.Transaction tr, _AcDb.Database db)
        {
            if (modelName == null) return null;

            // Load raw binary data from RCAD -> TerrainModels -> [modelName]
            byte[] data = LoadBinaryXRecord("TerrainModels", modelName, tr, db);

            if (data == null)
                return null;

            TerrainModel model = DeserializeTerrainModelBinary(data, modelName);

            tr.Commit();
            return model;
        }

        /// <summary>
        /// Load model from the binary structure {POINTS, TRIANGLES, LINES}
        /// </summary>
        private static TerrainModel DeserializeTerrainModelBinary(byte[] data, string modelName)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var br = new BinaryReader(ms))
                {
                    // --- POINTS ---
                    int pointCount = br.ReadInt32();
                    var points = new List<RCPoint>(pointCount);

                    for (int i = 0; i < pointCount; i++)
                    {
                        string handle = br.ReadString();
                        int number = br.ReadInt32();
                        double x = br.ReadDouble();
                        double y = br.ReadDouble();
                        double z = br.ReadDouble();

                        points.Add(new RCPoint(new Point3d(x, y, z), number, handle));
                    }

                    // --- TRIANGLES (1st pass — read indices only, triangles are built after lines) ---
                    int triCount = br.ReadInt32();
                    var triIndices = new (int i1, int i2, int i3)[triCount];

                    for (int t = 0; t < triCount; t++)
                    {
                        int i1 = br.ReadInt32();
                        int i2 = br.ReadInt32();
                        int i3 = br.ReadInt32();

                        //if ((uint)i1 >= (uint)pointCount || (uint)i2 >= (uint)pointCount || (uint)i3 >= (uint)pointCount)
                        //    throw new InvalidDataException($"Triangle {t} references invalid point index (i1={i1}, i2={i2}, i3={i3}, pointCount={pointCount}).");

                        triIndices[t] = (i1, i2, i3);
                    }

                    // --- LINES ---
                    int lineCount = br.ReadInt32();
                    var lines = new List<RCLine>(lineCount);

                    // Lookup dictionary: normalized point-index pair -> RCLine, for O(1) triangle edge matching.
                    // Key encodes the two point indices as a single long (smaller index in high 32 bits).
                    var lineByPoints = new Dictionary<long, RCLine>(lineCount);

                    for (int l = 0; l < lineCount; l++)
                    {
                        int i1 = br.ReadInt32();
                        int i2 = br.ReadInt32();
                        short typeVal = br.ReadInt16();
                        var type = (RC_SPOJNICE)typeVal;

                        //if ((uint)i1 >= (uint)pointCount || (uint)i2 >= (uint)pointCount)
                        //    throw new InvalidDataException($"Line {l} references invalid point index (i1={i1}, i2={i2}, pointCount={pointCount}).");

                        var line = new RCLine(points[i1], points[i2], type);
                        lines.Add(line);

                        lineByPoints[RCLine.MakeLineKey(i1, i2)] = line;
                    }

                    // --- TRIANGLES (2nd pass — build with correct line references) ---
                    var triangles = new List<RCTriangle>(triCount);

                    for (int t = 0; t < triCount; t++)
                    {
                        var (i1, i2, i3) = triIndices[t];
                        var pts = new[] { points[i1], points[i2], points[i3] };

                        var lns = new RCLine[3];
                        lns[0] = GetLineFromLookup(lineByPoints, i1, i2, pts[0], pts[1]);
                        lns[1] = GetLineFromLookup(lineByPoints, i2, i3, pts[1], pts[2]);
                        lns[2] = GetLineFromLookup(lineByPoints, i3, i1, pts[2], pts[0]);

                        triangles.Add(new RCTriangle(t, pts, lns));
                    }

                    return new TerrainModel(points, lines, triangles, false, modelName);
                }
            }
        }

        private static RCLine GetLineFromLookup(Dictionary<long, RCLine> lookup, int i1, int i2, RCPoint p1, RCPoint p2)
        {
            return lookup.TryGetValue(RCLine.MakeLineKey(i1, i2), out RCLine line)
                ? line
                : RCLine.CreateLineAutoType(p1, p2);
        }

        /// <summary>
        /// Loads raw binary data from an XRecord under RCAD -> [dictName] -> [recordName].
        /// Returns null if not found or invalid.
        /// </summary>
        internal static byte[] LoadBinaryXRecord(string dictName, string recordName, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // 1. Access Named Objects Dictionary (NOD)
            _AcDb.DBDictionary nod = (_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForRead);
            if (!nod.Contains("RCAD"))
                return null;

            _AcDb.DBDictionary rcadDict = (_AcDb.DBDictionary)tr.GetObject(nod.GetAt("RCAD"), _AcDb.OpenMode.ForRead);
            if (!rcadDict.Contains(dictName))
                return null;

            _AcDb.DBDictionary targetDict = (_AcDb.DBDictionary)tr.GetObject(rcadDict.GetAt(dictName), _AcDb.OpenMode.ForRead);
            if (!targetDict.Contains(recordName))
                return null;

            _AcDb.Xrecord xrec = (_AcDb.Xrecord)tr.GetObject(targetDict.GetAt(recordName), _AcDb.OpenMode.ForRead);
            if (xrec.Data == null)
                return null;

            _AcDb.TypedValue[] values = xrec.Data.AsArray();
            if (values.Length < 3)
                return null;

            if (values[0].TypeCode != (int)_AcDb.DxfCode.Int32 || values[1].TypeCode != (int)_AcDb.DxfCode.Int32)
                return null;

            int totalSize = (int)values[0].Value;
            int chunkCount = (int)values[1].Value;

            if (values.Length != chunkCount + 2)
                return null;

            byte[] binaryData = new byte[totalSize];
            int offset = 0;

            for (int i = 0; i < chunkCount; i++)
            {
                if (values[i + 2].TypeCode != (int)_AcDb.DxfCode.BinaryChunk)
                    return null;

                byte[] chunk = (byte[])values[i + 2].Value;
                Array.Copy(chunk, 0, binaryData, offset, chunk.Length);
                offset += chunk.Length;
            }

            return binaryData;
        }

        /// <summary>
        /// Loads tracked entity relationships from XRecords and reattaches reactors.
        /// </summary>
        internal static _AcDb.TypedValue[] LoadEntityReactors(_AcDb.Transaction tr, string recordName, _AcAp.Document doc)
        {
            const string dictionaryName = "EntityReactors";
            _AcDb.TypedValue[] values = new _AcDb.TypedValue[0];

            try
            {
                _AcDb.DBDictionary nod = (_AcDb.DBDictionary)tr.GetObject(
                    doc.Database.NamedObjectsDictionaryId, _AcDb.OpenMode.ForRead);

                if (!nod.Contains("RCAD"))
                {
                    return values;
                }

                _AcDb.DBDictionary rcadDict = (_AcDb.DBDictionary)tr.GetObject(
                    nod.GetAt("RCAD"), _AcDb.OpenMode.ForRead);

                if (!rcadDict.Contains(dictionaryName))
                {
                    return values;
                }

                _AcDb.DBDictionary reactorDict = (_AcDb.DBDictionary)tr.GetObject(
                    rcadDict.GetAt(dictionaryName), _AcDb.OpenMode.ForRead);

                if (!reactorDict.Contains(recordName))
                {
                    return values;
                }

                _AcDb.Xrecord xrec = (_AcDb.Xrecord)tr.GetObject(
                    reactorDict.GetAt(recordName), _AcDb.OpenMode.ForRead);

                if (xrec.Data == null)
                {
                    return values;
                }

                return xrec.Data.AsArray();
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\nLoadEntityReactors error: {ex.Message}");
            }
            return values;
        }
    }
}
