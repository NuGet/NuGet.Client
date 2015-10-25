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

        public static object ToolWindowButtonHoverActiveKey
        {
            get { return VsBrushes.ToolWindowButtonHoverActiveKey; }
        }

        public static object ToolWindowButtonHoverActiveBorderKey
        {
            get { return VsBrushes.ToolWindowButtonHoverActiveBorderKey; }
        }

        public static object ToolWindowBorderKey
        {
            get { return VsBrushes.ToolWindowBorderKey; }
        }

        public static object ToolWindowButtonDownKey
        {
            get { return VsBrushes.ToolWindowButtonDownKey; }
        }

        public static object ToolWindowButtonDownBorderKey
        {
            get { return VsBrushes.ToolWindowButtonDownBorderKey; }
        }

        public static object ComboBoxBorderKey
        {
            get { return VsBrushes.ComboBoxBorderKey; }
        }

        public static object SplitterBackgroundKey
        {
            get { return VsBrushes.CommandShelfBackgroundGradientKey; }
        }


        public static object HeaderColorsSeparatorLineBrushKey
        {
            get { return HeaderColors.SeparatorLineBrushKey; }
        }

        public static object HeaderColorsDefaultBrushKey
        {
            get { return HeaderColors.DefaultBrushKey; }
        }

        public static object HeaderColorsDefaultTextBrushKey
        {
            get { return HeaderColors.DefaultTextBrushKey; }
        }

        public static object HeaderColorsMouseOverBrushKey
        {
            get { return HeaderColors.MouseOverBrushKey; }
        }

        public static object HeaderColorsMouseOverTextBrushKey
        {
            get { return HeaderColors.MouseOverTextBrushKey; }
        }

        public static object HeaderColorsMouseDownBrushKey
        {
            get { return HeaderColors.MouseDownBrushKey; }
        }

        public static object HeaderColorsMouseDownTextBrushKey
        {
            get { return HeaderColors.MouseDownTextBrushKey; }
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
            if (StandaloneSwitch.IsRunningStandalone)
            {
                // the values are not important. Just make sure they
                // are not null othewise the xaml parser will throw exception
                // when the keys are used in an xaml file.
                ContentBrushKey = SystemColors.WindowBrush;
                ContentMouseOverTextBrushKey = SystemColors.ControlTextBrush;
                ContentInactiveSelectedTextBrushKey = SystemColors.ControlTextBrush;
                BackgroundBrushKey = SystemColors.WindowBrush;
                ContentSelectedBrushKey = SystemColors.ActiveCaptionBrushKey;
            }
            else
            {
                // use colors of VisualStudio UI.
                var assembly = AppDomain.CurrentDomain.Load("Microsoft.VisualStudio.ExtensionsExplorer.UI");
                var colorResources = assembly.GetType("Microsoft.VisualStudio.ExtensionsExplorer.UI.ColorResources");

                var prop = colorResources.GetProperty("ContentMouseOverBrushKey");
                ContentMouseOverBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentMouseOverTextBrushKey");
                ContentMouseOverTextBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentInactiveSelectedBrushKey");
                ContentInactiveSelectedBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentInactiveSelectedTextBrushKey");
                ContentInactiveSelectedTextBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentSelectedBrushKey");
                ContentSelectedBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentSelectedTextBrushKey");
                ContentSelectedTextBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("ContentBrushKey");
                ContentBrushKey = prop.GetValue(null);

                prop = colorResources.GetProperty("BackgroundBrushKey");
                BackgroundBrushKey = prop.GetValue(null);
            }
        }

        public static object ContentMouseOverBrushKey { get; private set; }

        public static object ContentMouseOverTextBrushKey { get; private set; }

        public static object ContentInactiveSelectedBrushKey { get; private set; }

        public static object ContentInactiveSelectedTextBrushKey { get; private set; }

        public static object ContentSelectedBrushKey { get; private set; }

        public static object ContentSelectedTextBrushKey { get; private set; }

        public static object ContentBrushKey { get; private set; }

        public static object BackgroundBrushKey { get; private set; }
    }
}