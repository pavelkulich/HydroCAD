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
using System.Linq;

using RailCAD.Common;
using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.MainApp;

namespace RailCAD.CadInterface.Tools
{
    public partial class CadXDataWriter
    {
        public static void WriteTriangulation(_AcDb.Database db, _AcDb.BlockTableRecord btr, _AcDb.Transaction tr, TerrainModel terrainModel, bool showTriangles)
        {
            // register new application names
            XDataTools.AddRegAppTableRecord(terrainModel.Name, tr, db);
            XDataTools.AddRegAppTableRecord(XDataAppNames.RC_SPOJNICE, tr, db);

            // write points xdata
            foreach (RCPoint rcPoint in terrainModel.Points)
            {
                if (rcPoint.IsValid)
                {
                    var dbPt = CadModel.GetEntityByHandle(rcPoint.Handle, tr, db, true);
                    //dbPt.Highlight();  // debug

                    if (dbPt != null)
                    {
                        dbPt.XData = CreateTerrainXDataForPoint(rcPoint, terrainModel.Name); ;  // written to a new application name
                    }
                }
            }

            // write triangluation lines
            foreach (RCLine rcLine in terrainModel.Lines)
            {
                if (showTriangles || rcLine.Type == RC_SPOJNICE.HRANICE)
                {
                    if (rcLine.Handle.IsNullHandle())
                    {
                        WriteNewTerrainLine(btr, tr, rcLine, terrainModel.Name);
                    }
                    else  // user selected segment - write xdata only
                    {
                        var dbLine = CadModel.GetEntityByHandle(rcLine.Handle, tr, db, true);

                        if (dbLine != null)
                        {
                            dbLine.XData = CreateTerrainXDataForLine(terrainModel.Name);  // written to a new application name
                        }
                    }
                }
            }
        }

