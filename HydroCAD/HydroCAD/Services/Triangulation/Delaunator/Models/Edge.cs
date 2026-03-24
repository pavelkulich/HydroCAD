using System;

namespace DelaunatorSharp
{
    public struct Edge : IEdge
    {
        public IPoint P { get; set; }
        public IPoint Q { get; set; }
        public int Index { get; set; }

        public Edge(int e, IPoint p, IPoint q)
        {
            Index = e;
            P = p;
            Q = q;
        }

        // constrained extension
        public override bool Equals(object obj)
        {
            if (!(obj is Edge))
                return false;

            Edge other = (Edge)obj;
            return other.P.Equals(P) && other.Q.Equals(Q);  // for queue contains (index not considered)
        }

        public override int GetHashCode()
        {
            return P.GetHashCode() + Q.GetHashCode();
        }
    }
}
