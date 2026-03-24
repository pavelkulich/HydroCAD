using System;

namespace HydroCAD.Models.Geometry
{
    public struct Point2d
    {
        public double X { get; }
        public double Y { get; }

        public Point2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2d other)
        {
            return Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
        }

        public bool IsEqualTo(Point2d other, double tolerance)
        {
            return Math.Abs(X - other.X) < tolerance && Math.Abs(Y - other.Y) < tolerance;
        }

        public override string ToString() => $"{X},{Y}";

        public Point3d ToPoint3d(double z = 0)
        {
            return new Point3d(X, Y, z);
        }

        public Vector2d VectorTo(Point2d other)
        {
            return new Vector2d(other.X - X, other.Y - Y);
        }

        public double AngleTo(Point2d other)
        {
            Vector2d vec = new Vector2d(other.X - X, other.Y - Y);
            return vec.Angle;
        }
    }
}
