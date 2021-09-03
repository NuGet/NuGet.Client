// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.PackageManagement.UI
{
    public class NuGetDeepLinkErrorWindow : VsDialogWindow
    {
        public NuGetDeepLinkErrorWindow(string message, string buttonText)
        {
            Content = new NuGetDeepLinkErrorView(message, buttonText);
        }
    }
}
