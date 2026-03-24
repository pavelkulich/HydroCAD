using RailCAD.CadInterface.Tools;
using System;

namespace RailCAD.Models.Geometry
{
    // todo: unify XData strings (to enums?)
    // RC_SPOJNICE, RC_BOD, RC_SING, SPOJNICE
    //Console.WriteLine(nameof(RC_SPOJNICE));

    [Flags]
    public enum RC_SPOJNICE
    {
        None      = 0,
        TRIANG    = 1 << 0,
        HRANICE   = 1 << 1,
        HRBETNICE = 1 << 2,
        UDOLNICE  = 1 << 3,
        HRANA     = 1 << 4,
        URCENA    = 1 << 5,

        // fixed segments
        POSSIBLE_TYPES_FOR_DEFINITION = TRIANG | HRBETNICE | UDOLNICE | HRANA | URCENA,  // for segment selection and definition
        FIXED_SEGMENT = HRANICE | HRBETNICE | UDOLNICE | HRANA,  // for intermediate point height calculation
        TYPES_TO_DELETE = TRIANG | HRANICE,  // for deleting with the terrain model
        FIXED_SEGMENT_INSIDE = HRBETNICE | UDOLNICE | HRANA,  // for saving into the XRecords
        ALL_TYPES = TRIANG | HRANICE | HRBETNICE | UDOLNICE | HRANA | URCENA,
    }

    public class RCLine : IRCEntity, IEquatable<RCLine>
    {
        public RCLine(RCPoint pt1, RCPoint pt2, RC_SPOJNICE type, string handle = "0")
        {
            this.Pt1 = pt1;
            this.Pt2 = pt2;
            this.Type = type;
            this.Handle = handle;
        }

        public RCPoint Pt1 { get; private set; }

        public RCPoint Pt2 { get; private set; }
        
        public RC_SPOJNICE Type { get; private set; }

        public string Handle { get; private set; }

        public static RCLine CreateLineAutoType(RCPoint pt1, RCPoint pt2, string handle = "0")
        {
            RC_SPOJNICE type = (pt1.IsHranice && pt2.IsHranice) ? RC_SPOJNICE.HRANICE : RC_SPOJNICE.TRIANG;
            return new RCLine(pt1, pt2, type, handle);
        }

        public void LinkPointsToLine()
        {
            // add reference: point -> line
            this.Pt1.Lines.Add(this);
            this.Pt2.Lines.Add(this);
        }

        public bool Equals(RCLine other)
        {
            // precise comparision: without line direction
            return (this.Pt1.Number == other.Pt1.Number && this.Pt2.Number == other.Pt2.Number) ||
                (this.Pt1.Number == other.Pt2.Number && this.Pt2.Number == other.Pt1.Number);

            //return (this.pt1.handle.Equals(other.pt1.handle) && this.pt2.handle.Equals(other.pt2.handle)) ||
            //       (this.pt1.handle.Equals(other.pt2.handle) && this.pt2.handle.Equals(other.pt1.handle));
        }

        public override int GetHashCode()
        {
            // fast comparision
            //return Pt1.Number + Pt2.Number;
            //return (int)(pt1.X + pt2.X);
            int min = Math.Min(Pt1.Number, Pt2.Number);
            int max = Math.Max(Pt1.Number, Pt2.Number);
            return HashCode.Combine(min, max);
        }

        /// <summary>
        /// Creates a normalized lookup key from two point numbers.
        /// The key is order-independent — MakeLineKey(a, b) == MakeLineKey(b, a).
        /// </summary>
        internal long MakeLineKey()
        {
            int min = Math.Min(Pt1.Number, Pt2.Number);
            int max = Math.Max(Pt1.Number, Pt2.Number);
            return ((long)min << 32) | (uint)max;
        }

        /// <summary>
        /// Creates a normalized lookup key from two point indices.
        /// The key is order-independent — MakeLineKey(a, b) == MakeLineKey(b, a).
        /// </summary>
        internal static long MakeLineKey(int i1, int i2)
        {
            return i1 < i2
                ? ((long)i1 << 32) | (uint)i2
                : ((long)i2 << 32) | (uint)i1;
        }

        public override string ToString()
        {
            return $"{Pt1.Number}-{Pt2.Number} ({Type})";
        }

        public object WriteToXData()
        {
            return this.WriteXData();
        }

        /// <summary>
        /// Returns a point that is not equal to the tested point.
        /// </summary>
        public RCPoint OtherPoint(RCPoint pt)
        {
            if (pt == Pt1)
            {
                return Pt2;
            }
            return Pt1;
        }
    }
}
