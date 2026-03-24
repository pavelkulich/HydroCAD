using System;
using RailCAD.Models.Geometry;

namespace RailCAD.Models.Alignment
{
    /// <summary>
    /// RailCAD spiral class (transition curve of a horizontal curve).
    /// </summary>
    public class RCSpiral
    {
        public string Handle = ""; // spiral entity (polyline)
        public RCPolyline Polyline; // polyline geometry
        public String Type; // type of transition curve (1 = clothoid etc.)
        public double Length; // in [m]
        public double Slope; // cant gradient (slope of a superelevation ramp)
        public RCCurve Curve; // main curve entity

        /// <summary>
        /// Constructor of horizontal curve transition curve (spiral) object.
        /// </summary>
        /// <param name="curve">Horizontal curve object (parent) which has 2 spirals</param>
        public RCSpiral(RCCurve curve)
        {
            this.Curve = curve;
        }
    }
}
