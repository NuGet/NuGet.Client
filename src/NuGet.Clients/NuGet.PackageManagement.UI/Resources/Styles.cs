// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public static class Styles
    {
        [Browsable(false)]
        public static object ThemedComboStyleKey => typeof(ComboBox);

        [Browsable(false)]
        public static object ScrollBarStyleKey => typeof(ScrollBar);

        [Browsable(false)]
        public static object ScrollViewerStyleKey => typeof(ScrollViewer);
    }
}
