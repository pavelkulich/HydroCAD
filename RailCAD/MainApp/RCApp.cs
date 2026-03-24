using RailCAD.CadInterface;
using System;
using System.Windows;

namespace RailCAD.MainApp
{
    partial class RCApp
    {
        private static DateTime TIME_LICENCE = new DateTime(2026, 12, 31);
        private static string APP_VERSION = "4000";

        private static bool CheckOrActivateLicence(ICadModel cad, bool fillLispResp = false)
        {
            LicenceType licenceType = CheckLicence(cad, fillLispResp);

            bool licenceValid = DateTime.Now <= RCApp.TIME_LICENCE && licenceType > LicenceType.INVALID;

            if (licenceValid && licenceType == LicenceType.STUD)
            {
                cad.WriteMessageNoDebug(Properties.Resources.StudentLicenceWarning);
            }

            return licenceValid;
        }

        private static void Execute(Action<ICadModel> function, ICadModel cad)
        {
            if (CheckOrActivateLicence(cad))
            {
                // execute function
                try
                {
                    function(cad);
                }
                catch (Exception ex)
                {
                    cad.WriteMessage(ex.ToString());
                    cad.WriteMessageNoDebug(Properties.Resources.ErrorOccured);
                    //throw new Exception("Error occurred");
                }
            }
        }

        private static void ExecuteFunction(Action<ICadModel> function, ICadModel cad)
        {
            try
            {
                function(cad);
            }
            catch (Exception ex)
            {
                cad.WriteMessage(ex.ToString());
                cad.WriteMessageNoDebug(Properties.Resources.ErrorOccured);
                //throw new Exception("Error occurred");
            }
        }

        public static void FunctionLicence(ICadModel cad)
        {
            // execute directly without wrapper
            try
            {
                CheckOrActivateLicence(cad, true);
            }
            catch (Exception ex)
            {
                cad.WriteMessage(ex.ToString());
                cad.WriteMessageNoDebug(Properties.Resources.ErrorOccured);
            }
        }

        public static void FunctionTriangulate(ICadModel cad)
        {
            Execute(Triangulate, cad);
        }

        public static void FunctionDelPointsPercentage(ICadModel cad)
        {
            Execute(DeletePointsPercentage, cad);
        }

        public static void FunctionImportPoints(ICadModel cad)
        {
            Execute(ImportPnts, cad);
        }

        public static void FunctionDeleteTerrainModel(ICadModel cad)
        {
            Execute(DeleteTerrainModel, cad);
        }

        public static void CommandDrawTramProfile(ICadModel cad)
        {
            Execute(DrawTramProfile, cad);
        }

        public static void FunctionClosestPolylineDistance(ICadModel cad)
        {
            Execute(ClosestPolylineDistance, cad);
        }

        // LISP functions
        public static void FunctionPointsHeight(ICadModel cad)
        {
            ExecuteFunction(PointsHeight, cad);
        }

        public static void FunctionDrawTramProfile(ICadModel cad)
        {
            ExecuteFunction(DrawTramProfile, cad);
        }

        public static void FunctionDefineCurveTangentReactor(ICadModel cad)
        {
            cad.DefineCurveTangentReactor();
        }

        public static void FunctionInitializeReactors(ICadModel cad)
        {
            cad.InitializeReactors();
        }

        public static void FunctionGetFreePointsNumbers(ICadModel cad)
        {
            ExecuteFunction(GetFreePointsNumbers, cad);
        }
    }
}
