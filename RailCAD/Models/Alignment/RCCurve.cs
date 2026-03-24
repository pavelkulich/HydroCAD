using System;
using System.Collections.Generic;
using System.Linq;

using RailCAD.Models.Geometry;

namespace RailCAD.Models.Alignment
{
    /// <summary>
    /// RailCAD horizontal curve class (a segment of one or more axes).
    /// </summary>
    public class RCCurve
    {
        public string Handle = ""; // main curve entity (arc)
        public RCArc Arc; // arc geometry (circular part of a curve)
        public RCTangent Tangent1; // tangent (linear or curve)
        public RCTangent Tangent2;
        public RCSpiral Spiral1; // transition curve (eg clothoid)
        public RCSpiral Spiral2;
        public double Radius; // in [m]
        public IList<double> Speeds; // 5 speeds (usualy named V, V130, V150, Vk, Vn) in [km/h]
        public double Cant; // superelevation in [mm]
        public string Turn; // curve with angle greater than pi (tocka)
        public IList<string> Circles = new List<string>(); // 2 or 4 circles
        public IList<string> Label = new List<string>(); // 1 or more texts
        public IList<string> Stationing = new List<string>(); // 0 to 4 blocks or lines & texts (old format)
        public double GaugeWidening; // in [mm]
        public string Number; // user number
        public double Length; // total length of a curve in [m]
        public double ArcLength; // lenght of the circular part of a curve in [m]
        public double Angle; // total angle of a curve in [rad]
        public double ArcAngle; // angle of the circular part of a curve in [rad]

        public RCCurve(string handle)
        {
            Handle = handle;
            Tangent1 = new RCTangent(this);
            Tangent2 = new RCTangent(this);
            Spiral1 = new RCSpiral(this);
            Spiral2 = new RCSpiral(this);
        }

        /// <summary>
        /// Constructor of horizontal curve object.
        /// </summary>
        /// <param name="handle">Handle of main curve entity (arc)</param>
        /// <param name="tang1">1st tangent object</param>
        /// <param name="tang2">2nd tangent object</param>
        /// <param name="spiral1">1st transition curve object</param>
        /// <param name="spiral2">2nd transition curve object</param>
        /// <param name="radius">Radius of the circular part of curve</param>
        /// <param name="cant">Superelevation of curve</param>
        /// <param name="turn">Turning curve with angle greater than pi (yes "1", no "0")</param>
        public RCCurve(string handle, RCTangent tang1, RCTangent tang2, RCSpiral spiral1, RCSpiral spiral2, double radius, double cant, string turn)
        {
            Handle = handle;
            Tangent1 = tang1;
            Tangent2 = tang2;
            Spiral1 = spiral1;
            Spiral2 = spiral2;
            Radius = radius;
            Cant = cant;
            Turn = turn;
            tang1.Curve = this;
            tang2.Curve = this;
            spiral1.Curve = this;
            spiral2.Curve = this;
        }

        /// <summary>
        /// Switches both tangents and spirals.
        /// </summary>
        public void ReverseTangents()
        {
            (Tangent1, Tangent2) = (Tangent2, Tangent1);
            (Spiral1, Spiral2) = (Spiral2, Spiral1);
        }

        /// <summary>
        /// Returns an array of associated curve entities.
        /// </summary>
        public IList<string> GetEntities()
        {
            List<string> ents = new List<string>();
            if (Circles != null)
            {
                ents.AddRange(Circles);
            }
            if (Label != null)
            {
                ents.AddRange(Label);
            }
            if (Stationing != null)
            {
                ents.AddRange(Stationing);
            }
            return ents;
        }

        public static IDictionary<string, RCCurve> GetCollection(IList<RCCurve> curves)
        {
            if (curves == null && curves.Count == 0)
            {
                return null;
            }
            IDictionary<string, RCCurve> dict = curves.ToDictionary(p => p.Handle);
            return dict;
        }

        /// <summary>
        /// Finds curve (object) by its main entity handle in a list of curves.
        /// </summary>
        public static RCCurve GetCurveByHandle(IList<RCCurve> curves, string ent)
        {
            if (curves == null && curves.Count == 0)
            {
                return null;
            }
            IDictionary<string, RCCurve> dict = curves.ToDictionary(p => p.Handle);
            return dict[ent];
        }
    }
}
