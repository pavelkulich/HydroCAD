#if ACAD
    using _AcGe = Autodesk.AutoCAD.Geometry;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcGe = Teigha.Geometry;
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcGe = Gssoft.Gscad.Geometry;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcGe = ZwSoft.ZwCAD.Geometry;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using HydroCAD.Models.Geometry;

namespace HydroCAD.CadInterface.Tools
{
    internal static class LispCadExtensions
    {
        // ── CAD → HC ────────────────────────────────────────────────────

        public static Point2d ToPoint2d(this _AcGe.Point2d pt) => new Point2d(pt.X, pt.Y);
        public static Point2d ToPoint2d(this _AcGe.Point3d pt) => new Point2d(pt.X, pt.Y);
        public static Point3d ToPoint3d(this _AcGe.Point3d pt) => new Point3d(pt.X, pt.Y, pt.Z);
        public static Vector3d ToVector3d(this _AcGe.Point3d pt) => new Vector3d(pt.X, pt.Y, pt.Z);
        public static Vector3d ToVector3d(this _AcGe.Vector3d v) => new Vector3d(v.X, v.Y, v.Z);

        // ── HC → CAD ────────────────────────────────────────────────────

        public static _AcGe.Point2d ToAcGePoint2d(this Point2d pt) => new _AcGe.Point2d(pt.X, pt.Y);
        public static _AcGe.Point3d ToAcGePoint3d(this Point2d pt, double z = 0) => new _AcGe.Point3d(pt.X, pt.Y, z);
        public static _AcGe.Point3d ToAcGePoint3d(this Point3d pt) => new _AcGe.Point3d(pt.X, pt.Y, pt.Z);
        public static _AcGe.Vector3d ToAcGeVector3d(this Vector3d v) => new _AcGe.Vector3d(v.X, v.Y, v.Z);

        // ── TypedValue helpers ───────────────────────────────────────────

        public static string GetAsString(this _AcDb.TypedValue tv) => tv.Value?.ToString() ?? string.Empty;

        public static Point2d GetAsPoint2d(this _AcDb.TypedValue tv)
        {
            if (tv.Value is _AcGe.Point2d p2) return p2.ToPoint2d();
            if (tv.Value is _AcGe.Point3d p3) return p3.ToPoint2d();
            return new Point2d(0, 0);
        }

        public static Vector3d GetAsVector3d(this _AcDb.TypedValue tv)
        {
            if (tv.Value is _AcGe.Point3d p3) return p3.ToVector3d();
            return new Vector3d(0, 0, 1);
        }

        public static double GetAsDouble(this _AcDb.TypedValue tv)
        {
            if (tv.Value is double d) return d;
            if (double.TryParse(tv.Value?.ToString(), out double result)) return result;
            return 0;
        }

        public static int GetAsInt(this _AcDb.TypedValue tv)
        {
            if (tv.Value is int i) return i;
            if (int.TryParse(tv.Value?.ToString(), out int result)) return result;
            return 0;
        }
    }
}
