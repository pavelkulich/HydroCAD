using System;

namespace HydroCAD.Models.Geometry
{
    [Flags]
    public enum HC_SPOJNICE
    {
        None      = 0,
        TRIANG    = 1 << 0,
        HRANICE   = 1 << 1,   // boundary/hull
        RIDGE     = 1 << 2,   // ridge line
        VALLEY    = 1 << 3,   // valley line
        BREAKLINE = 1 << 4,   // fixed break line
        DEFINED   = 1 << 5,   // user-defined edge

        POSSIBLE_TYPES_FOR_DEFINITION = TRIANG | RIDGE | VALLEY | BREAKLINE | DEFINED,
        FIXED_SEGMENT = HRANICE | RIDGE | VALLEY | BREAKLINE,
        TYPES_TO_DELETE = TRIANG | HRANICE,
        FIXED_SEGMENT_INSIDE = RIDGE | VALLEY | BREAKLINE,
        ALL_TYPES = TRIANG | HRANICE | RIDGE | VALLEY | BREAKLINE | DEFINED,
    }

    public class HCLine : IEquatable<HCLine>
    {
        public HCLine(HCPoint pt1, HCPoint pt2, HC_SPOJNICE type, string handle = "0")
        {
            Pt1 = pt1;
            Pt2 = pt2;
            Type = type;
            Handle = handle;
        }

        public HCPoint Pt1 { get; private set; }
        public HCPoint Pt2 { get; private set; }
        public HC_SPOJNICE Type { get; private set; }
        public string Handle { get; private set; }

        public static HCLine CreateLineAutoType(HCPoint pt1, HCPoint pt2, string handle = "0")
        {
            HC_SPOJNICE type = (pt1.IsHranice && pt2.IsHranice) ? HC_SPOJNICE.HRANICE : HC_SPOJNICE.TRIANG;
            return new HCLine(pt1, pt2, type, handle);
        }

        public void LinkPointsToLine()
        {
            Pt1.Lines.Add(this);
            Pt2.Lines.Add(this);
        }

        public bool Equals(HCLine other)
        {
            return (Pt1.Number == other.Pt1.Number && Pt2.Number == other.Pt2.Number) ||
                   (Pt1.Number == other.Pt2.Number && Pt2.Number == other.Pt1.Number);
        }

        public override int GetHashCode()
        {
            int min = Math.Min(Pt1.Number, Pt2.Number);
            int max = Math.Max(Pt1.Number, Pt2.Number);
            return HashCode.Combine(min, max);
        }

        public HCPoint OtherPoint(HCPoint pt) => pt == Pt1 ? Pt2 : Pt1;

        internal static long MakeLineKey(int i1, int i2)
        {
            return i1 < i2
                ? ((long)i1 << 32) | (uint)i2
                : ((long)i2 << 32) | (uint)i1;
        }

        public override string ToString() => $"{Pt1.Number}-{Pt2.Number} ({Type})";
    }
}
