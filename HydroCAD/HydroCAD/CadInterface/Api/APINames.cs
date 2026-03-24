namespace HydroCAD.CadInterface.Api
{
    internal static class APINames
    {
        // CAD Commands
        internal const string HC_IMPORT_POINTS  = "HC_IMPORT_POINTS";
        internal const string HC_BUILD_DTM      = "HC_BUILD_DTM";
        internal const string HC_DELETE_DTM     = "HC_DELETE_DTM";
        internal const string HC_DRAW_PROFILE   = "HC_DRAW_PROFILE";
        internal const string HC_POINTS_HEIGHT  = "HC_POINTS_HEIGHT";

        // Lisp function names
        internal const string HC_DTM_HEIGHT     = "HC_DTM_HEIGHT";   // get height at XY point
    }
}
