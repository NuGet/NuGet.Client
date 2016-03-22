// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.PackageCreation.Resources;

namespace NuGet.Packaging
{
    public class PackageReferenceSet
    {
        internal static readonly char[] ReferenceFileInvalidCharacters = new[] {
                    '\x00', '\x01', '\x02', '\x03', '\x04', '\x05', '\x06', '\x07',
                    '\x08', '\x09', '\x0A', '\x0B', '\x0C', '\x0D', '\x0E', '\x0F', '\x10', '\x11', '\x12',
                    '\x13', '\x14', '\x15', '\x16', '\x17', '\x18', '\x19', '\x1A', '\x1B', '\x1C', '\x1D',
                    '\x1E', '\x1F', '\x22', '\x3C', '\x3E', '\x7C', ':', '*', '?', '\\', '/' };

        public PackageReferenceSet(IEnumerable<string> references)
            : this((NuGetFramework)null, references)
        {
        }

        public PackageReferenceSet(string targetFramework, IEnumerable<string> references)
            : this(targetFramework != null ? NuGetFramework.Parse(targetFramework) : null, references)
        {
        }

        public PackageReferenceSet(NuGetFramework targetFramework, IEnumerable<string> references)
        {
            if (references == null)
            {
                throw new ArgumentNullException(nameof(references));
            }

            TargetFramework = targetFramework;
            References = references.ToArray();
        }

        public IReadOnlyCollection<string> References { get; }

        public NuGetFramework TargetFramework { get; }

        public IEnumerable<string> Validate()
        {
            foreach(var reference in References)
            {
                if (String.IsNullOrEmpty(reference))
                {
                    yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_RequiredElementMissing, "File");
                }
                else if (reference.IndexOfAny(ReferenceFileInvalidCharacters) != -1)
                {
                    yield return String.Format(CultureInfo.CurrentCulture, NuGetResources.Manifest_InvalidReferenceFile, reference);
                }
            }
        }
    }
}