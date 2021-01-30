// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Windows;
using System.Windows.Media;

namespace NuGet.PackageManagement.UI
{
    public static class WpfUtility
    {
        public static T FindParent<T> (this DependencyObject element)
            where T : FrameworkElement
        {
            T parent = null;
            DependencyObject current = element;

            while (parent == null && current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                parent = current as T;
            }

            return parent;
        }
    }
}
