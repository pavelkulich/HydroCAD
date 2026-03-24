using System;

using RailCAD.Models.Geometry;

namespace RailCAD.Models.Alignment
{
    /// <summary>
    /// RailCAD tangent class (tangent of a horizontal curve).
    /// </summary>
    public class RCTangent
    {
        public string Handle = ""; // tangent entity (line or arc)
        public RCArc Arc; // tangent arc geometry
        public Point3d Point1; // start point of a line/ selected point on a curve
        public Point3d Point2; // end point of a line/ center point of a curve
        public double Radius; // > 0 for curve/ 0 for line
        public double Cant; // superelevation of tangent curve [mm]
        public RCCurve Curve; // main curve entity
        public double GaugeRun; // length of gauge extension run [m]
        public Point3d EndPoint; // start/ end point of a curve (from routing data)
        public Vector3d Direction; // direction vector to apex point of the curve (from routing data)
        public double Length; // length of tangent (from routing data)
        public RCCurve TangentCurve { get; private set; } // tangent curve

        /// <summary>
        /// Creates link between neighbor (tangent) curve and current curve
        /// </summary>
        public void SetTangentCurve(RCCurve neighborCurve)
        {
            TangentCurve = neighborCurve;

            // save link to current curve
            if (Curve.Tangent1 == this) 
            {
                neighborCurve.Tangent2.TangentCurve = Curve;

                // exchange intermediate spirals
                if (Curve.Spiral1.Polyline == null && neighborCurve.Spiral2.Polyline != null)
                {
                    Curve.Spiral1 = neighborCurve.Spiral2;
                }
                else if (neighborCurve.Spiral2.Polyline == null && Curve.Spiral1.Polyline != null)
                {
                    neighborCurve.Spiral2 = Curve.Spiral1;
                }
            }
            else
            {
                neighborCurve.Tangent1.TangentCurve = Curve;

                // exchange intermediate spirals
                if (Curve.Spiral2.Polyline == null && neighborCurve.Spiral1.Polyline != null) 
                {
                    Curve.Spiral2 = neighborCurve.Spiral1;
                }
                else if (neighborCurve.Spiral1.Polyline == null && Curve.Spiral2.Polyline != null)
                {
                    neighborCurve.Spiral1 = Curve.Spiral2;
                }
            }
        }

        /// <summary>
        /// Constructor of the horizontal curve tangent object.
        /// </summary>
        /// <param name="curve">Horizontal curve object (parent) which has 2 tangents</param>
        public RCTangent(RCCurve curve)
        {
            this.Curve = curve;
        }
    }
}
