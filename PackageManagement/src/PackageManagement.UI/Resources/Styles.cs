// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public static class Styles
    {
        public static void Initialize()
        {
            var assembly = AppDomain.CurrentDomain.Load("Microsoft.VisualStudio.ExtensionsExplorer.UI");
            var comboBoxType = assembly.GetType("Microsoft.VisualStudio.ExtensionsExplorer.UI.AutomationComboBox");
            ThemedComboStyle = Application.Current.FindResource(
                new ComponentResourceKey(comboBoxType, "ThemedComboBoxStyle")) as Style;
            ScrollBarStyle = Application.Current.FindResource(VsResourceKeys.ScrollBarStyleKey) as Style;
            ScrollViewerStyle = Application.Current.FindResource(VsResourceKeys.ScrollViewerStyleKey) as Style;
        }

        public static Style ThemedComboStyle { get; private set; }

        public static Style ScrollBarStyle { get; private set; }

        public static Style ScrollViewerStyle { get; private set; }
    }
}