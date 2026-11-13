using System;
using System.Windows;
using System.Windows.Media;

namespace Scout.Controls;

public static class NotchGeometryHelper
{
    public static PathGeometry CreateNotchPath(double width, double height, double cornerRadius, double earRadius)
    {
        cornerRadius = Math.Min(cornerRadius, Math.Min(width, height) / 2.0);
        earRadius = Math.Min(earRadius, height / 2.0);

        var figure = new PathFigure
        {
            StartPoint = new Point(0, 0),
            IsClosed = true,
            IsFilled = true
        };

        // Left ear: curve from (0,0) down and right to (earRadius, earRadius)
        figure.Segments.Add(new BezierSegment(
            new Point(0.5523 * earRadius, 0),
            new Point(earRadius, (1 - 0.5523) * earRadius),
            new Point(earRadius, earRadius),
            true));

        // Left vertical side down to bottom-left corner
        figure.Segments.Add(new LineSegment(new Point(earRadius, height - cornerRadius), true));

        // Bottom-left rounded corner
        figure.Segments.Add(new ArcSegment(
            new Point(earRadius + cornerRadius, height),
            new Size(cornerRadius, cornerRadius),
            0, false, SweepDirection.Counterclockwise, true));

        // Bottom horizontal line to bottom-right corner
        figure.Segments.Add(new LineSegment(new Point(width - earRadius - cornerRadius, height), true));

        // Bottom-right rounded corner
        figure.Segments.Add(new ArcSegment(
            new Point(width - earRadius, height - cornerRadius),
            new Size(cornerRadius, cornerRadius),
            0, false, SweepDirection.Counterclockwise, true));

        // Right vertical side up to right ear
        figure.Segments.Add(new LineSegment(new Point(width - earRadius, earRadius), true));

        // Right ear: curve up and right to (width, 0)
        figure.Segments.Add(new BezierSegment(
            new Point(width - earRadius, (1 - 0.5523) * earRadius),
            new Point(width - earRadius + 0.5523 * earRadius, 0),
            new Point(width, 0),
            true));

        // Top line back to start
        figure.Segments.Add(new LineSegment(new Point(0, 0), true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}
