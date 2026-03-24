#if ACAD
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using System.Collections.Generic;
using HydroCAD.MainApp;
using HydroCAD.Models.Geometry;
using HydroCAD.Models.Network;
using HydroCAD.Models.TerrainModel;
using HydroCAD.Profile;

namespace HydroCAD.CadInterface
{
    internal interface ICadModel
    {
        // ── Messaging ────────────────────────────────────────────────────
        void WriteMessage(object message);
        void WriteMessageNoDebug(object message);

        // ── User interaction ─────────────────────────────────────────────
        bool PromptUserSettings(out UserSettings settings);
        bool PromptProfileSettings(out ProfileSettings settings);

        // ── Lisp I/O ────────────────────────────────────────────────────
        _AcDb.ResultBuffer GetLispInputArgs();
        void SetLispResp<T>(System.Func<T, _AcDb.ResultBuffer> writeRespFunction, T resp);

        // ── Point operations ─────────────────────────────────────────────
        void ImportPoints();
        IList<HCPoint> SelectPoints(string message = "");
        IList<HCPoint> SelectAllPoints();
        void SetPointsHeight(IList<HCPoint> points, TerrainModel terrainModel);

        // ── Segment / polyline operations ────────────────────────────────
        IList<HCLine> SelectSegments(IDictionary<string, HCPoint> handleMap, bool requireXData = false);
        HCPolyline SelectPolyline(string message = "");

        // ── Manhole operations ───────────────────────────────────────────
        void SelectManholes(HCPipeRoute route, string message = "");

        // ── Terrain model ────────────────────────────────────────────────
        void WriteTriangulation(TerrainModel terrainModel, bool showTriangles);
        void SaveTerrainModel(TerrainModel terrainModel);
        TerrainModel LoadTerrainModel(string appName);
        TerrainModel ReadTerrainModel(string appName);
        void DeleteTerrainModel();

        // ── Profile drawing ──────────────────────────────────────────────
        void DrawLongitudinalProfile(ProfileData profileData, ProfileSettings settings);
    }
}
