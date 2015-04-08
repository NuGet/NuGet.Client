using System;
using System.Collections.Generic;
using NuGet.Packaging;

namespace NuGet.PackageManagement
{
    public class PackageRestoreFailedEventArgs : EventArgs
    {
        public PackageReference RestoreFailedPackageReference { get; private set; }
        public Exception Exception { get; private set; }
        public IReadOnlyCollection<string> ProjectNames { get; private set; }

        public PackageRestoreFailedEventArgs(PackageReference restoredFailedPackageReference, Exception exception, IReadOnlyCollection<string> projectNames)
        {
            if (restoredFailedPackageReference == null)
            {
                throw new ArgumentNullException("restoredFailedPackageReference");
            }

            if (exception == null)
            {
                throw new ArgumentNullException("exception");
            }

            if (projectNames == null)
            {
                throw new ArgumentNullException("projectNames");
            }

            RestoreFailedPackageReference = restoredFailedPackageReference;
            Exception = exception;
            ProjectNames = projectNames;
        }
    }
}
