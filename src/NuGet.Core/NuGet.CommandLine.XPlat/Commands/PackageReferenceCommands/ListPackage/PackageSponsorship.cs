// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// The sponsorship URLs a single package source returned for a package, in source order.
    /// </summary>
    internal sealed class PackageSponsorship
    {
        internal string Source { get; } // source url that returns data
        internal IReadOnlyList<string> Urls { get; } // urls in original order

        internal PackageSponsorship(string source, IReadOnlyList<string> urls) // pairs the two
        {
            Source = source;
            Urls = urls;
        }
    }
}
