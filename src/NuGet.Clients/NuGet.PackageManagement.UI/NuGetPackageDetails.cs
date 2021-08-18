// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;

namespace NuGet.PackageManagement.UI
{
    
    public class NuGetPackageDetails
    {
        public NuGetPackageDetails(string packageName, string versionNumber)
        {
            PackageName = packageName;
            VersionNumber = versionNumber;
        }

        public string PackageName { get; }

        public string VersionNumber { get; }
    }
}
