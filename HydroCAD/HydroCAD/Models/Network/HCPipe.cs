using System;

namespace HydroCAD.Models.Network
{
    /// <summary>
    /// Represents a single pipe segment between two manholes.
    /// </summary>
    public class HCPipe
    {
        public HCPipe(string id, HCManhole startManhole, HCManhole endManhole,
                      double diameter, PipeMaterial material = PipeMaterial.PVC,
                      PipeType pipeType = PipeType.GravitySewer, string handle = "0")
        {
            Id = id;
            StartManhole = startManhole;
            EndManhole = endManhole;
            Diameter = diameter;
            Material = material;
            PipeType = pipeType;
            Handle = handle;
        }

        /// <summary>Pipe segment identifier (e.g., "P-01").</summary>
        public string Id { get; set; }

        public HCManhole StartManhole { get; set; }
        public HCManhole EndManhole { get; set; }

        /// <summary>Internal pipe diameter in millimetres.</summary>
        public double Diameter { get; set; }

        public PipeMaterial Material { get; set; }
        public PipeType PipeType { get; set; }

        /// <summary>CAD polyline handle representing the pipe centreline.</summary>
        public string Handle { get; set; }

        /// <summary>Invert level at the upstream (start) end.</summary>
        public double InvertLevelStart
        {
            get => StartManhole?.InvertLevel ?? double.NaN;
            set { if (StartManhole != null) StartManhole.InvertLevel = value; }
        }

        /// <summary>Invert level at the downstream (end) end.</summary>
        public double InvertLevelEnd
        {
            get => EndManhole?.InvertLevel ?? double.NaN;
            set { if (EndManhole != null) EndManhole.InvertLevel = value; }
        }

        /// <summary>Pipe length in metres (2D plan length).</summary>
        public double Length { get; set; }

        /// <summary>Hydraulic gradient (m/m). Positive means falling from start to end.</summary>
        public double Gradient
        {
            get
            {
                if (Length > 0 && !double.IsNaN(InvertLevelStart) && !double.IsNaN(InvertLevelEnd))
                    return (InvertLevelStart - InvertLevelEnd) / Length;
                return double.NaN;
            }
        }

        /// <summary>Gradient expressed as a percentage.</summary>
        public double GradientPercent => Gradient * 100;

        /// <summary>Crown level = invert + diameter (in metres).</summary>
        public double CrownLevelStart => double.IsNaN(InvertLevelStart) ? double.NaN : InvertLevelStart + Diameter / 1000.0;
        public double CrownLevelEnd => double.IsNaN(InvertLevelEnd) ? double.NaN : InvertLevelEnd + Diameter / 1000.0;

        /// <summary>Optional note/reference.</summary>
        public string Note { get; set; }

        public override string ToString() =>
            $"Pipe {Id}: DN{Diameter} {Material}, L={Length:F2}m, i={GradientPercent:F3}%";
    }
}
