// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents a package identity with an acessible name in the Preview Window
    /// </summary>
    public class PackageIdentityResult : PackageIdentity
    {
        public PackageIdentityResult(string id, NuGetVersion version)
            : base(id, version)
        {
        }

        public PackageIdentityResult(PackageIdentity id)
            : base(id.Id, id.Version)
        {
        }

        public string AutomationName => string.Format(
                CultureInfo.CurrentUICulture,
                Resources.Accesibility_PackageIdentity,
                Id,
                Version.ToNormalizedString());
    }
}
