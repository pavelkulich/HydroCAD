#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using System;
using System.Collections.Generic;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.TerrainModel;

namespace HydroCAD.CadInterface.Tools
{
    internal static class CadXDataWriter
    {
        private const string APP_POINT = "HC_POINT";
        private const string APP_DTM   = "HC_DTM";

        internal static void WritePointXData(HCPoint pt, _AcDb.DBPoint dbPt,
                                              _AcDb.Transaction tr, _AcDb.Database db)
        {
            EnsureAppRegistered(APP_POINT, db, tr);

            var rb = new _AcDb.ResultBuffer(
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, APP_POINT),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "NUM"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, (double)pt.Number),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "TAG"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, pt.Tag ?? "")
            );
            dbPt.XData = rb;
        }

        internal static void UpdatePointHeight(string handle, double height,
                                               _AcDb.Transaction tr, _AcDb.Database db)
        {
            if (!long.TryParse(handle, System.Globalization.NumberStyles.HexNumber,
                               System.Globalization.CultureInfo.CurrentCulture, out long val)) return;

            if (!db.TryGetObjectId(new _AcDb.Handle(val), out _AcDb.ObjectId id) || !id.IsValid) return;
            var dbPt = tr.GetObject(id, _AcDb.OpenMode.ForWrite) as _AcDb.DBPoint;
            if (dbPt == null) return;

            // Update Z coordinate
            _AcDb.ObjectId posId = dbPt.ObjectId;
            var pos = dbPt.Position;
            dbPt.Position = new Autodesk.AutoCAD.Geometry.Point3d(pos.X, pos.Y, height);
        }

        internal static void SaveTerrainModel(TerrainModel terrainModel, _AcDb.Transaction tr, _AcDb.Database db)
        {
            EnsureAppRegistered(APP_DTM, db, tr);

            var nod = (_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForWrite);

            // Serialize terrain model into XRecord: store point handles + neighbour handles
            var rb = new _AcDb.ResultBuffer();
            rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, terrainModel.Name));

            foreach (var pt in terrainModel.Points)
            {
                rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "PT"));
                rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, pt.Handle));
                foreach (string nh in pt.NeighborsHandles)
                    rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, nh));
                rb.Add(new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "END_PT"));
            }

            var xRecord = new _AcDb.Xrecord { Data = rb };
            if (nod.Contains(terrainModel.Name))
            {
                var existing = (_AcDb.Xrecord)tr.GetObject(nod.GetAt(terrainModel.Name), _AcDb.OpenMode.ForWrite);
                existing.Data = rb;
            }
            else
            {
                nod.SetAt(terrainModel.Name, xRecord);
                tr.AddNewlyCreatedDBObject(xRecord, true);
            }
        }

        internal static void DeleteTerrainModel(_AcDb.Transaction tr, _AcDb.Database db)
        {
            var nod = (_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForWrite);
            var toDelete = new List<string>();
            foreach (var entry in nod)
                if (entry.Key.StartsWith("HC_DTM_")) toDelete.Add(entry.Key);

            foreach (string key in toDelete)
            {
                var obj = tr.GetObject(nod.GetAt(key), _AcDb.OpenMode.ForWrite);
                obj.Erase();
            }
        }

        private static void EnsureAppRegistered(string appName, _AcDb.Database db, _AcDb.Transaction tr)
        {
            var rat = (_AcDb.RegAppTable)tr.GetObject(db.RegAppTableId, _AcDb.OpenMode.ForWrite);
            if (!rat.Has(appName))
            {
                var entry = new _AcDb.RegAppTableRecord { Name = appName };
                rat.Add(entry);
                tr.AddNewlyCreatedDBObject(entry, true);
            }
        }
    }
}
