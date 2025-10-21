// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Common;

namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Contains logics to push symbol packages in Http server or file system
    /// </summary>
    public class SymbolPackageUpdateResourceV3 : INuGetResource
    {
        private string _source;

        public SymbolPackageUpdateResourceV3(string source,
            HttpSource httpSource)
        {
            _source = source;
        }

        public Uri SourceUri => UriUtility.CreateSourceUri(_source);
    }
}
