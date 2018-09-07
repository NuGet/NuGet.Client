// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if STANDALONE
using System.Windows;
#else
using Microsoft.VisualStudio.PlatformUI;
#endif

namespace NuGet.PackageManagement.UI
{
#if STANDALONE
    public class VsDialogWindow : Window
    {
        public bool? ShowModal()
        {
            return ShowDialog();
        }
    }
#else
    public class VsDialogWindow : DialogWindow
    {
        // Wrapper for the VS dialog
    }
#endif
}
