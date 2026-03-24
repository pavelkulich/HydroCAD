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
using System.Text.RegularExpressions;

using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;
using RailCAD.Models.Alignment;

namespace RailCAD.CadInterface.Tools
{
    internal class CadXDataReader
    {
        internal static IList<RCPoint> CreatePointsFromSelection(_AcEd.SelectionSet acSSet, _AcDb.Transaction tr, bool requireXData)
        {
            var points = new List<RCPoint>(acSSet.Count);

            foreach (_AcEd.SelectedObject acSSObj in acSSet)
            {
                if (acSSObj != null)  // valid SelectedObject object was returned?
                {
                    _AcDb.DBPoint dbPt = (_AcDb.DBPoint)tr.GetObject(acSSObj.ObjectId, _AcDb.OpenMode.ForRead);

                    if (dbPt != null)
                    {
                        RCPoint rcPoint = requireXData ? ReadPoint(dbPt) : CreatePointFromDbPt(dbPt);
                        if (rcPoint != null)
                        {
                            points.Add(rcPoint);
                        }
                    }
                }
            }

            return points;
        }

        internal static IList<RCLine> CreateSegmentsFromSelection(_AcEd.SelectionSet acSSet, _AcDb.Transaction tr, bool requireXData, IDictionary<string, RCPoint> handleMap)
        {
            var segments = new List<RCLine>(acSSet.Count);
            foreach (_AcEd.SelectedObject acSSObj in acSSet)
            {
                if (acSSObj != null)  // valid SelectedObject object was returned?
                {
                    _AcDb.Entity dbLine = tr.GetObject(acSSObj.ObjectId, _AcDb.OpenMode.ForRead) as _AcDb.Entity;

                    RCLine rcLine = requireXData ? ReadLine(dbLine, handleMap) : throw new NotImplementedException();
                    if (rcLine != null)
                    {
                        segments.Add(rcLine);
                    }
                }
            }
            segments.Reverse();

            return segments;
        }

        private static RCPoint CreatePointFromDbPt(_AcDb.DBPoint dbPt)
        {
            var rcBodId = (int)dbPt.Id.OldIdPtr;
            var rcBodType = RC_BOD.ZAKLAD;  // use basic point type
            _AcGe.Point3d position = dbPt.Position;
            return new RCPoint(position.ToPoint3d(), rcBodId, dbPt.Handle.ToString(), rcBodType);
        }

        /// <summary>
        /// Creates a RCPoint (object) of a numbered point in drawing according to the Xdata information.
        /// </summary>
        internal static RCPoint ReadPoint(_AcDb.DBPoint ent, string appName = "")
        {
            HashSet<string> neighbors = new HashSet<string>();
            if (ent == null) return null;

            var xdata = ent.XData;
            if (xdata == null) return null;
            _AcDb.TypedValue[] sezXdat = xdata.AsArray();

            int num = 0;
            string tag = "";
            string textNum = "0";
            string textHeight1 = "0";
            string textHeight2 = "0";
            string textTag = "0";
            string sign = "0";
            string label = "0";
            RC_BOD type = RC_BOD.ZAKLAD;
            string mode = "-";
            Vector3d? normal = null;
            bool rcBodDone = false;
            bool rcSingDone = true;
            bool appNameDone = appName == "" ? true : false;
            int len = sezXdat.Length;

            for (int i = 0; i < len - 1; i++)
            {
                var val = sezXdat[i];
                int code = val.TypeCode;

                if (code == (int)_AcDb.DxfCode.ExtendedDataRegAppName)
                {
                    if (mode == XDataAppNames.RC_BOD)
                    {
                        rcBodDone = true;
                    }
                    else if (mode == appName)
                    {
                        appNameDone = true;
                    }
                    mode = "-";
                    switch ((string)val.Value)
                    {
                        //case XDataAppNames.RC_SING: // fixed terrain point
                        //    if (sezXdat[i + 1].TypeCode == (int)_AcDb.DxfCode.ExtendedDataAsciiString)
                        //    {
                        //        Enum.TryParse((string)sezXdat[i + 1].Value, out type); // convert string to enum
                        //        i++;
                        //    }
                        //    rcSingDone = true;
                        //    break;
                        case XDataAppNames.RC_BOD: // point data
                            mode = XDataAppNames.RC_BOD;
                            break;
                        case XDataAppNames.RC_ASSBOD:
                        case XDataAppNames.RC_BODINFO:
                            break;
                        default: // RC_D# - terrain model
                            if ((string)val.Value == appName)
                            {
                                mode = appName;
                                i++;
                            }
                            break;
                    }
                }
                else if (mode == XDataAppNames.RC_BOD && code == (int)_AcDb.DxfCode.ExtendedDataInteger16) // point data
                {
                    short key = (short)val.Value;
                    //short key = Convert.ToInt16(val.Value);
                    var nextVal = sezXdat[i + 1].Value;

                    switch (key)
                    {
                        case 1: num = Convert.ToInt32(nextVal); break; // always present
                        case 2: tag = (string)nextVal; break; // always present
                        case 3: textNum = (string)nextVal; break;
                        case 4: textHeight1 = (string)nextVal; break;
                        case 5: textHeight2 = (string)nextVal; break;
                        case 6: textTag = (string)nextVal; break;
                        case 7: sign = (string)nextVal; break;
                        case 8: label = (string)sezXdat[i + 1].Value; break;
                    }
                    i++;
                }
                else if (mode == appName) // terrain model
                {
                    if (code == (int)_AcDb.DxfCode.ExtendedDataXCoordinate && normal == null) // only 1st normal
                    {
                        normal = val.GetAsVector3d(); // read normal
                    }
                    else if (code == (int)_AcDb.DxfCode.ExtendedDataHandle)
                    {
                        neighbors.Add(val.GetAsString()); // adds only if handle is not already in the list
                    }
                }
                // Early-exit
                if (rcBodDone && rcSingDone && appNameDone)
                    break;
            }

            // create a new point
            var pos = ent.Position;
            var point = new RCPoint(pos.ToPoint3d(), num, ent.Handle.ToString(), type, tag);
            if (normal != null)
                point.SetNormal(normal.Value);
            point.SetNeighborsHandles(neighbors);
            point.SetAssociatedEntities(label, sign, textNum, textTag, textHeight1, textHeight2);
            return point;
        }

