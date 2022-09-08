// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.Options
{
    public class PackageSourceViewModel
    {
        public PackageSourceContextInfo SourceInfo { get; private set; }
        public bool IsChecked { get; set; }

        public PackageSourceViewModel(PackageSourceContextInfo sourceInfo, bool isChecked)
        {
            SourceInfo = sourceInfo;
            IsChecked = isChecked;
        }
    }
}
