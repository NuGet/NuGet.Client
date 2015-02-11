using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement
{
    public class PackageEventArgs : EventArgs
    {
        private readonly NuGetPackageManager _packageManager;
        private readonly PackageIdentity _identity;
        private readonly NuGetProject _project;
        private readonly string _installPath;

        /// <summary>
        /// Default constructor for events where no info is known
        /// </summary>
        public PackageEventArgs(NuGetPackageManager packageManager)
            : this(packageManager, null, null, null)
        {

        }

        public PackageEventArgs(NuGetPackageManager packageManager, NuGetProject project, PackageIdentity identity, string installPath)
        {
            _packageManager = packageManager;
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

        public NuGetPackageManager PackageManager
        {
            get
            {
                return _packageManager;
            }
        }
    }
}
