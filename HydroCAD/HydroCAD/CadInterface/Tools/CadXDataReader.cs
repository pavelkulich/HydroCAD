#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcEd = Autodesk.AutoCAD.EditorInput;
    using _AcGe = Autodesk.AutoCAD.Geometry;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
    using _AcEd = Bricscad.EditorInput;
    using _AcGe = Teigha.Geometry;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcEd = Gssoft.Gscad.EditorInput;
    using _AcGe = Gssoft.Gscad.Geometry;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcEd = ZwSoft.ZwCAD.EditorInput;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
#endif

using System;
using System.Collections.Generic;
using HydroCAD.Common;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.Network;
using HydroCAD.Models.TerrainModel;
using HydroCAD.Services.Triangulation;

namespace HydroCAD.CadInterface.Tools
{
    internal static class CadXDataReader
    {
        private const string APP_POINT   = "HC_POINT";
        private const string APP_DTM     = "HC_DTM";

        internal static IList<HCPoint> CreatePointsFromSelection(
            _AcEd.SelectionSet acSSet, _AcDb.Transaction tr, bool requireXData)
        {
            var points = new List<HCPoint>(acSSet.Count);
            foreach (_AcEd.SelectedObject selObj in acSSet)
            {
                if (selObj == null) continue;
                var dbPt = tr.GetObject(selObj.ObjectId, _AcDb.OpenMode.ForRead) as _AcDb.DBPoint;
                if (dbPt == null) continue;

                HCPoint hcPt = requireXData ? ReadPoint(dbPt) : CreatePointFromDbPt(dbPt);
                if (hcPt != null) points.Add(hcPt);
            }
            return points;
        }

        internal static IList<HCLine> CreateSegmentsFromSelection(
            _AcEd.SelectionSet acSSet, _AcDb.Transaction tr, bool requireXData,
            IDictionary<string, HCPoint> handleMap)
        {
            var segments = new List<HCLine>(acSSet.Count);
            foreach (_AcEd.SelectedObject selObj in acSSet)
            {
                if (selObj == null) continue;
                var entity = tr.GetObject(selObj.ObjectId, _AcDb.OpenMode.ForRead) as _AcDb.Entity;
                var seg = ReadSegment(entity, handleMap);
                if (seg != null) segments.Add(seg);
            }
            return segments;
        }

        internal static HCPolyline ReadPolyline(_AcDb.Polyline entity)
        {
            if (entity == null) return null;
            var vertices = new List<PolylineVertex>(entity.NumberOfVertices);
            for (int i = 0; i < entity.NumberOfVertices; i++)
            {
                _AcGe.Point2d pt = entity.GetPoint2dAt(i);
                double bulge = entity.GetBulgeAt(i);
                vertices.Add(new PolylineVertex(new Point2d(pt.X, pt.Y), bulge));
            }
            return new HCPolyline(vertices, entity.Handle.ToString());
        }

        internal static TerrainModel LoadTerrainModel(string appName, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // Read terrain model data stored in a named object dictionary (XRecord)
            try
            {
                var nod = ((_AcDb.DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, _AcDb.OpenMode.ForRead));
                string key = string.IsNullOrEmpty(appName) ? FindLatestDtmKey(nod) : appName;
                if (key == null || !nod.Contains(key)) return null;

                var xRecord = (_AcDb.Xrecord)tr.GetObject(nod.GetAt(key), _AcDb.OpenMode.ForRead);
                return DeserializeTerrainModel(xRecord, tr, db);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ── Private helpers ────────────────────────────────────────────────

        private static HCPoint CreatePointFromDbPt(_AcDb.DBPoint dbPt)
        {
            _AcGe.Point3d pos = dbPt.Position;
            return new HCPoint(new Point3d(pos.X, pos.Y, pos.Z),
                               (int)dbPt.Id.OldIdPtr, dbPt.Handle.ToString());
        }

        private static HCPoint ReadPoint(_AcDb.DBPoint dbPt)
        {
            if (dbPt?.XData == null) return CreatePointFromDbPt(dbPt);

            _AcDb.TypedValue[] xdata = dbPt.XData.AsArray();
            int num = 0;
            HC_BOD type = HC_BOD.BASIC;
            var neighbors = new HashSet<string>();

            for (int i = 0; i < xdata.Length; i++)
            {
                if (xdata[i].TypeCode == (short)_AcDb.DxfCode.ExtendedDataAsciiString)
                {
                    string tag = xdata[i].Value?.ToString() ?? "";
                    if (tag == "NUM" && i + 1 < xdata.Length)
                        num = (int)Convert.ToDouble(xdata[i + 1].Value);
                    else if (tag == "NEIGHBOR" && i + 1 < xdata.Length)
                        neighbors.Add(xdata[i + 1].Value?.ToString() ?? "");
                }
            }

            _AcGe.Point3d pos = dbPt.Position;
            var pt = new HCPoint(new Point3d(pos.X, pos.Y, pos.Z), num, dbPt.Handle.ToString(), type);
            pt.SetNeighborsHandles(neighbors);
            return pt;
        }

        private static HCLine ReadSegment(_AcDb.Entity entity, IDictionary<string, HCPoint> handleMap)
        {
            if (entity == null || handleMap == null) return null;
            // Simplified: create a line between endpoints
            if (entity is _AcDb.Line line)
            {
                string h1 = FindNearestPointHandle(line.StartPoint.ToPoint2d(), handleMap);
                string h2 = FindNearestPointHandle(line.EndPoint.ToPoint2d(), handleMap);
                if (h1 != null && h2 != null && handleMap.TryGetValue(h1, out var pt1) && handleMap.TryGetValue(h2, out var pt2))
                    return new HCLine(pt1, pt2, HC_SPOJNICE.BREAKLINE, entity.Handle.ToString());
            }
            return null;
        }

        private static string FindNearestPointHandle(Point2d position, IDictionary<string, HCPoint> handleMap)
        {
            double bestDist = 0.01; // tolerance
            string bestHandle = null;
            foreach (var kv in handleMap)
            {
                double d = kv.Value.Point2d.DistanceTo(position);
                if (d < bestDist) { bestDist = d; bestHandle = kv.Key; }
            }
            return bestHandle;
        }

        private static string FindLatestDtmKey(_AcDb.DBDictionary nod)
        {
            string best = null;
            foreach (var entry in nod)
            {
                if (entry.Key.StartsWith("HC_DTM_"))
                    best = entry.Key;
            }
            return best;
        }

        private static TerrainModel DeserializeTerrainModel(
            _AcDb.Xrecord xRecord, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // Simplified deserialization – rebuild from survey points in drawing
            // (Full implementation would store and reload point neighbour graph)
            return null;
        }
    }
}
