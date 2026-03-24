#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using _AcGe = Autodesk.AutoCAD.Geometry;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
    using _AcGe = Teigha.Geometry;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using _AcGe = Gssoft.Gscad.Geometry;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using _AcGe = ZwSoft.ZwCAD.Geometry;
#endif

using RailCAD.Models.Geometry;

namespace RailCAD.CadInterface.Tools
{
    public static class XDataExtensions
    {
        public static _AcDb.TypedValue WriteXData(this RCPoint rcPoint)
        {
            return new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataHandle, rcPoint.Handle);
        }

        public static _AcDb.TypedValue WriteXData(this RCLine rcLine)
        {
            return new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataAsciiString, rcLine.Type.ToString());  // Line type info
        }

        public static _AcDb.TypedValue WriteXData(this Point3d rcPoint)
        {
            return new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataXCoordinate, new _AcGe.Point3d(rcPoint.X, rcPoint.Y, rcPoint.Z));
        }

        public static _AcDb.TypedValue WriteXData(this Vector3d rcVector)
        {
            return new _AcDb.TypedValue((int)_AcDb.DxfCode.ExtendedDataXCoordinate, new _AcGe.Point3d(rcVector.X, rcVector.Y, rcVector.Z));
        }
    }
}
