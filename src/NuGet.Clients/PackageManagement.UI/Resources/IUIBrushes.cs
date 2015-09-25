// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public interface IUIBrushes
    {
        object HeaderBackground { get; }
        object BorderBrush { get; }
        object ListPaneBackground { get; }
        object DetailPaneBackground { get; }
        object LegalMessageBackground { get; }
        object UIText { get; }
        object ControlLinkTextKey { get; }
        object ControlLinkTextHoverKey { get; }
        object WindowTextKey { get; }
        object IndicatorFillBrushKey { get; }

        //public static Microsoft.VisualStudio.Shell.ThemeResourceKey IndicatorFillBrushKey
        //{
        //    get
        //    {
        //        return null;
        //        //return Microsoft.VisualStudio.PlatformUI.ProgressBarColors.IndicatorFillBrushKey;
        //    }
        //}
    }
}
