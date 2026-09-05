using Avalonia;
using EQTool.Services.Map;
using System;

namespace EQTool.Avalonia.Controls
{
    // Turns EverQuest map coordinates into screen coordinates.
    //
    // EQ's axes are not screen axes. Upstream's MapViewModelService positions
    // everything as -(location.Y) horizontally and -(location.X) vertically
    // (MapViewModelService.cs:61-62), so EQ's Y is the screen X axis, EQ's X is
    // the screen Y axis, and both are negated.
    //
    // This is separated from the control and pinned by tests because getting it
    // wrong does not look wrong: the map still renders as a coherent map, just
    // mirrored and rotated, and nothing about it reads as a defect.
    public static class EqMapProjection
    {
        public static Point ToScreenSpace(double eqX, double eqY)
        {
            return new Point(-eqY, -eqX);
        }

        // MapLoad has already shifted geometry so it starts at the origin, and
        // records what it subtracted on ParsedData.Offset. A player position
        // arrives raw from the log, so it needs the same shift before it lines up.
        public static Point ProjectPlayer(Point3DLike location, Point3DLike offset)
        {
            return ToScreenSpace(location.X - offset.X, location.Y - offset.Y);
        }

        public static Rect ComputeTransformedBounds(ParsedData data)
        {
            if (data == null || data.Lines == null || data.Lines.Count == 0)
                return default;

            var minimumX = double.MaxValue;
            var minimumY = double.MaxValue;
            var maximumX = double.MinValue;
            var maximumY = double.MinValue;

            foreach (var line in data.Lines)
            {
                if (line.Points == null)
                    continue;

                foreach (var point in line.Points)
                {
                    var screenPoint = ToScreenSpace(point.X, point.Y);
                    minimumX = Math.Min(minimumX, screenPoint.X);
                    minimumY = Math.Min(minimumY, screenPoint.Y);
                    maximumX = Math.Max(maximumX, screenPoint.X);
                    maximumY = Math.Max(maximumY, screenPoint.Y);
                }
            }

            if (minimumX > maximumX)
                return default;

            return new Rect(minimumX, minimumY, maximumX - minimumX, maximumY - minimumY);
        }

        // A plain carrier so the projection does not drag the WPF Point3D shim into
        // its own signature.
        public struct Point3DLike
        {
            public Point3DLike(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }
        }
    }
}