        /// <summary>
        /// Creates a RCLine (object) of a connecting line between 2 points in drawing according to the Xdata information.
        /// </summary>
        internal static RCLine ReadLine(_AcDb.Entity ent, IDictionary<string, RCPoint> handleMap, string appName = "", bool fixedSegment = false)
        {
            if (ent == null) return null;

            var xdata = ent.XData;
            if (xdata == null) return null;
            _AcDb.TypedValue[]sezXdat = xdata.AsArray();
            int len = sezXdat.Length;

            for (int i = 0; i < len - 1; i++)
            {
                var val = sezXdat[i];
                if (val.TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName && val.Value is string app && app == XDataAppNames.RC_SPOJNICE)
                {
                    // XData always has the same structure
                    var typeStr = sezXdat[i + 1].Value as string;
                    var h1 = sezXdat[i + 2].Value as string;
                    var h2 = sezXdat[i + 3].Value as string;

                    if (typeStr != null && h1 != null && h2 != null)
                    {
                        Enum.TryParse(typeStr, out RC_SPOJNICE type);
                        handleMap.TryGetValue(h1, out var pt1);
                        handleMap.TryGetValue(h2, out var pt2);

                        if ((fixedSegment
                            ? RC_SPOJNICE.FIXED_SEGMENT.HasFlag(type)
                            : RC_SPOJNICE.POSSIBLE_TYPES_FOR_DEFINITION.HasFlag(type))
                            && pt1 != null && pt2 != null && !pt1.Equals(pt2))
                        {
                            return new RCLine(pt1, pt2, type, ent.Handle.ToString());
                        }
                    }
                    break; // no more XData → quick exit
                }
            }

            return null;
        }

        /// <summary>
        /// Returns application name from entity XData that begins with the desired prefix.
        /// </summary>
        internal static IList<string> ReadAppNames(_AcDb.Entity ent, string prefix)
        {
            if (ent == null) return null;

            var xdata = ent.XData;
            if (xdata == null) return null;

            _AcDb.TypedValue[] sezXdat = xdata.AsArray();
            IList<string> apps = new List<string>();
            string pattern = "^" + Regex.Escape(prefix) + @"\d+$";
            Regex regex = new Regex(pattern);

            foreach (var val in sezXdat)
            {
                if (val.TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName && val.Value is string app && regex.IsMatch(app))
                {
                    apps.Add(app);
                }
            }

            if (apps.Count > 0)
            {
                return apps;
            }
            return null;
        }

