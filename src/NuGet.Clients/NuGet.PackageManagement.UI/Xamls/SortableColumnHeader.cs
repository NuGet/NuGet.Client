// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace NuGet.PackageManagement.UI
{
    public class SortableColumnHeader
    {
        public static string GetSortPropertyName(DependencyObject obj)
        {
            return (string)obj.GetValue(SortPropertyNameProperty);
        }

        public static void SetSortPropertyName(DependencyObject obj, string value)
        {
            obj.SetValue(SortPropertyNameProperty, value);
        }

        public static ListSortDirection? GetSortDirectionProperty(DependencyObject obj)
        {
            return (ListSortDirection?)obj.GetValue(SortDirectionProperty);
        }

        public static void SetSortDirectionProperty(DependencyObject obj, ListSortDirection? value)
        {
            obj.SetValue(SortDirectionProperty, value);
        }

        public static readonly DependencyProperty SortPropertyNameProperty =
            DependencyProperty.RegisterAttached("SortPropertyName", typeof(string), typeof(SortableColumnHeader), new PropertyMetadata(null));

        public static readonly DependencyProperty SortDirectionProperty =
            DependencyProperty.RegisterAttached("SortDirectionProperty", typeof(ListSortDirection?), typeof(SortableColumnHeader), new PropertyMetadata(null, GlyphChanged));

        private static void GlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var columnHeader = d as GridViewColumnHeader;
            if (columnHeader == null)
            {
                return;
            }

            var oldValue = (ListSortDirection?)e.OldValue;
            var newValue = (ListSortDirection?)e.NewValue;

            var layer = AdornerLayer.GetAdornerLayer(columnHeader);

            if (layer == null || oldValue == newValue)
            {
                return;
            }

            if (oldValue != null)
            {
                var adorners = layer.GetAdorners(columnHeader);
                if (adorners != null)
                {
                    foreach (var adorner in adorners)
                    {
                        if (adorner is ArrowGlyphAdorner)
                        {
                            layer.Remove(adorner);
                        }
                    }
                }
            }

            if (newValue == null)
            {
                return;
            }

            layer.Add(new ArrowGlyphAdorner(columnHeader, newValue.Value));
        }
    }
}