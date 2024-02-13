// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class SearchResultPackage
    {
        public IPackageSearchMetadata PackageSearchMetadata { get; set; }
        public string DeprecationMessage { get; set; }
    }
}
