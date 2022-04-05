// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using ContractsItemFilter = NuGet.VisualStudio.Internal.Contracts.ItemFilter;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion, bool isSolutionLevel, ContractsItemFilter activeTab)
        {
            Action = action;

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
            Version = packageVersion;
            IsSolutionLevel = isSolutionLevel;
            ActiveTab = activeTab;
        }

        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion, bool isSolutionLevel, ContractsItemFilter activeTab, VersionRange versionRange)
            : this(action, packageId, packageVersion, isSolutionLevel, activeTab)
        {
            VersionRange = versionRange;
        }

        public NuGetProjectActionType Action { get; private set; }
        public bool IsSolutionLevel { get; private set; }
        public ContractsItemFilter ActiveTab { get; private set; }
        public string PackageId { get; }
        public NuGetVersion Version { get; }
        public VersionRange VersionRange { get; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, bool isSolutionLevel, ContractsItemFilter activeTab)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion, isSolutionLevel, activeTab);
        }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, bool isSolutionLevel, ContractsItemFilter activeTab, VersionRange versionRange)
        {
            if (packageVersion == null && versionRange == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion, isSolutionLevel, activeTab, versionRange);
        }

        public static UserAction CreateUnInstallAction(string packageId, bool isSolutionLevel, ContractsItemFilter activeTab)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, packageId, packageVersion: null, isSolutionLevel, activeTab);
        }
    }
}
