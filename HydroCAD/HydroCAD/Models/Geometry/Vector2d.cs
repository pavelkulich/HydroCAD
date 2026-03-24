using System;

namespace HydroCAD.Models.Geometry
{
    public struct Vector2d
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Length => Math.Sqrt(X * X + Y * Y);

        public double Angle
        {
            get
            {
                double angle = Math.Atan2(Y, X);
                if (angle < 0) angle += 2 * Math.PI;
                return angle;
            }
        }

        public Vector2d Normalize()
        {
            double l = Length;
            if (l < 1e-12) return new Vector2d(0.0, 0.0);
            return new Vector2d(X / l, Y / l);
        }

        public double DotProduct(Vector2d other) => X * other.X + Y * other.Y;

        public double CrossProduct(Vector2d other) => X * other.Y - Y * other.X;

        public static Vector2d operator +(Vector2d a, Vector2d b) => new Vector2d(a.X + b.X, a.Y + b.Y);
        public static Vector2d operator -(Vector2d a, Vector2d b) => new Vector2d(a.X - b.X, a.Y - b.Y);
        public static Vector2d operator -(Vector2d a) => new Vector2d(-a.X, -a.Y);
        public static Vector2d operator *(Vector2d v, double s) => new Vector2d(v.X * s, v.Y * s);
        public static Vector2d operator *(double s, Vector2d v) => new Vector2d(v.X * s, v.Y * s);
        public static Vector2d operator /(Vector2d v, double s) => new Vector2d(v.X / s, v.Y / s);

        public override string ToString() => $"({X},{Y})";
    }
}
