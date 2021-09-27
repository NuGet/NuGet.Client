// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using NuGet.Versioning;
using NuGet.VisualStudio;

namespace NuGet.PackageManagement.UI
{
    public class UserAction
    {
        private UserAction(NuGetProjectActionType action, string packageId, NuGetVersion packageVersion, UIActionSource uiActionSource)
            : this(action, uiActionSource)
        {
            if (string.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException(nameof(packageId));
            }

            PackageId = packageId;
            Version = packageVersion;
        }

        private UserAction(NuGetProjectActionType action, UIActionSource uiActionSource)
        {
            Action = action;
            UIActionSource = uiActionSource;
        }

        public NuGetProjectActionType Action { get; private set; }

        public string PackageId { get; }

        public NuGetVersion Version { get; }

        public UIActionSource UIActionSource { get; private set; }

        public static UserAction CreateInstallAction(string packageId, NuGetVersion packageVersion, UIActionSource uiActionSource)
        {
            if (packageVersion == null)
            {
                throw new ArgumentNullException(nameof(packageVersion));
            }

            return new UserAction(NuGetProjectActionType.Install, packageId, packageVersion, uiActionSource);
        }

        public static UserAction CreateUnInstallAction(string packageId, UIActionSource uiActionSource)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, packageId, packageVersion: null, uiActionSource);
        }

        public static UserAction CreateUpdateAction(UIActionSource uiActionSource)
        {
            return new UserAction(NuGetProjectActionType.Uninstall, uiActionSource);
        }
    }
}
