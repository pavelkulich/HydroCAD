#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.Runtime;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
    using Bricscad.Runtime;
    using Teigha.Runtime;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
    using Gssoft.Gscad.Runtime;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
    using ZwSoft.ZwCAD.Runtime;
#endif

using HydroCAD.MainApp;

namespace HydroCAD.CadInterface.Api
{
    public class LispFunctions
    {
        /// <summary>
        /// Returns the terrain elevation (Z) at a given X,Y point.
        /// Usage from AutoLISP: (HC_DTM_HEIGHT x y)
        /// Returns the height as a real number, or nil if outside DTM.
        /// </summary>
        [LispFunction(APINames.HC_DTM_HEIGHT)]
        public void LispGetDtmHeight(_AcDb.ResultBuffer resbuf)
        {
            HCApp.FunctionGetPointsHeight(new CadModel(resbuf));
        }
    }
}
