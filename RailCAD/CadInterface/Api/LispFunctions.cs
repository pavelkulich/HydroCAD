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
    public class LispFunctions
    {
        [LispFunction(APINames.RCLIC)]
        public TypedValue RailCadLicence(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionLicence(cad);
            return cad.GetLispRespSingleValue();
        }

        [LispFunction(APINames.CSPTH)]
        public ResultBuffer CsPointsHeight(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionPointsHeight(cad);
            return cad.GetLispResp();
        }

        [LispFunction(APINames.GENTRAMPROFILE)]
        public void TramProfile(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionDrawTramProfile(cad);
        }

        [LispFunction(APINames.DEFTANGREACT)]
        public void DefCurveTangentReactor(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionDefineCurveTangentReactor(cad);
        }

        [LispFunction(APINames.LOADREACTORS)]
        public void LoadReactors(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionInitializeReactors(cad);
        }

        [LispFunction(APINames.FREEPOINTSNUMBERS)]
        public ResultBuffer GetFreePointsNumbers(ResultBuffer resbuf)
        {
            CadModel cad = new CadModel(resbuf);
            RCApp.FunctionGetFreePointsNumbers(cad);
            return cad.GetLispResp();
        }
    }
}