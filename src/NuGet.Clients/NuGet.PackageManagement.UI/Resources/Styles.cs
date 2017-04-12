// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public class Styles
    {
        public static void LoadVsStyles()
        {
            var assembly = AppDomain.CurrentDomain.Load(
                "Microsoft.VisualStudio.ExtensionsExplorer.UI");
            var comboBoxType = assembly.GetType(
                "Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox");
            ThemedComboStyleKey = new ComponentResourceKey(comboBoxType, "ThemedComboBoxStyle");
        }

        [Browsable(false)]
        public static object ThemedComboStyleKey { get; private set; } = typeof(ComboBox);

        [Browsable(false)]
        public static object ScrollBarStyleKey => VsResourceKeys.ScrollBarStyleKey ?? typeof(ScrollBar);

        [Browsable(false)]
        public static object ScrollViewerStyleKey => VsResourceKeys.ScrollViewerStyleKey ?? typeof(ScrollViewer);
    }
}