        /// <summary>
        /// Tests if the line is of the correct type. If not, it returns null.
        /// </summary>
        internal static bool TestLineType(_AcDb.Entity ent, RC_SPOJNICE searchTypes)
        {
            if (ent == null) return false;

            var xdata = ent.XData;
            if (xdata == null) return false;
            _AcDb.TypedValue[] sezXdat = xdata.AsArray();
            int len = sezXdat.Length;

            for (int i = 0; i < len - 1; i++)
            {
                var val = sezXdat[i];
                if (val.TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName && val.Value is string app && app == XDataAppNames.RC_SPOJNICE)
                {
                    // XData always has the same structure
                    var typeStr = sezXdat[i + 1].Value as string;

                    if (typeStr != null)
                    {
                        Enum.TryParse(typeStr, out RC_SPOJNICE type);

                        if (searchTypes.HasFlag(type))
                        {
                            return true;
                        }
                    }
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Creates a RCCurve (object) of a selected curve in drawing from the Xdata information.
        /// </summary>
        /// <param name="arc">Main curve entity (arc).</param>
        /// <returns>Returns a new RCCurve object.</returns>
        public static RCCurve ReadCurve(_AcDb.Arc arc, _AcDb.Transaction tr, _AcDb.Database db)
        {
            if (arc == null) return null;
            var Xdata = arc.XData;
            if (Xdata == null) return null;
            var sezXdat = Xdata.AsArray();

            IList<double> speeds = new List<double>(new double[5]);
            RCCurve curve = new RCCurve(arc.Handle.ToString());
            var tang1 = curve.Tangent1;
            var tang2 = curve.Tangent2;
            var spiral1 = curve.Spiral1;
            var spiral2 = curve.Spiral2;

            for (int i = 0; i < sezXdat.Length; i++)
            {
                if (sezXdat[i].TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName)
                {
                    switch (sezXdat[i].Value.ToString())
                    {
                        // basic curve parameters - fixed format (specific order of parameters)
                        case XDataAppNames.RC_SMER:
                            i += 2;
                            tang1.Point1 = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang1.Point2 = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang1.Cant = (double)sezXdat[i++].Value;
                            tang1.Radius = (double)sezXdat[i++].Value;
                            tang1.Handle = sezXdat[i++].Value.ToString();
                            i += 2;
                            tang2.Point1 = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang2.Point2 = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang2.Cant = (double)sezXdat[i++].Value;
                            tang2.Radius = (double)sezXdat[i++].Value;
                            tang2.Handle = sezXdat[i++].Value.ToString();
                            i += 2;
                            spiral1.Type = (String)sezXdat[i++].Value;
                            spiral1.Length = (double)sezXdat[i++].Value;
                            spiral1.Slope = (double)sezXdat[i++].Value;
                            i += 2;
                            spiral2.Type = (String)sezXdat[i++].Value;
                            spiral2.Length = (double)sezXdat[i++].Value;
                            spiral2.Slope = (double)sezXdat[i++].Value;
                            i += 2;
                            curve.Radius = (double)sezXdat[i++].Value;
                            speeds[0] = (double)sezXdat[i++].Value;
                            curve.Cant = (double)sezXdat[i++].Value;
                            curve.Turn = (String)sezXdat[i += 1].Value;
                            i++;
                            break;
                        // additional curve parameters - dynamic format (parameter names and values)
                        case XDataAppNames.RCAD:
                            for (i++; i < sezXdat.Length; i += 2)
                            {
                                var key = sezXdat[i];
                                if (key.TypeCode == (int)_AcDb.DxfCode.ExtendedDataAsciiString)
                                {
                                    object val = sezXdat[i + 1].Value;
                                    switch (key.Value.ToString())
                                    {
                                        case "V_1":
                                            speeds[1] = (double)val;
                                            break;
                                        case "V_2":
                                            speeds[2] = (double)val;
                                            break;
                                        case "V_3":
                                            speeds[3] = (double)val;
                                            break;
                                        case "V_4":
                                            speeds[4] = (double)val;
                                            break;
                                        case "du":
                                            curve.GaugeWidening = (double)val;
                                            break;
                                        case "Lu_1":
                                            tang1.GaugeRun = (double)val;
                                            break;
                                        case "Lu_2":
                                            tang2.GaugeRun = (double)val;
                                            break;
                                        case "cisl":
                                            curve.Number = (String)val;
                                            break;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                            i--;
                            break;
                        // routing data (for inclusion into axis) - fixed format (specific order of parameters)
                        case XDataAppNames.RC_TRASA:
                            i += 2;
                            tang1.EndPoint = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang1.Direction = ((_AcGe.Point3d)sezXdat[i++].Value).ToVector3d();
                            tang1.Length = (double)sezXdat[i++].Value;
                            i += 2;
                            tang2.EndPoint = ((_AcGe.Point3d)sezXdat[i++].Value).ToPoint3d();
                            tang2.Direction = ((_AcGe.Point3d)sezXdat[i++].Value).ToVector3d();
                            tang2.Length = (double)sezXdat[i++].Value;
                            i += 2;
                            curve.Length = (double)sezXdat[i++].Value;
                            break;
                        // links to the curve entities - fixed format (specific order of parameters)
                        case XDataAppNames.RC_ENOBL:
                            i += 3;
                            curve.Circles.Add(sezXdat[i++].Value.ToString());
                            curve.Circles.Add(sezXdat[i++].Value.ToString());
                            curve.Circles.Add(sezXdat[i++].Value.ToString());
                            curve.Circles.Add(sezXdat[i++].Value.ToString());
                            i += 3;
                            spiral1.Handle = sezXdat[i++].Value.ToString();
                            spiral2.Handle = sezXdat[i++].Value.ToString();
                            break;
                    }
                }
            }
            if (curve.Radius > 0)
            {
                curve.Speeds = speeds;

                return curve;
            }
            return null;
        }

        /// <summary>
        /// Creates a RCTurnout (object) of a selected turnout in drawing from the Xdata information.
        /// </summary>
        /// <param name="turnout">Main turnout entity (circle).</param>
        /// <returns>Returns a new RCTurnout object.</returns>
        public static RCTurnout ReadTurnout(_AcDb.Entity turnoutEntity)
        {
            if (turnoutEntity == null) return null;
            var Xdata = turnoutEntity.XData;
            if (Xdata == null) return null;
            var sezXdat = Xdata.AsArray();

            RCTurnout turnout = new RCTurnout(turnoutEntity.Handle.ToString());

            for (int i = 0; i < sezXdat.Length; i++)
            {
                // turnout data - dynamic format (parameter names and values) 
                if (sezXdat[i].TypeCode == (int)_AcDb.DxfCode.ExtendedDataRegAppName && sezXdat[i].Value.ToString() == XDataAppNames.VYHYBKA)
                {
                    for (i = i + 1; i < sezXdat.Length; i += 2)
                    {
                        var key = sezXdat[i];
                        if (key.TypeCode == (int)_AcDb.DxfCode.ExtendedDataInteger16)
                        {
                            object val = sezXdat[i + 1].Value;
                            switch (Convert.ToInt16(key.Value))
                            {
                                case 1:
                                    turnout.Shape = (String)val;
                                    break;
                                case 2:
                                    turnout.Rail = (String)val;
                                    break;
                                case 3:
                                    turnout.Angle = (String)val;
                                    break;
                                case 4:
                                    turnout.Radius = Convert.ToDouble((String)val);
                                    break;
                                case 5:
                                    turnout.DriverSide = (String)val;
                                    break;
                                case 6:
                                    turnout.SleeperMaterial = (String)val;
                                    break;
                                case 7:
                                    turnout.Type = (String)val;
                                    break;
                                case 9:
                                    turnout.Number = (String)val;
                                    break;
                                case 10:
                                    turnout.EndPoints[0] = ((_AcGe.Point3d)val).ToPoint3d();
                                    break;
                                case 11:
                                    turnout.EndPoints[1] = ((_AcGe.Point3d)val).ToPoint3d();
                                    break;
                                case 12:
                                    turnout.EndPoints[2] = ((_AcGe.Point3d)val).ToPoint3d();
                                    break;
                                case 13:
                                    turnout.EndPoints[3] = ((_AcGe.Point3d)val).ToPoint3d();
                                    break;
                                case 14:
                                    turnout.Transf = (String)val;
                                    break;
                                case 15:
                                    turnout.Note = (String)val;
                                    break;
                                case 16:
                                    turnout.PotSleeper = (String)val;
                                    break;
                                case 17:
                                    turnout.AddedInfo = (String)val;
                                    break;
                                case 18:
                                    turnout.MainBranch = (String)val;
                                    break;
                                case 19:
                                    turnout.Cant = (double)val;
                                    break;
                                case 20:
                                    turnout.LockType = (String)val;
                                    break;
                                case 21:
                                    turnout.Fastening = (String)val;
                                    break;
                                case 22:
                                    turnout.FrogType = (String)val;
                                    break;
                                case 23:
                                    turnout.LabelRadius = (String)val;
                                    break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    i--;
                }
            }

            return turnout;
        }
    }
}
