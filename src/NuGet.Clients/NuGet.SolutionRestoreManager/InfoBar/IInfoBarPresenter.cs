// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SolutionRestoreManager
{
    /// <summary>
    /// Represents the presenter for displaying info bars in Visual Studio.
    /// </summary>
    internal interface IInfoBarPresenter
    {
        /// <summary>
        /// Shows the info bar in Visual Studio.
        /// </summary>
        void ShowInfoBar();

        /// <summary>
        /// Hides the info bar in Visual Studio.
        /// </summary>
        void HideInfoBar();
    }
}
