using HydroCAD.Models.Geometry;

namespace HydroCAD.Profile
{
    /// <summary>
    /// Data at a single station (chainage) along a longitudinal profile.
    /// </summary>
    public class ProfileStation
    {
        public ProfileStation(double chainage, Point2d planPosition)
        {
            Chainage = chainage;
            PlanPosition = planPosition;
        }

        /// <summary>Distance from the start of the route (m).</summary>
        public double Chainage { get; set; }

        /// <summary>2D plan-view position.</summary>
        public Point2d PlanPosition { get; set; }

        /// <summary>Ground/terrain elevation at this station (m). NaN if outside DTM.</summary>
        public double GroundLevel { get; set; } = double.NaN;

        /// <summary>Pipe invert elevation at this station (m).</summary>
        public double InvertLevel { get; set; } = double.NaN;

        /// <summary>Pipe crown elevation = invert + pipe OD (m).</summary>
        public double CrownLevel => double.IsNaN(InvertLevel) || double.IsNaN(PipeDiameter)
                                    ? double.NaN
                                    : InvertLevel + PipeDiameter / 1000.0;

        /// <summary>Cover depth = ground - crown (m). Positive = buried.</summary>
        public double CoverDepth => (!double.IsNaN(GroundLevel) && !double.IsNaN(CrownLevel))
                                    ? GroundLevel - CrownLevel
                                    : double.NaN;

        /// <summary>Pipe internal diameter at this station (mm).</summary>
        public double PipeDiameter { get; set; } = double.NaN;

        /// <summary>True if a manhole is located at this station.</summary>
        public bool IsManhole { get; set; }

        /// <summary>Label for the manhole (if any).</summary>
        public string ManholeId { get; set; }

        public override string ToString() =>
            $"Ch={Chainage:F2}m GL={GroundLevel:F3} IL={InvertLevel:F3} Cover={CoverDepth:F3}";
    }
}
