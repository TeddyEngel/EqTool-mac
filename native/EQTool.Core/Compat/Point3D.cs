// Shim for WPF's System.Windows.Media.Media3D.Point3D so that upstream files
// referencing that namespace can be linked into a non-WPF net9.0 assembly
// without any edits. Declaring a type inside a System.* namespace is legal C#;
// this mirrors the polyfill technique used by e.g. dotnet/runtime.
//
// Upstream usage sites (LocationParser.cs, EventModels.cs) treat Point3D as a
// plain X/Y/Z holder created with an object initializer and, in one case, as
// Point3D? (nullable). It must therefore be a struct with settable properties.

using System;
using System.Globalization;

namespace System.Windows.Media.Media3D
{
    public struct Point3D : IEquatable<Point3D>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Point3D other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Point3D other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + X.GetHashCode();
                hash = (hash * 31) + Y.GetHashCode();
                hash = (hash * 31) + Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Point3D left, Point3D right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Point3D left, Point3D right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2}",
                X,
                Y,
                Z);
        }
    }
}
