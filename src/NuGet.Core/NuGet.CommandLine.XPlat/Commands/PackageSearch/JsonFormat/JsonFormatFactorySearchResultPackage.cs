// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class JsonFormatFactorySearchResultPackage
    {
        public static ISearchResultPackage GetPackage(IPackageSearchMetadata packageSearchMetadata, PackageSearchVerbosity verbosity, bool exactMatch, string deprecation)
        {
            if (exactMatch)
            {
                switch (verbosity)
                {
                    case PackageSearchVerbosity.Detailed:
                        return new ExactMatchDetailedVerbosityJsonFormatSearchResultPackage(packageSearchMetadata, deprecation);
                    case PackageSearchVerbosity.Minimal:
                        return new ExactMatchMinimalVerbosityJsonFormatSearchResultPackage(packageSearchMetadata);
                    default:
                        return new ExactMatchNormalVerbosityJsonFormatSearchResultPackage(packageSearchMetadata);
                }
            }
            else
            {
                switch (verbosity)
                {
                    case PackageSearchVerbosity.Detailed:
                        return new DetailedVerbosityJsonFormatSearchResultPackage(packageSearchMetadata, deprecation);
                    case PackageSearchVerbosity.Minimal:
                        return new MinimalVerbosityJsonFormatSearchResultPackage(packageSearchMetadata);
                    default:
                        return new NormalVerbosityJsonFormatSearchResultPackage(packageSearchMetadata);
                }
            }
        }
    }
}
