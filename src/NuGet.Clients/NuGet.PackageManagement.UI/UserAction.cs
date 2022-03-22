// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion, UIOperationSource uiSource)
        {
            Action = action;

            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
            Version = packageVersion;
            UIOperationsource = uiSource;
        }

        public UIOperationSource UIOperationsource { get; set; }

        public NuGetProjectActionType Action { get; private set; }

        public string PackageId { get; }

        public NuGetVersion Version { get; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, UIOperationSource uiSource)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion, uiSource);
        }

        public static UserAction CreateUnInstallAction(string packageId, UIOperationSource uiSource)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, packageId, packageVersion: null, uiSource);
        }
    }
}
