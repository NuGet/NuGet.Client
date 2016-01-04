// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion)
        {
            Action = action;

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
            Version = packageVersion;
        }

        public NuGetProjectActionType Action { get; private set; }

        public string PackageId { get; }

        public NuGetVersion Version { get; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(Version));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion);
        }

        public static UserAction CreateUnInstallAction(string packageId)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, packageId, null);
        }
    }
}
