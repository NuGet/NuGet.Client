// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Protocol.ApiApps
{
    /// <summary>
    /// Namespace, Id, Version
    /// </summary>
    public class ApiAppIdentity : PackageIdentity, IEquatable<ApiAppIdentity>
    {
        private readonly string _packageNamespace;

        public ApiAppIdentity(string packageNamespace, string id, NuGetVersion version)
            : base(id, version)
        {
            _packageNamespace = packageNamespace;
        }

        /// <summary>
        /// Namespace for the package Id
        /// </summary>
        public string Namespace
        {
            get { return _packageNamespace; }
        }

        public bool Equals(ApiAppIdentity other)
        {
            return base.Equals(other) && StringComparer.OrdinalIgnoreCase.Equals(Namespace, other.Namespace);
        }

        public override bool Equals(object obj)
        {
            var other = obj as ApiAppIdentity;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return ToString().ToUpperInvariant().GetHashCode();
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0}:{1}", _packageNamespace, base.ToString());
        }
    }
}
