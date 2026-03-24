#if ACAD
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.Runtime;
#elif BCAD
    using Teigha.DatabaseServices;
    using Teigha.Runtime;
    using Bricscad.Runtime;
#elif GCAD
    using Gssoft.Gscad.DatabaseServices;
    using Gssoft.Gscad.Runtime;
#elif ZCAD
    using ZwSoft.ZwCAD.DatabaseServices;
    using ZwSoft.ZwCAD.Runtime;
#endif

using HydroCAD.MainApp;

namespace HydroCAD.CadInterface.Api
{
    public class Commands
    {
        /// <summary>Import survey points from a text/CSV file.</summary>
        [CommandMethod(APINames.HC_IMPORT_POINTS)]
        public void ImportPoints()
        {
            HCApp.FunctionImportPoints(new CadModel());
        }

        /// <summary>Build a digital terrain model (DTM) from selected survey points.</summary>
        [CommandMethod(APINames.HC_BUILD_DTM)]
        public void BuildTerrainModel()
        {
            HCApp.FunctionBuildTerrainModel(new CadModel());
        }

        /// <summary>Delete the current terrain model from the drawing.</summary>
        [CommandMethod(APINames.HC_DELETE_DTM)]
        public void DeleteTerrainModel()
        {
            HCApp.FunctionDeleteTerrainModel(new CadModel());
        }

        /// <summary>Draw a longitudinal profile along a selected pipe route polyline.</summary>
        [CommandMethod(APINames.HC_DRAW_PROFILE)]
        public void DrawProfile()
        {
            HCApp.FunctionDrawProfile(new CadModel());
        }

        /// <summary>Calculate and update elevations of selected survey points from the DTM.</summary>
        [CommandMethod(APINames.HC_POINTS_HEIGHT)]
        public void GetPointsHeight()
        {
            HCApp.FunctionGetPointsHeight(new CadModel());
        }
    }
}
