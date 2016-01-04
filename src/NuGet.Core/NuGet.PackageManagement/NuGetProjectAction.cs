// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    /// <summary>
    /// Enum for the type of NuGetProjectAction
    /// </summary>
    public enum NuGetProjectActionType
    {
        /// <summary>
        /// Install
        /// </summary>
        Install,

        /// <summary>
        /// Uninstall
        /// </summary>
        Uninstall
    }

    /// <summary>
    /// NuGetProjectAction
    /// </summary>
    public class NuGetProjectAction
    {
        /// <summary>
        /// PackageIdentity on which the action is performed
        /// </summary>
        public PackageIdentity PackageIdentity { get; private set; }

        /// <summary>
        /// Type of NuGetProjectAction. Install/Uninstall
        /// </summary>
        public NuGetProjectActionType NuGetProjectActionType { get; private set; }

        /// <summary>
        /// For NuGetProjectActionType.Install, SourceRepository from which the package should be installed
        /// For NuGetProjectActionType.Uninstall, this will be null
        /// </summary>
        public SourceRepository SourceRepository { get; private set; }

        protected NuGetProjectAction(PackageIdentity packageIdentity, NuGetProjectActionType nuGetProjectActionType, SourceRepository sourceRepository = null)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            PackageIdentity = packageIdentity;
            NuGetProjectActionType = nuGetProjectActionType;
            SourceRepository = sourceRepository;
        }

        public static NuGetProjectAction CreateInstallProjectAction(PackageIdentity packageIdentity, SourceRepository sourceRepository)
        {
            return new NuGetProjectAction(packageIdentity, NuGetProjectActionType.Install, sourceRepository);
        }

        internal static NuGetProjectAction CreateUninstallProjectAction(PackageIdentity packageIdentity)
        {
            return new NuGetProjectAction(packageIdentity, NuGetProjectActionType.Uninstall);
        }
    }
}
