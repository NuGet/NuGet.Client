// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public static class Brushes
    {
        public static object HeaderBackground
        {
            get { return VsBrushes.BrandedUIBackgroundKey; }
        }

        public static object BorderBrush
        {
            get { return VsBrushes.BrandedUIBorderKey; }
        }

        public static object ListPaneBackground
        {
            get { return VsBrushes.BrandedUIBackgroundKey; }
        }

        public static object DetailPaneBackground
        {
            get { return VsBrushes.BrandedUIBackgroundKey; }
        }

        public static object LegalMessageBackground
        {
            get { return VsBrushes.BrandedUIBackgroundKey; }
        }

        public static object UIText
        {
            get { return VsBrushes.BrandedUITextKey; }
        }

        public static object ControlLinkTextKey
        {
            get { return VsBrushes.ControlLinkTextKey; }
        }

        public static object ControlLinkTextHoverKey
        {
            get { return VsBrushes.ControlLinkTextHoverKey; }
        }

        public static object WindowTextKey
        {
            get { return VsBrushes.WindowTextKey; }
        }

        public static object IndicatorFillBrushKey
        {
            get
            {
                if (StandaloneSwitch.IsRunningStandalone)
                {
                    return SystemColors.WindowFrameColor;
                }
                return ProgressBarColors.IndicatorFillBrushKey;
            }
        }

        public static void Initialize()
        {
            var assembly = AppDomain.CurrentDomain.Load("Microsoft.VisualStudio.ExtensionsExplorer.UI");
            var colorResources = assembly.GetType("Microsoft.VisualStudio.ExtensionsExplorer.UI.ColorResources");

            var prop = colorResources.GetProperty("ContentMouseOverBrushKey");
            ContentMouseOverBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentInactiveSelectedBrushKey");
            ContentInactiveSelectedBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentSelectedBrushKey");
            ContentSelectedBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentSelectedTextBrushKey");
            ContentSelectedTextBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("ContentBrushKey");
            ContentBrushKey = prop.GetValue(null);

            prop = colorResources.GetProperty("BackgroundBrushKey");
            BackgroundBrushKey = prop.GetValue(null);
        }

        public static object ContentMouseOverBrushKey { get; private set; }

        public static object ContentInactiveSelectedBrushKey { get; private set; }

        public static object ContentSelectedBrushKey { get; private set; }

        public static object ContentSelectedTextBrushKey { get; private set; }

        public static object ContentBrushKey { get; private set; }

        public static object BackgroundBrushKey { get; private set; }
    }
}