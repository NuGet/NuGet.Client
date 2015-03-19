using NuGet.Packaging.Core;
using System;

namespace NuGet.ProjectManagement
{
    public class PackageEventArgs : EventArgs
    {
        private readonly PackageIdentity _identity;
        private readonly NuGetProject _project;
        private readonly string _installPath;

        /// <summary>
        /// Default constructor for events where no info is known
        /// </summary>
        public PackageEventArgs()
            : this(null, null, null)
        {

        }

        public PackageEventArgs(NuGetProject project, PackageIdentity identity, string installPath)
        {
            _identity = identity;
            _installPath = installPath;
            _project = project;
        }

        /// <summary>
        /// Package identity
        /// </summary>
        public PackageIdentity Identity
        {
            get
            {
                return _identity;
            }
        }

        /// <summary>
        /// Folder path of the package
        /// </summary>
        public string InstallPath
        {
            get
            {
                return _installPath;
            }
        }

        /// <summary>
        /// Project where the action occurred
        /// </summary>
        public NuGetProject Project
        {
            get
            {
                return _project;
            }
        }
    }
}
