// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;

namespace NuGet.PackageManagement.UI
{
    public class ArrowGlyphAdorner : Adorner
    {
        private GridViewColumnHeader _columnHeader;
        private ListSortDirection _direction;

        public ArrowGlyphAdorner(GridViewColumnHeader columnHeader, ListSortDirection direction) : base(columnHeader)
        {
            _columnHeader = columnHeader;
            _direction = direction;
        }

        private Geometry GetDefaultGlyph()
        {
            var x1 = _columnHeader.ActualWidth - 13;
            var x2 = x1 + 7;
            var x3 = x1 + 3.5;
            var y1 = _columnHeader.ActualHeight / 2 - 3;
            var y2 = y1 + 3.5;

            if (_direction == ListSortDirection.Ascending)
            {
                var tmp = y1;
                y1 = y2;
                y2 = tmp;
            }

            var pathSegmentCollection = new PathSegmentCollection();
            pathSegmentCollection.Add(new LineSegment(new Point(x3, y2), true));
            pathSegmentCollection.Add(new LineSegment(new Point(x2, y1), true));

            var pathFigure = new PathFigure(new Point(x1, y1), pathSegmentCollection, false);

            var pathGeometry = new PathGeometry() { Figures = { pathFigure } };
            return pathGeometry;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var drawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.BrandedUITextBrushKey);
            var mediaColor = Color.FromRgb(drawingColor.R, drawingColor.G, drawingColor.B);
            drawingContext.DrawGeometry(null, new Pen(new SolidColorBrush(mediaColor), 1.0), GetDefaultGlyph());
        }
    }
}