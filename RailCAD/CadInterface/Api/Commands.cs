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

using RailCAD.MainApp;

namespace RailCAD.CadInterface.Api
{
    public class Commands
    {
        [CommandMethod(APINames.SESTAV_MODEL)]
        public void CsTriangulate()
        {
            RCApp.FunctionTriangulate(new CadModel());
        }

        [CommandMethod(APINames.BODY_SMAZ)]
        public void DelEveryNthPoint()
        {
            RCApp.FunctionDelPointsPercentage(new CadModel());
        }

        [CommandMethod(APINames.BODYIN)]
        public void ImportPointsFromFile()
        {
            RCApp.FunctionImportPoints(new CadModel());
        }

        [CommandMethod(APINames.SMAZ_MODEL)]
        public void DeleteTerrainModel()
        {
            RCApp.FunctionDeleteTerrainModel(new CadModel());
        }

        [CommandMethod(APINames.TRAMPROFIL)]
        public void TramProfile()
        {
            RCApp.CommandDrawTramProfile(new CadModel());
        }

        [CommandMethod(APINames.VZDALENOST_KRIVEK)]
        public void ClosestPolylineDistance()
        {
            RCApp.FunctionClosestPolylineDistance(new CadModel());
        }

        [CommandMethod(APINames.RCLICENCE)]
        public void RenewRailCADLicence()
        {
            CadModel cad = new CadModel(
                new ResultBuffer(new TypedValue((int)LispDataType.Text, "REGENERATE"))
            );
            RCApp.FunctionLicence(cad);
        }
    }
}