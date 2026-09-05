using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using EQTool.Services.Map;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media.Media3D;

namespace EQTool.Avalonia.Controls
{
    // Renders a parsed EverQuest zone map.
    //
    // EQ map coordinates are not screen coordinates. Upstream's MapViewModelService
    // positions everything as -(location.Y) horizontally and -(location.X)
    // vertically, so EQ's Y is the screen X axis, EQ's X is the screen Y axis, and
    // both are negated. Rendering without that swap still produces something that
    // looks like a map, just mirrored and rotated, so it is worth being explicit.
    //
    // MapLoad has already shifted every point so the geometry starts at the origin,
    // and stores what it subtracted on ParsedData.Offset. Player positions arrive
    // raw from the log, so they need that offset applied before they line up.
    public class ZoneMapControl : Control
    {
        public static readonly StyledProperty<ParsedData> MapDataProperty =
            AvaloniaProperty.Register<ZoneMapControl, ParsedData>(nameof(MapData));

        public static readonly StyledProperty<Point3D?> PlayerLocationProperty =
            AvaloniaProperty.Register<ZoneMapControl, Point3D?>(nameof(PlayerLocation));

        public static readonly StyledProperty<string> PlayerNameProperty =
            AvaloniaProperty.Register<ZoneMapControl, string>(nameof(PlayerName));

        public static readonly StyledProperty<bool> ShowLabelsProperty =
            AvaloniaProperty.Register<ZoneMapControl, bool>(nameof(ShowLabels), defaultValue: true);

        private const double MinimumZoom = 0.02;
        private const double MaximumZoom = 40.0;
        private const double ZoomStep = 1.15;
        private const double PlayerMarkerRadius = 5.0;

        private readonly Dictionary<uint, ImmutablePen> pensByColour = new Dictionary<uint, ImmutablePen>();
        private readonly Dictionary<uint, ImmutableSolidColorBrush> brushesByColour =
            new Dictionary<uint, ImmutableSolidColorBrush>();

