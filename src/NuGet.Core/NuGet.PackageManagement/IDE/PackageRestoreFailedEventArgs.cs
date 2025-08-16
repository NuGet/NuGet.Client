// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.PackageManagement
{
    public class PackageRestoreFailedEventArgs : EventArgs
    {
        public Packaging.PackageReference RestoreFailedPackageReference { get; private set; }
        public Exception Exception { get; private set; }
        public IEnumerable<string> ProjectNames { get; private set; }

        public PackageRestoreFailedEventArgs(Packaging.PackageReference restoredFailedPackageReference, Exception exception, IEnumerable<string> projectNames)
        {
            RestoreFailedPackageReference = restoredFailedPackageReference ?? throw new ArgumentNullException(nameof(restoredFailedPackageReference));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ProjectNames = projectNames ?? throw new ArgumentNullException(nameof(projectNames));
        }
    }
}
