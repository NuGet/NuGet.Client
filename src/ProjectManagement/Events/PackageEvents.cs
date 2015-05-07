// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.ProjectManagement
{
    /// <summary>
    /// Package events relayed to the public IVsPackageInstallerEvents
    /// </summary>
    public class PackageEvents
    {
        /// <summary>
        /// Raised when a package is about to be installed into the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageInstalling;

        /// <summary>
        /// Raised after a package has been installed into the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageInstalled;

        /// <summary>
        /// Raised when a package is about to be uninstalled from the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageUninstalling;

        /// <summary>
        /// Raised after a package has been uninstalled from the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageUninstalled;

        /// <summary>
        /// Raised after a package has been installed into a project within the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageReferenceAdded;

        /// <summary>
        /// Raised after a package has been uninstalled from a project within the current solution.
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageReferenceRemoved;

        internal PackageEvents()
        {
        }

        internal void NotifyInstalling(PackageEventArgs e)
        {
            if (PackageInstalling != null)
            {
                PackageInstalling(this, e);
            }
        }

        internal void NotifyInstalled(PackageEventArgs e)
        {
            if (PackageInstalled != null)
            {
                PackageInstalled(this, e);
            }
        }

        internal void NotifyUninstalling(PackageEventArgs e)
        {
            if (PackageUninstalling != null)
            {
                PackageUninstalling(this, e);
            }
        }

        internal void NotifyUninstalled(PackageEventArgs e)
        {
            if (PackageUninstalled != null)
            {
                PackageUninstalled(this, e);
            }
        }

        internal void NotifyReferenceAdded(PackageEventArgs e)
        {
            if (PackageReferenceAdded != null)
            {
                PackageReferenceAdded(this, e);
            }
        }

        internal void NotifyReferenceRemoved(PackageEventArgs e)
        {
            if (PackageReferenceRemoved != null)
            {
                PackageReferenceRemoved(this, e);
            }
        }
    }
}
