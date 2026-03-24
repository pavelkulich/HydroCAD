using System;
using RailCAD.CadInterface.Tools;

namespace RailCAD.Models.Geometry
{
    public struct Point3d : IRCEntity
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool IsEqualTo(Point3d other, double tolerance)
        {
            return Math.Abs(X - other.X) < tolerance && Math.Abs(Y - other.Y) < tolerance && Math.Abs(Z - other.Z) < tolerance;
        }

        public double DistanceTo(Point3d other)
        {
            return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z));
        }

        public override string ToString() => $"{X},{Y},{Z}";

        public object WriteToXData()
        {
            return this.WriteXData();
        }

        /// <summary>
        /// Convert Point3d to Point2d
        /// </summary>
        public Point2d ToPoint2d()
        {
            return new Point2d(X, Y);
        }

        /// <summary>
        /// Construct vector from this point to other point.
        /// </summary>
        public Vector3d VectorTo(Point3d other)
        {
            return new Vector3d(other.X - X, other.Y - Y, other.Z - Z);
        }
    }
}
