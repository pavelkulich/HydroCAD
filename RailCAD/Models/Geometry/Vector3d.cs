using System;
using RailCAD.CadInterface.Tools;
using RailCAD.Common;

namespace RailCAD.Models.Geometry
{
    /// <summary>
    /// Simple 3D vector helper type used by geometry routines.
    /// </summary>
    public struct Vector3d : IRCEntity
    {
        public Vector3d(double x, double y, double z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }

        public double X { get; private set; }

        public double Y { get; private set; }

        public double Z { get; private set; }

        public Vector3d CrossProduct(Vector3d other)
        {
            double x, y, z;
            x = this.Y * other.Z - other.Y * this.Z;
            y = (this.X * other.Z - other.X * this.Z) * -1;
            z = this.X * other.Y - other.X * this.Y;

            return new Vector3d(x, y, z);
        }

        public Vector3d Normalized()
        {
            double length = Math.Sqrt(X * X + Y * Y + Z * Z);
            return new Vector3d(X / length, Y / length, Z / length);
        }

        public Vector3d TryOrientUp()
        {
            // ad hoc method for orienting vectors up (positive Z)
            // not needed when orientation of triangle points is fixed
            if (Z < 0)
            {
                return new Vector3d(-X, -Y, -Z);
            }
            else
            {
                return this;
            }
        }

        public override string ToString()
        {
            return $"({X};{Y};{Z})";
        }

        public object WriteToXData()
        {
            return this.WriteXData();
        }

        public static Vector3d operator *(Vector3d vec, double mult)
        {
            return new Vector3d(vec.X * mult, vec.Y * mult, vec.Z * mult);
        }

        public bool IsEqual(Vector3d other, double tol = 1e-6)
        {
            return
                GeometryHelper.EqualsWithTol(this.X, other.X, tol) &&
                GeometryHelper.EqualsWithTol(this.Y, other.Y, tol) &&
                GeometryHelper.EqualsWithTol(this.Z, other.Z, tol);
        }

        public double AngleXY(Vector3d other)
        {
            // angle is calculated as a counterclockwise positive value between this and other vector
            // https://www.csharphelper.com/howtos/howto_vector_angle.html
            // returns angle between 0 to 2PI radians
            //     0.5PI
            //       ^
            //       |
            // PI <--+--> 0 (== this)
            //       |
            //       v
            //     1.5PI
            if (this.IsEqual(other))
            {
                return 0;
            }

            double len1 = Math.Sqrt(this.X * this.X + this.Y * this.Y);
            double len2 = Math.Sqrt(other.X * other.X + other.Y * other.Y);

            double cos = (this.X * other.X + this.Y * other.Y) / (len1 * len2);
            double sin = (this.X * other.Y - this.Y * other.X) / (len1 * len2);

            // fix for numerical errors
            if (GeometryHelper.EqualsWithTol(cos, -1))
            {
                cos = -1;
            }
            else if (GeometryHelper.EqualsWithTol(cos, 1))
            {
                cos = 1;
            }

            double angle = Math.Acos(cos);
            if (sin < 0)
            {
                angle = (2 * Math.PI) - angle;
            }
            return angle;
        }

        public double AngleOnXY()
        {
            // Atan2 returns angle on interval (-π, π]
            double angle = Math.Atan2(this.Y, this.X);

            // If the angle is negative, moves it to interval <0, 2π>
            if (angle < 0)
                angle += 2 * Math.PI;

            return angle;
        }

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }
}
