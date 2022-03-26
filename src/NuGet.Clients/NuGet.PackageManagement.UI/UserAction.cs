// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion, bool isSolutionLevel)
        {
            Action = action;

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
            Version = packageVersion;
            IsSolutionLevel = isSolutionLevel;
        }

        public NuGetProjectActionType Action { get; private set; }
        public bool IsSolutionLevel { get; private set; }
        public string PackageId { get; }
        public NuGetVersion Version { get; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, bool isSolutionLevel)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion, isSolutionLevel);
        }

        public static UserAction CreateUnInstallAction(string packageId, bool isSolutionLevel)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, packageId, packageVersion: null, isSolutionLevel);
        }
    }
}
