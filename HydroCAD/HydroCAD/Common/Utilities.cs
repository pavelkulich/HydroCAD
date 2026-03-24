namespace HydroCAD.Common
{
    internal static class Utilities
    {
        internal static bool IsNullHandle(this string handle)
        {
            return handle == null || handle == "" || handle == "0";
        }
    }
}
