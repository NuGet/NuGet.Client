// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.PowerShellGet
{
    public class PSPackageMetadata : PackageIdentity
    {
        public NuGetVersion ModuleVersion { get; internal set; }
        public string CompanyName { get; internal set; }
        public Guid Guid { get; internal set; }
        public NuGetVersion PowerShellHostVersion { get; internal set; }
        public NuGetVersion DotNetFrameworkVersion { get; internal set; }
        public NuGetVersion CLRVersion { get; internal set; }
        public string ProcessorArchitecture { get; internal set; }
        public IEnumerable<string> CmdletsToExport { get; internal set; }
        public IEnumerable<string> FunctionsToExport { get; internal set; }
        public IEnumerable<string> DscResourcesToExport { get; internal set; }
        public Uri LicenseUrl { get; internal set; }
        public Uri ProjectUrl { get; internal set; }
        public string ReleaseNotes { get; internal set; }

        public PSPackageMetadata(string id, NuGetVersion version)
            : base(id, version)
        {
        }
    }
}
