// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// Represents a package identity with an acessible name in the Preview Window Dialog in the PM UI
    /// </summary>
    public sealed class AccessiblePackageIdentity : PackageIdentity
    {
        private Lazy<string> _automationNameLazy;

        public AccessiblePackageIdentity(string id, NuGetVersion version)
            : base(id, version)
        {
            _automationNameLazy = new Lazy<string>(() => string.Format(
               CultureInfo.CurrentUICulture,
               Resources.Accesibility_PackageIdentity,
               Id,
               Version.ToNormalizedString()));
        }

        public AccessiblePackageIdentity(PackageIdentity id)
            : this(id.Id, id.Version)
        {
        }

        public string AutomationName => _automationNameLazy.Value;
    }
}
