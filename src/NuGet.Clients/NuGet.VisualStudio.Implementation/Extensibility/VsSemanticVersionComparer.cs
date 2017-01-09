// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using NuGet.Versioning;

namespace NuGet.VisualStudio
{
    [Export(typeof(IVsSemanticVersionComparer))]
    public class VsSemanticVersionComparer : IVsSemanticVersionComparer
    {
        public int Compare(string versionA, string versionB)
        {
            if (versionA == null)
            {
                throw new ArgumentNullException(nameof(versionA));
            }

            if (versionB == null)
            {
                throw new ArgumentNullException(nameof(versionB));
            }

            var parsedVersionA = NuGetVersion.Parse(versionA);
            var parsedVersionB = NuGetVersion.Parse(versionB);

            return parsedVersionA.CompareTo(parsedVersionB);
        }
    }
}
