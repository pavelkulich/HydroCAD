#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcEd = Autodesk.AutoCAD.EditorInput;
    using _AcGe = Autodesk.AutoCAD.Geometry;
    using _AcRn = Autodesk.AutoCAD.Runtime;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
    using _AcEd = Bricscad.EditorInput;
    using _AcGe = Teigha.Geometry;
    using _AcRn = Bricscad.Runtime;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcEd = Gssoft.Gscad.EditorInput;
    using _AcGe = Gssoft.Gscad.Geometry;
    using _AcRn = Gssoft.Gscad.Runtime;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcEd = ZwSoft.ZwCAD.EditorInput;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
    using _AcRn = ZwSoft.ZwCAD.Runtime;
#endif

using System;
using System.Collections.Generic;
using System.Linq;

using RailCAD.Models.Geometry;
using RailCAD.MainApp;

namespace RailCAD.CadInterface.Tools
{
    using CsPthInput = Tuple<string, IList<Point2d>>;

    internal class ResBufIO
    {
        public static CsPthInput ReadCsPthInput(_AcDb.ResultBuffer resbuf)
        {
            _AcDb.TypedValue[] rvArr = resbuf.AsArray();
            if (rvArr.Length >= 2)
            {
                // read app name
                string appName = rvArr[0].GetAsString();

                // read points (in a list)
                var points = new List<Point2d>(rvArr.Length - 3);
                for (int i = 1; i < rvArr.Length; i++)
                {
                    if (rvArr[i].TypeCode == (short)_AcRn.LispDataType.ListBegin || rvArr[i].TypeCode == (short)_AcRn.LispDataType.ListEnd)
                        continue;

                    points.Add(rvArr[i].GetAsPoint2d());
                }

                return new CsPthInput(appName, points);
            }

            return null;
        }

        public static _AcDb.ResultBuffer WriteCsPthResp(IList<Point3d> pts)
        {
            var typedValues = new _AcDb.TypedValue[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                typedValues[i] = new _AcDb.TypedValue((int)_AcRn.LispDataType.Point3d, pts[i].ToAcGePoint3d());
            }
            var resBuf = new _AcDb.ResultBuffer(typedValues);

            return resBuf;
        }

        public static string ReadRCLicenceInput(_AcDb.ResultBuffer resbuf)
        {
            _AcDb.TypedValue[] rvArr = resbuf.AsArray();
            if (rvArr.Length > 0)
            {
                // read optional parameter
                return rvArr[0].GetAsString();
            }
            return null;
        }

        public static _AcDb.ResultBuffer WriteLicenceResp(LicenceType licence)
        {
            return new _AcDb.ResultBuffer
            {
                new _AcDb.TypedValue((int)_AcRn.LispDataType.Int16, licence)
            };
        }

        public static _AcDb.ResultBuffer WritePointNumbersResp(IList<int> numbers)
        {
            var resBuf = new _AcDb.ResultBuffer(
                numbers.Select(n => new _AcDb.TypedValue((int)_AcRn.LispDataType.Int32, n)).ToArray()
            );

            return resBuf;
        }
    }
}
