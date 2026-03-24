using HydroCAD.Models.Geometry;

namespace HydroCAD.Models.Network
{
    /// <summary>
    /// Represents a manhole, inspection chamber, or node point on the pipe network.
    /// </summary>
    public class HCManhole
    {
        public HCManhole(string id, Point2d position, double rimLevel = double.NaN,
                         double invertLevel = double.NaN, string handle = "0")
        {
            Id = id;
            Position = position;
            RimLevel = rimLevel;       // top of cover (ground level)
            InvertLevel = invertLevel; // lowest invert at this manhole
            Handle = handle;
        }

        /// <summary>User-assigned ID label (e.g., "MH-01").</summary>
        public string Id { get; set; }

        /// <summary>2D plan position.</summary>
        public Point2d Position { get; set; }

        /// <summary>Cover/rim elevation (m). NaN = use terrain model value.</summary>
        public double RimLevel { get; set; }

        /// <summary>Invert elevation at the lowest incoming/outgoing pipe.</summary>
        public double InvertLevel { get; set; }

        /// <summary>Depth from rim to invert (m). Calculated when both levels are set.</summary>
        public double Depth => (!double.IsNaN(RimLevel) && !double.IsNaN(InvertLevel))
                               ? RimLevel - InvertLevel
                               : double.NaN;

        /// <summary>CAD entity handle (if placed in drawing).</summary>
        public string Handle { get; set; }

        /// <summary>Optional design note or label.</summary>
        public string Note { get; set; }

        public override string ToString() => $"MH {Id} @ ({Position.X:F2},{Position.Y:F2}) RL={RimLevel:F3}";
    }
}