        internal static _AcDb.ResultBuffer CreateTerrainXDataForPoint(RCPoint rcPoint, string terrainModelAppName)
        {
            // no new point is created, only xdata are written
            var newXData = new _AcDb.ResultBuffer
            {
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, terrainModelAppName),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, rcPoint.Type.ToString()),  // point type
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataXCoordinate, new _AcGe.Point3d(rcPoint.Normal.X, rcPoint.Normal.Y, rcPoint.Normal.Z)),  // normal vector 
            };

            // neighboring points and normals
            foreach (IRCEntity neighborEntity in GetSortedNeighborEntitiesForXDataWrite(rcPoint).Values)
            {
                newXData.Add((_AcDb.TypedValue)neighborEntity.WriteToXData());  // entity data to Xdata
            }

            return newXData;
        }

        internal static _AcDb.ResultBuffer CreateTerrainXDataForLine(string terrainModelAppName)
        {
            if (String.IsNullOrEmpty(terrainModelAppName)) return null;

            // no new line is created, only xdata are written
            var newXData = new _AcDb.ResultBuffer
            {
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, terrainModelAppName),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "SPOJNICE"),
            };

            return newXData;
        }

        private static void WriteNewTerrainLine(_AcDb.BlockTableRecord btr, _AcDb.Transaction tr, RCLine rcLine, string terrainModelAppName)
        {
            var line = new _AcDb.Line(rcLine.Pt1.Point3d.ToAcGePoint3d(), rcLine.Pt2.Point3d.ToAcGePoint3d());

            // terain model - xdata
            line.XData = new _AcDb.ResultBuffer
            {
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, terrainModelAppName),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "SPOJNICE"),

                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_SPOJNICE),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, rcLine.Type.ToString()),

                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, rcLine.Pt1.Handle),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, rcLine.Pt2.Handle),
            };
            btr.AppendEntity(line);

            tr.AddNewlyCreatedDBObject(line, true);
        }

        public static SortedDictionary<double, IRCEntity> GetSortedNeighborEntitiesForXDataWrite(RCPoint rcPoint)
        {
            IList<RCPoint> neighborEdgePts = rcPoint.GetNeighbors(RC_SPOJNICE.FIXED_SEGMENT);
            Vector2d vector = new Vector2d(1, 0);

            if (neighborEdgePts.Count > 0)
            {
                IList<RCPoint> borderPoints = neighborEdgePts.Where(p => p.IsHranice).ToList();
                if (borderPoints.Count > 1)  // 1st border point is not 1st neighbor point
                {
                    RCPoint firstPt = GeometryHelper.FirstBorderPoint(rcPoint, borderPoints[0], borderPoints[1]);
                    neighborEdgePts.Remove(firstPt);
                    neighborEdgePts.Insert(0, firstPt);
                }
                else if (neighborEdgePts.Count > 1)
                {
                    neighborEdgePts = neighborEdgePts.OrderBy(p => rcPoint.Point2d.AngleTo(p.Point2d)).ToList();  // sort by angle counter-clockwise
                }
                vector = rcPoint.Point2d.VectorTo(neighborEdgePts.FirstOrDefault().Point2d);
            }
            return GetSortedNeighborEntitiesForXDataWriteFromBaseVector(rcPoint, vector);
        }

        private static SortedDictionary<double, IRCEntity> GetSortedNeighborEntitiesForXDataWriteFromBaseVector(RCPoint rcPoint, Vector2d baseVector)
        {
            // entities will be sorted counter-clockwise by angle calculated from the base vector
            SortedDictionary<double, IRCEntity> neighborEntities = new SortedDictionary<double, IRCEntity>();
            double ANGLE_EPSILON = 1e-15;

            // add neighbor points data
            for (int i = 0; i < rcPoint.Lines.Count; i++)
            {
                RCLine line = rcPoint.Lines[i];
                RCPoint otherPt = rcPoint.Equals(line.Pt1) ? line.Pt2 : line.Pt1;

                Vector2d lineVector = rcPoint.Point2d.VectorTo(otherPt.Point2d);
                double angle = baseVector.AngleTo(lineVector);
                if (neighborEntities.ContainsKey(angle))  // identical point - do not save
                {
                }
                else if (RC_SPOJNICE.FIXED_SEGMENT.HasFlag(line.Type))
                {
                    if (angle != 0 && line.Type == RC_SPOJNICE.HRANICE)  // add 2nd border point to the end
                        angle += Math.PI * 2.1;
                    neighborEntities.Add(angle, otherPt);  // neighbor point
                    neighborEntities.Add(angle + 1 * ANGLE_EPSILON, line);  // line type
                    if (angle != 0 && line.Type != RC_SPOJNICE.HRANICE)
                    {
                        neighborEntities.Add(angle + 2 * ANGLE_EPSILON, otherPt.Normal);  // todo: which normal to use?
                        neighborEntities.Add(angle + 3 * ANGLE_EPSILON, otherPt);  // neighbor point (again) 
                        neighborEntities.Add(angle + 4 * ANGLE_EPSILON, line);  // line type (again)
                    }
                }
                else
                {
                    neighborEntities.Add(angle, otherPt);  // neighbor point
                }
            }

            if (neighborEntities.LastOrDefault().Value is RCPoint)  // add 1st point to the end - only internal points
            {
                double angle = neighborEntities.LastOrDefault().Key;
                neighborEntities.Add(angle + 1 * ANGLE_EPSILON, neighborEntities.ElementAt(0).Value);  // neighbor point
                if (neighborEntities.ElementAt(1).Value is RCLine)
                    neighborEntities.Add(angle + 2 * ANGLE_EPSILON, neighborEntities.ElementAt(1).Value);  // line type
            }

            return neighborEntities;
        }

        internal static _AcDb.ResultBuffer CreateXDataForPoint(int number, string tag)
        {
            // no new point is created, only xdata are written
            var newXData = new _AcDb.ResultBuffer
            {
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_BOD),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataInteger16, 1),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataInteger32, number),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataInteger16, 2),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, tag ?? "")
            };

            return newXData;
        }
        internal static string WriteNewPoint(_AcDb.BlockTableRecord btr, _AcDb.Transaction tr, RCPoint rcPoint)
        {
            var point = new _AcDb.DBPoint(rcPoint.Point3d.ToAcGePoint3d());
            if (point == null) return "";

            point.XData = CreateXDataForPoint(rcPoint.Number, rcPoint.Tag);

            btr.AppendEntity(point);
            tr.AddNewlyCreatedDBObject(point, true);

            return point.Handle.ToString();
        }

        /// <summary>
        /// Deletes Xdata for a specified application name from the given entity
        /// This is the only reliable way - manually parsing and rebuilding Xdata
        /// </summary>
        /// <param name="entity">The entity from which to remove Xdata</param>
        /// <param name="applicationName">The name of the application whose Xdata should be removed</param>
        /// <returns>True if Xdata was found and removed, false if no Xdata was found for the application</returns>
        internal static void DeleteXData(_AcDb.Entity entity, string applicationName)
        {
            // Validate input parameters
            if (entity == null && string.IsNullOrEmpty(applicationName))
                return;

            _AcDb.ResultBuffer xdata = entity.XData;
            if (xdata == null) return;

            _AcDb.TypedValue[] xdataArray = xdata.AsArray();
            xdata.Dispose(); // Clean up the original buffer

            foreach (var tv in xdataArray)
            {
                // Check if we're at the start of any application
                if (tv.TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName && tv.Value.ToString() == applicationName)
                {
                    entity.XData = new _AcDb.ResultBuffer(tv);
                }
            }
        }

        /// <summary>
        /// Add extended tram profile XData to the profiles and arc.
        /// </summary>
        internal static void AddXDataToProfiles(_AcDb.Arc arc, _AcDb.Entity[] profiles)
        {
            if (arc == null || profiles == null || profiles.Length != 2) return;
            // Calculate widening values
            double innerWidening = TramProfileData.GetWidening(arc.Radius, -1);
            double outerWidening = TramProfileData.GetWidening(arc.Radius, 1);

            var xData = new _AcDb.ResultBuffer(
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_TRAMPROFIL),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "OBLOUK"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, profiles[0] != null ? profiles[0].Handle.ToString() : "0"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, profiles[1] != null ? profiles[1].Handle.ToString() : "0"),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, innerWidening),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, outerWidening)
            );
            arc.XData = xData;

            if (profiles?[0] != null)
            {
                xData = new _AcDb.ResultBuffer(
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_TRAMPROFIL),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "OBRYS"),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, arc.Handle.ToString()),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataInteger16, -1),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, innerWidening),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, outerWidening)
                );
                profiles[0].XData = xData;
            }

            if (profiles?[1] != null)
            {
                xData = new _AcDb.ResultBuffer(
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, XDataAppNames.RC_TRAMPROFIL),
                new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, "OBRYS"),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, arc.Handle.ToString()),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataInteger16, 1),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, innerWidening),
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataReal, outerWidening)
                );
                profiles[1].XData = xData;
            }
        }
    }
}
