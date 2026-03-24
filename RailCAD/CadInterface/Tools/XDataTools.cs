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
using System.Text;


namespace RailCAD.CadInterface.Tools
{
    internal static class XDataTools
    {
        internal static _AcDb.TypedValue[] ReadXDataResBufArray(_AcDb.DBObject dbObj, string applicationName)
        {
            _AcDb.ResultBuffer rb = dbObj.GetXDataForApplication(applicationName);

            if (rb != null)
            {
                return rb.AsArray();
            }
            return null;
        }

        internal static bool TryReadXDataAtIndex(_AcDb.TypedValue[] rvArr, int index, out _AcDb.TypedValue typedValue, _AcDb.DxfCode typeCheck = _AcDb.DxfCode.Invalid)
        {
            if (rvArr != null && rvArr.Length > index)
            {
                typedValue = rvArr[index];  // assign result

                if (typeCheck != _AcDb.DxfCode.Invalid)  // perform type check?
                {
                    if ((_AcDb.DxfCode)typedValue.TypeCode == typeCheck)
                    {
                        return true;
                    }
                }
                else  // without type check
                {
                    return true;
                }
            }

            // not successful
            typedValue = new _AcDb.TypedValue((int)_AcDb.DxfCode.Invalid);
            return false;
        }

        internal static bool TryReadXDataAtIndex(_AcDb.DBObject dbObj, string applicationName, int index, out _AcDb.TypedValue typedValue, _AcDb.DxfCode typeCheck = _AcDb.DxfCode.Invalid)
        {
            _AcDb.ResultBuffer rb = dbObj.GetXDataForApplication(applicationName);

            if (rb != null)
            {
                _AcDb.TypedValue[] rvArr = rb.AsArray();

                if (rvArr != null && rvArr.Length > index)
                {
                    typedValue = rvArr[index];  // assign result

                    if (typeCheck != _AcDb.DxfCode.Invalid)  // perform type check?
                    {
                        if ((_AcDb.DxfCode)typedValue.TypeCode == typeCheck)
                        {
                            return true;
                        }
                    }
                    else  // without type check
                    {
                        return true;
                    }
                }
            }

            // not successful
            typedValue = new _AcDb.TypedValue((int)_AcDb.DxfCode.Invalid);
            return false;
        }

        internal static void AppendOrAddSingleXData(_AcDb.Entity entity, string applicationName, _AcDb.TypedValue newXData)
        {
            _AcDb.ResultBuffer buffer = entity.GetXDataForApplication(applicationName);
            if (buffer != null)  // append
            {
                entity.UpgradeOpen();
                buffer.Add(newXData);
                entity.XData = buffer;
                buffer.Dispose();
            }
            else  // write to a new application name
            {
                // application name must already be registered!
                entity.XData = new _AcDb.ResultBuffer
                {
                    new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataRegAppName, applicationName),
                    newXData,
                };
            }
        }

        internal static void AddRegAppTableRecord(string regAppName, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // new app names must be registered: https://www.keanw.com/2007/04/adding_xdata_to.html
            _AcDb.RegAppTable rat = (_AcDb.RegAppTable)tr.GetObject(db.RegAppTableId, _AcDb.OpenMode.ForRead, false);

            if (!rat.Has(regAppName))
            {
                rat.UpgradeOpen();

                _AcDb.RegAppTableRecord ratr = new _AcDb.RegAppTableRecord();
                ratr.Name = regAppName;

                rat.Add(ratr);
                tr.AddNewlyCreatedDBObject(ratr, true);
            }
        }

        internal static void ReadEntityXData(_AcDb.Entity ent, _AcEd.Editor ed, string applicationName)
        {
            // https://spiderinnet1.typepad.com/blog/2012/11/autocad-net-xdata-read-existing-xdata-from-entityobject.html
            _AcDb.ResultBuffer rb = ent.GetXDataForApplication(applicationName);
            if (rb != null)
            {
                _AcDb.TypedValue[] rvArr = rb.AsArray();
                foreach (_AcDb.TypedValue tv in rvArr)
                {
                    switch ((_AcDb.DxfCode)tv.TypeCode)
                    {
                        case _AcDb.DxfCode.ExtendedDataRegAppName:
                            string appName = (string)tv.Value;
                            ed.WriteMessage("\nXData of appliation name (1001) {0}:", appName);
                            break;
                        case _AcDb.DxfCode.ExtendedDataAsciiString:
                            string asciiStr = (string)tv.Value;
                            ed.WriteMessage("\n\tAscii string (1000): {0}", asciiStr);
                            break;
                        case _AcDb.DxfCode.ExtendedDataLayerName:
                            string layerName = (string)tv.Value;
                            ed.WriteMessage("\n\tLayer name (1003): {0}", layerName);
                            break;
                        case _AcDb.DxfCode.ExtendedDataBinaryChunk:
                            Byte[] chunk = tv.Value as Byte[];
                            string chunkText = Encoding.ASCII.GetString(chunk);
                            ed.WriteMessage("\n\tBinary chunk (1004): {0}", chunkText);
                            break;
                        case _AcDb.DxfCode.ExtendedDataHandle:
                            ed.WriteMessage("\n\tObject handle (1005): {0}", tv.Value);
                            break;
                        case _AcDb.DxfCode.ExtendedDataXCoordinate:
                            _AcGe.Point3d pt = (_AcGe.Point3d)tv.Value;
                            ed.WriteMessage("\n\tPoint (1010): {0}", pt.ToString());
                            break;
                        case _AcDb.DxfCode.ExtendedDataWorldXCoordinate:
                            _AcGe.Point3d pt1 = (_AcGe.Point3d)tv.Value;
                            ed.WriteMessage("\n\tWorld point (1011): {0}", pt1.ToString());
                            break;
                        case _AcDb.DxfCode.ExtendedDataWorldXDisp:
                            _AcGe.Point3d pt2 = (_AcGe.Point3d)tv.Value;
                            ed.WriteMessage("\n\tDisplacement (1012): {0}", pt2.ToString());
                            break;
                        case _AcDb.DxfCode.ExtendedDataWorldXDir:
                            _AcGe.Point3d pt3 = (_AcGe.Point3d)tv.Value;
                            ed.WriteMessage("\n\tDirection (1013): {0}", pt3.ToString());
                            break;
                        case _AcDb.DxfCode.ExtendedDataControlString:
                            string ctrStr = (string)tv.Value;
                            ed.WriteMessage("\n\tControl string (1002): {0}", ctrStr);
                            break;
                        case _AcDb.DxfCode.ExtendedDataReal:
                            double realValue = (double)tv.Value;
                            ed.WriteMessage("\n\tReal (1040): {0}", realValue);
                            break;
                        case _AcDb.DxfCode.ExtendedDataDist:
                            double dist = (double)tv.Value;
                            ed.WriteMessage("\n\tDistance (1041): {0}", dist);
                            break;
                        case _AcDb.DxfCode.ExtendedDataScale:
                            double scale = (double)tv.Value;
                            ed.WriteMessage("\n\tScale (1042): {0}", scale);
                            break;
                        case _AcDb.DxfCode.ExtendedDataInteger16:
                            Int16 int16 = (short)tv.Value;
                            ed.WriteMessage("\n\tInt16 (1070): {0}", int16);
                            break;
                        case _AcDb.DxfCode.ExtendedDataInteger32:
                            Int32 int32 = (Int32)tv.Value;
                            ed.WriteMessage("\n\tInt32 (1071): {0}", int32);
                            break;
                        default:
                            ed.WriteMessage("\n\tUnknown XData DXF code.");
                            break;
                    }
                }
            }
        }
    }
}
