// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement
{
    public class PackageRestoreData
    {
        public Packaging.PackageReference PackageReference { get; }
        public IEnumerable<string> ProjectNames { get; }
        public bool IsMissing { get; }

        public PackageRestoreData(Packaging.PackageReference packageReference, IEnumerable<string> projectNames, bool isMissing)
        {
            PackageReference = packageReference ?? throw new ArgumentNullException(nameof(packageReference));
            ProjectNames = projectNames ?? throw new ArgumentNullException(nameof(projectNames));
            IsMissing = isMissing;
        }
    }
}
