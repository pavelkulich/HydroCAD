#if ACAD
    using _AcAp = Autodesk.AutoCAD.ApplicationServices;
    using _AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BCAD
    using _AcAp = Bricscad.ApplicationServices;
    using _AcDb = Teigha.DatabaseServices;
#elif GCAD
    using _AcAp = Gssoft.Gscad.ApplicationServices;
    using _AcDb = Gssoft.Gscad.DatabaseServices;
#elif ZCAD
    using _AcAp = ZwSoft.ZwCAD.ApplicationServices;
    using _AcDb = ZwSoft.ZwCAD.DatabaseServices;
#endif

using System;
using System.Collections.Generic;
using System.Linq;

using RailCAD.Common;
using RailCAD.MainApp;
using RailCAD.Models.Alignment;
using RailCAD.Models.Geometry;

namespace RailCAD.CadInterface.Tools
{
	/// <summary>
	/// CAD-dependent part of tram profile class
	/// </summary>
	internal static class TramProfile
    {
        private const double TOL = TramProfileGenerator.TOL;  // Basic precision for equality testing

        #region Main Methods

        internal static string GenerateTramProfile(_AcDb.Arc arc, _AcDb.Transaction tr, _AcDb.Database db, bool update = false)
        {
            if (arc == null || tr == null) return null;

            // Determine curve tangents configuration
            var curves = DetermineCurvesConfig(arc, tr, db, out Dictionary<string, _AcDb.Arc> arcs);
            var allPolylines = FindExistingProfiles(arcs, tr, db);
            if (update == true && Utilities.IsEmpty(allPolylines)) return null;  // only update if tram profile already exists
            var allProfiles = GetPolylinesGeometries(allPolylines);

            // Register XData application
            XDataTools.AddRegAppTableRecord(XDataAppNames.RC_TRAMPROFIL, tr, db);

			// Generate profiles using universal method
			allProfiles = TramProfileGenerator.GenerateTramProfiles(curves, allProfiles);

            // Add profiles to drawing
            var modelSpace = tr.GetObject(db.CurrentSpaceId, _AcDb.OpenMode.ForWrite) as _AcDb.BlockTableRecord;

            foreach (var profiles in allProfiles)
            {
                var polylines = allPolylines[profiles.Key];
				for (int i = 0; i < 2; i++)
                {
                    var profile = profiles.Value[i];
					var polyline = polylines[i];
                    polyline = LispCadExtensions.GetPolylineFromGeometry(profile, polyline);
                    if (polyline.ObjectId == _AcDb.ObjectId.Null)
                    {
                        modelSpace.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                    polylines[i] = polyline;
                }
                // Add XData
                var curve = arcs[profiles.Key];
                CadXDataWriter.AddXDataToProfiles(curve, polylines);
            }

            tr.Commit();
            if (update == false)
                return curves.Count > 1 ?
                    Properties.Resources.TramProfile_InfoTramProfilesWereCreatedForCompoundArc :
                    String.Format(Properties.Resources.TramProfile_InfoTramProfilesWereCreatedForArc, arc.Radius.ToString("F1"));
            else
                return null;
        }

        /// <summary>
        /// Determines curve configs for all arcs inside the compound curve
        /// Returns dictionary of arcs (CAD entities) and sorted list of curves (RCCurve objects) inside a compound curve.
        /// </summary>
        private static List<RCCurve> DetermineCurvesConfig(_AcDb.Arc arc, _AcDb.Transaction tr, _AcDb.Database db, out Dictionary<string, _AcDb.Arc> arcs)
        {
            List<RCCurve> curves = new List<RCCurve>();
            arcs = new Dictionary<string, _AcDb.Arc>();
			arcs[arc.Handle.ToString()] = arc;
            bool reverse = false;

			RCCurve curve = DetermineCurveConfig(arc, arcs, tr, db); // current arc
			curves.Add(curve);
            string startHandle = curve.Tangent1.Handle;
            string endHandle = curve.Tangent2.Handle;
            _AcDb.Arc startArc = arcs.ContainsKey(startHandle) ? arcs[startHandle] : null;
            while (startArc != null)
            {
				RCCurve startCurve = DetermineCurveConfig(startArc, arcs, tr, db); // previous arc
				curves.Insert(0, startCurve);
				startHandle = startCurve.Tangent1.Handle;
				startArc = arcs.ContainsKey(startHandle) ? arcs[startHandle] : null;
                curve.Tangent1.SetTangentCurve(startCurve);
                curve = startCurve;
			}
            _AcDb.Arc endArc = arcs.ContainsKey(endHandle) ? arcs[endHandle] : null;
			while (endArc != null)
            {
				RCCurve endCurve = DetermineCurveConfig(endArc, arcs, tr, db); // next arc
				curves.Add(endCurve);
				endHandle = endCurve.Tangent2.Handle;
				endArc = arcs.ContainsKey(endHandle) ? arcs[endHandle] : null;
                curve.Tangent2.SetTangentCurve(endCurve);
                curve = endCurve;
            }

            // check if the compound curve is counterclockwise
            if (GeometryHelper.Orientation(curves[0].Tangent1.EndPoint.ToPoint2d(), curves[0].Arc.MiddlePoint, curves[0].Tangent2.EndPoint.ToPoint2d()) == 1)
            {
                reverse = true;
            }

            if (reverse)  // switch curves to counterclockwise direction
            {
                foreach (RCCurve curvex in curves)
                {
                    curvex.ReverseTangents();
                }
                curves.Reverse();
            }

            return curves;
        }

        /// <summary>
        /// Determines curve config - finds neighboring arcs (from a compound curve) and spirals.
        /// Returns curve data as RCCurve object with tangents and spirals.
        /// </summary>
        private static RCCurve DetermineCurveConfig(_AcDb.Arc arc, Dictionary<string, _AcDb.Arc> arcs, _AcDb.Transaction tr, _AcDb.Database db)
        {
            // Check minimum radius
            if (arc.Radius < TramProfileData.MinRadius)
                throw new InvalidOperationException(String.Format(Properties.Resources.TramProfile_ErrorArcRadiusIsSmallerThanMin,
                    arc.Radius.ToString("F1"), TramProfileData.MinRadius));

            _AcDb.Polyline spiral1 = null;
            _AcDb.Polyline spiral2 = null;
            _AcDb.Arc arc1 = null;
            _AcDb.Arc arc2 = null;

            var curve = CadXDataReader.ReadCurve(arc, tr, db);
            if (curve != null)
            {
                curve.Arc = LispCadExtensions.GetArcGeometry(arc);

                var radius1 = curve.Tangent1.Radius;
                var radius2 = curve.Tangent2.Radius;
                var curveHandle1 = curve.Tangent1.Handle;
                var curveHandle2 = curve.Tangent2.Handle;
                arc1 = radius1 > TOL ? CadModel.GetEntityByHandle(curveHandle1, tr, db, true) as _AcDb.Arc : null;
                arc2 = radius2 > TOL ? CadModel.GetEntityByHandle(curveHandle2, tr, db, true) as _AcDb.Arc : null;
				curve.Tangent1.Arc = LispCadExtensions.GetArcGeometry(arc1);
				curve.Tangent2.Arc = LispCadExtensions.GetArcGeometry(arc2);
                if (arc1 != null) arcs[arc1.Handle.ToString()] = arc1;
				if (arc2 != null) arcs[arc2.Handle.ToString()] = arc2;

                var spiralHandle1 = curve.Spiral1.Handle;
                var spiralHandle2 = curve.Spiral2.Handle;
                spiral1 = CadModel.GetEntityByHandle(spiralHandle1, tr, db, false) as _AcDb.Polyline;
                spiral2 = CadModel.GetEntityByHandle(spiralHandle2, tr, db, false) as _AcDb.Polyline;
                if (spiral1 != null && spiral1.StartPoint.ToPoint2d().DistanceTo(curve.Tangent2.EndPoint.ToPoint2d()) <
                    spiral1.StartPoint.ToPoint2d().DistanceTo(curve.Tangent1.EndPoint.ToPoint2d()) ||
                    spiral2 != null && spiral2.StartPoint.ToPoint2d().DistanceTo(curve.Tangent1.EndPoint.ToPoint2d()) <
                    spiral2.StartPoint.ToPoint2d().DistanceTo(curve.Tangent2.EndPoint.ToPoint2d()))
                {
                    (spiral1, spiral2) = (spiral2, spiral1);  // exchange spiral entities to match tangents
                }
                curve.Spiral1.Polyline = LispCadExtensions.GetPolylineGeometry(spiral1, false);
				curve.Spiral2.Polyline = LispCadExtensions.GetPolylineGeometry(spiral2, false);
			}
            else
            {
                curve = new RCCurve(arc.Handle.ToString());
                curve.Arc = LispCadExtensions.GetArcGeometry(arc);
            }
            return curve;
        }

        /// <summary>
        /// Try to get existing outline polylines linked to the arc via XData.
        /// </summary>
        private static Dictionary<string, _AcDb.Polyline[]> FindExistingProfiles(Dictionary<string, _AcDb.Arc> arcs, _AcDb.Transaction tr, _AcDb.Database db)
        {
            if (arcs == null || arcs.Count == 0) return null;
            var result = new Dictionary<string, _AcDb.Polyline[]>();

            foreach (var arc in arcs)
            {
                var pair = new _AcDb.Polyline[2];
                // Read arc XData
                var xdata = arc.Value.GetXDataForApplication(XDataAppNames.RC_TRAMPROFIL);
                if (xdata != null)
                {
                    var values = xdata.AsArray();
                    // values are: RegAppName, "OBLOUK", handleInner, handleOuter, innerWidening, outerWidening

                    for (int i = 2; i <= 3; i++) // indexes of polyline handles
                    {
                        if (values[i].TypeCode == (int)_AcDb.DxfCode.ExtendedDataHandle && values[i].Value is string handle)
                        {
                            try
                            {
                                var ent = CadModel.GetEntityByHandle(handle, tr, db, true) as _AcDb.Polyline;
                                if (ent != null)
                                    pair[i - 2] = ent;
                            }
                            catch
                            {
                                // Entity does not exist - ignore
                            }
                        }
                    }
                }
                result[arc.Key] = pair;
            }

            return result;
        }

        private static Dictionary<string, RCPolyline[]> GetPolylinesGeometries(Dictionary<string, _AcDb.Polyline[]> allPolylines)
        {
            Dictionary<string, RCPolyline[]> allGeometries = new Dictionary<string, RCPolyline[]>(allPolylines.Count);

			foreach (var pairs in allPolylines)
            {
                string key = pairs.Key;
                var polylines = pairs.Value;
                var geometries = new RCPolyline[2];

                for (int i = 0; i < 2; i++)
                {
                    geometries[i] = LispCadExtensions.GetPolylineGeometry(polylines[i], true);
                }
                allGeometries[key] = geometries;
            }
            return allGeometries;
        }

        #endregion
    }
}