        private static readonly ImmutablePen PlayerPen =
            new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(255, 90, 90)), 2.0);

        private static readonly ImmutableSolidColorBrush PlayerFill =
            new ImmutableSolidColorBrush(Color.FromArgb(200, 255, 90, 90));

        private Point panOffset;
        private double zoom = 1.0;
        private bool hasFitToViewport;
        private Point? dragOrigin;
        private Point dragStartPan;
        private Rect transformedBounds;

        static ZoneMapControl()
        {
            AffectsRender<ZoneMapControl>(
                MapDataProperty,
                PlayerLocationProperty,
                PlayerNameProperty,
                ShowLabelsProperty);

            MapDataProperty.Changed.AddClassHandler<ZoneMapControl>((control, _) => control.OnMapDataChanged());
        }

        public ZoneMapControl()
        {
            ClipToBounds = true;
            Focusable = true;
        }

        public ParsedData MapData
        {
            get => GetValue(MapDataProperty);
            set => SetValue(MapDataProperty, value);
        }

        public Point3D? PlayerLocation
        {
            get => GetValue(PlayerLocationProperty);
            set => SetValue(PlayerLocationProperty, value);
        }

        public string PlayerName
        {
            get => GetValue(PlayerNameProperty);
            set => SetValue(PlayerNameProperty, value);
        }

        public bool ShowLabels
        {
            get => GetValue(ShowLabelsProperty);
            set => SetValue(ShowLabelsProperty, value);
        }

        public void ResetView()
        {
            hasFitToViewport = false;
            InvalidateVisual();
        }

        private void OnMapDataChanged()
        {
            hasFitToViewport = false;
            transformedBounds = EqMapProjection.ComputeTransformedBounds(MapData);
        }

        private void FitToViewport()
        {
            if (transformedBounds.Width <= 0 || transformedBounds.Height <= 0)
                return;

            if (Bounds.Width <= 0 || Bounds.Height <= 0)
                return;

            const double margin = 16.0;
            var usableWidth = Math.Max(1.0, Bounds.Width - (margin * 2));
            var usableHeight = Math.Max(1.0, Bounds.Height - (margin * 2));

            zoom = Math.Min(usableWidth / transformedBounds.Width, usableHeight / transformedBounds.Height);
            zoom = Math.Clamp(zoom, MinimumZoom, MaximumZoom);

            var centre = transformedBounds.Center;
            panOffset = new Point(
                (Bounds.Width / 2) - (centre.X * zoom),
                (Bounds.Height / 2) - (centre.Y * zoom));

            hasFitToViewport = true;
        }

        private Point Project(double eqX, double eqY)
        {
            var screenPoint = EqMapProjection.ToScreenSpace(eqX, eqY);
            return new Point(
                (screenPoint.X * zoom) + panOffset.X,
                (screenPoint.Y * zoom) + panOffset.Y);
        }

        private ImmutablePen GetPen(EQTool.Models.Colour colour)
        {
            var key = PackColour(colour);
            if (pensByColour.TryGetValue(key, out var existing))
                return existing;

            var created = new ImmutablePen(
                new ImmutableSolidColorBrush(Color.FromArgb(255, colour.R, colour.G, colour.B)),
                1.0);
            pensByColour[key] = created;
            return created;
        }

        private ImmutableSolidColorBrush GetBrush(EQTool.Models.Colour colour)
        {
            var key = PackColour(colour);
            if (brushesByColour.TryGetValue(key, out var existing))
                return existing;

            var created = new ImmutableSolidColorBrush(Color.FromArgb(255, colour.R, colour.G, colour.B));
            brushesByColour[key] = created;
            return created;
        }

        private static uint PackColour(EQTool.Models.Colour colour)
        {
            return ((uint)colour.R << 16) | ((uint)colour.G << 8) | colour.B;
        }

        private static double LabelFontSize(LabelSize size)
        {
            switch (size)
            {
                case LabelSize.Large: return 14.0;
                case LabelSize.Medium: return 11.0;
                default: return 9.0;
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var data = MapData;
            if (data == null)
                return;

            if (!hasFitToViewport)
                FitToViewport();

            if (data.Lines != null)
            {
                foreach (var line in data.Lines)
                {
                    if (line.Points == null || line.Points.Length < 2)
                        continue;

                    var start = Project(line.Points[0].X, line.Points[0].Y);
                    var end = Project(line.Points[1].X, line.Points[1].Y);
                    context.DrawLine(GetPen(line.Color), start, end);
                }
            }

            if (ShowLabels && data.Labels != null)
            {
                foreach (var label in data.Labels)
                {
                    if (string.IsNullOrWhiteSpace(label.label))
                        continue;

                    var origin = Project(label.Point.X, label.Point.Y);
                    var formatted = new FormattedText(
                        label.label.Replace('_', ' '),
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        LabelFontSize(label.LabelSize),
                        GetBrush(label.Color));

                    context.DrawText(formatted, origin);
                }
            }

            DrawPlayer(context, data);
        }

        private void DrawPlayer(DrawingContext context, ParsedData data)
        {
            var location = PlayerLocation;
            if (location == null)
                return;

            // Geometry was shifted to the origin at parse time; a raw log position
            // has to have the same shift applied before it lines up with it.
            var centre = Project(
                location.Value.X - data.Offset.X,
                location.Value.Y - data.Offset.Y);

            context.DrawEllipse(PlayerFill, PlayerPen, centre, PlayerMarkerRadius, PlayerMarkerRadius);

            if (string.IsNullOrWhiteSpace(PlayerName))
                return;

            var nameText = new FormattedText(
                PlayerName,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11.0,
                PlayerFill);

            context.DrawText(nameText, new Point(centre.X + PlayerMarkerRadius + 3, centre.Y - 7));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            dragOrigin = point.Position;
            dragStartPan = panOffset;
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (dragOrigin == null)
                return;

            var current = e.GetCurrentPoint(this).Position;
            panOffset = new Point(
                dragStartPan.X + (current.X - dragOrigin.Value.X),
                dragStartPan.Y + (current.Y - dragOrigin.Value.Y));

            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            dragOrigin = null;
            e.Pointer.Capture(null);
        }

        // Zoom about the cursor rather than the centre, so the point under the
        // pointer stays put. Without this, zooming into a corner walks the map away.
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (Math.Abs(e.Delta.Y) < double.Epsilon)
                return;

            var cursor = e.GetCurrentPoint(this).Position;
            var factor = e.Delta.Y > 0 ? ZoomStep : 1.0 / ZoomStep;
            var updatedZoom = Math.Clamp(zoom * factor, MinimumZoom, MaximumZoom);

            if (Math.Abs(updatedZoom - zoom) < double.Epsilon)
                return;

            var worldX = (cursor.X - panOffset.X) / zoom;
            var worldY = (cursor.Y - panOffset.Y) / zoom;

            zoom = updatedZoom;
            panOffset = new Point(cursor.X - (worldX * zoom), cursor.Y - (worldY * zoom));

            hasFitToViewport = true;
            InvalidateVisual();
        }
    }
}
