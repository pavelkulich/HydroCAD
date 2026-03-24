using System;

namespace RailCAD.Models.Geometry
{
    /// <summary>
    /// Simple 2D vector helper type used by geometry routines.
    /// </summary>
    public struct Vector2d
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Vector2d(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Length (magnitude).</summary>
        public double Length => Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// Calculates the angle (in radians) between two points relative to the X-axis.
        /// The result is normalized to the range 0 – 2π.
        /// </summary>
        public double Angle
        {
            get
            {
                double angle = Math.Atan2(Y, X);

                if (angle < 0)
                    angle += 2 * Math.PI;

                return angle;
            }
        }

        /// <summary>Return normalized vector (zero vector stays zero).</summary>
        public Vector2d Normalize()
        {
            double l = Length;
            if (l < 1e-12) return new Vector2d(0.0, 0.0);
            return new Vector2d(X / l, Y / l);
        }

        /// <summary>Dot product.</summary>
        public double DotProduct(Vector2d other) => X * other.X + Y * other.Y;

        /// <summary>Cross product (z-component) in 2D.</summary>
        public double CrossProduct(Vector2d other) => X * other.Y - Y * other.X;

        public static Vector2d operator +(Vector2d a, Vector2d b) => new Vector2d(a.X + b.X, a.Y + b.Y);
        public static Vector2d operator -(Vector2d a, Vector2d b) => new Vector2d(a.X - b.X, a.Y - b.Y);
        public static Vector2d operator -(Vector2d a) => new Vector2d(-a.X, -a.Y);
        public static Vector2d operator *(Vector2d v, double s) => new Vector2d(v.X * s, v.Y * s);
        public static Vector2d operator *(double s, Vector2d v) => new Vector2d(v.X * s, v.Y * s);
        public static Vector2d operator /(Vector2d v, double s) => new Vector2d(v.X / s, v.Y / s);

        public override string ToString() => $"({X},{Y})";
        
        /// <summary>
        /// Calculates the angle between two vectors in radians.
        /// Result is in range 0 to π.
        /// </summary>
        /// <returns>Angle between vectors in radians (0 to π)</returns>
        public double AngleBetween(Vector2d other)
        {
            double dot = this.DotProduct(other);
            double lengthProduct = this.Length * other.Length;

            // Protect against division by zero
            if (lengthProduct < 1e-12)
                return 0.0;

            // Ensure value is in range [-1, 1] due to numerical errors
            double cosAngle = Math.Max(-1.0, Math.Min(1.0, dot / lengthProduct));

            return Math.Acos(cosAngle);
        }

        /// <summary>
        /// Calculates oriented angle from this to other vector. Positive angle is counter-clockwise.
        /// </summary>
        /// <returns>Oriented angle (-π, π)</returns>
        public double SignedAngleTo(Vector2d other)
        {
            double dot = this.DotProduct(other);
            double cross = this.CrossProduct(other);

            return Math.Atan2(cross, dot);
        }

        /// <summary>
        /// Calculates angle from this to other vector measured counter-clockwise.
        /// </summary>
        /// <returns>Angle (0, 2π)</returns>
        public double AngleTo(Vector2d other)
        {
            double dot = this.DotProduct(other);
            double cross = this.CrossProduct(other);

            double angle = Math.Atan2(cross, dot);

            // Normalize to range 0 to 2π
            if (angle < 0)
                angle += 2 * Math.PI;

            return angle;
        }

        /// <summary>
        /// Determines which of two vectors is angularly closer to this vector.
        /// </summary>
        /// <returns>Returns closer vector from the two.</returns>
        public Vector2d GetCloserVector(Vector2d vector1, Vector2d vector2)
        {
            double angle1 = this.AngleBetween(vector1);
            double angle2 = this.AngleBetween(vector2);

            return angle1 <= angle2 ? vector1 : vector2;
        }
    }
}
