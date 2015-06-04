// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace NuGet.PackageManagement.UI
{
    public static class ResourceKeys
    {
        public static object ScrollBarStyleKey
        {
            get { return VsResourceKeys.ScrollBarStyleKey; }
        }

        public static object ScrollViewerStyleKey
        {
            get { return VsResourceKeys.ScrollViewerStyleKey; }
        }
    }
}