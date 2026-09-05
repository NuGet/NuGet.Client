// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.CommandLine.XPlat
{
    /// <summary>
    /// The sponsorship URLs a single package source returned for a package, in source order.
    /// </summary>
    internal sealed class PackageSponsorship
    {
        internal string Source { get; }
        internal IReadOnlyList<string> Urls { get; }

        internal PackageSponsorship(string source, IReadOnlyList<string> urls)
        {
            Source = source;
            Urls = urls;
        }
    }
}
