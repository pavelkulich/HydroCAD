#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using System;
using System.Collections.Generic;

using RailCAD.Models.Geometry;
using RailCAD.Models.TerrainModel;

namespace RailCAD.CadInterface
{
    internal interface ICadModel
    {
        void WriteMessage(object message);

        void WriteMessageNoDebug(object message);

        bool PromptUserSettings(out UserSettings settings);

        string PromptActivateLicence();

        object ReadAndValidateLispArgs(Func<_AcDb.ResultBuffer, object> readArgsFunction);

        void SetLispResp<T>(Func<T, _AcDb.ResultBuffer> writeRespFunction, T resp);

        _AcDb.ResultBuffer GetLispInputArgs();
        
        _AcDb.ResultBuffer GetLispResp();

        _AcDb.TypedValue GetLispRespSingleValue();

        void ImportPoints();

        void DrawTramProfile();

        void ClosestPolylineDistance();

        IList<RCPoint> SelectPoints(string message = "");

        IList<RCPoint> SelectAllPoints();

        IList<RCPoint> SelectPointsManually(string message, bool requireXData = true);

        IList<RCLine> SelectSegments(IDictionary<string, RCPoint> handleMap, bool requireXData = true);

        void DeletePointsPercentage(IList<RCPoint> points);

        void WriteTriangulation(TerrainModel terrainModel, bool showTriangles);

        void SaveTerrainModel(TerrainModel terrainModel);
        
        TerrainModel LoadTerrainModel(string appName);

        TerrainModel ReadTerrainModel(string appName);

        void DeleteTerrainModel();

        void DefineCurveTangentReactor();

        void InitializeReactors();
    }
}
