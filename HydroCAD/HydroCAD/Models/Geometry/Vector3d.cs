using System;
using HydroCAD.Common;

namespace HydroCAD.Models.Geometry
{
    public struct Vector3d
    {
        public Vector3d(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; private set; }
        public double Y { get; private set; }
        public double Z { get; private set; }

        public Vector3d CrossProduct(Vector3d other)
        {
            return new Vector3d(
                Y * other.Z - other.Y * Z,
                (X * other.Z - other.X * Z) * -1,
                X * other.Y - other.X * Y
            );
        }

        public Vector3d Normalized()
        {
            double length = Math.Sqrt(X * X + Y * Y + Z * Z);
            return new Vector3d(X / length, Y / length, Z / length);
        }

        public Vector3d TryOrientUp()
        {
            if (Z < 0) return new Vector3d(-X, -Y, -Z);
            return this;
        }

        public bool IsEqual(Vector3d other, double tol = 1e-6)
        {
            return GeometryHelper.EqualsWithTol(X, other.X, tol) &&
                   GeometryHelper.EqualsWithTol(Y, other.Y, tol) &&
                   GeometryHelper.EqualsWithTol(Z, other.Z, tol);
        }

        public override string ToString() => $"({X};{Y};{Z})";

        public static Vector3d operator *(Vector3d vec, double mult) => new Vector3d(vec.X * mult, vec.Y * mult, vec.Z * mult);
        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }
}
