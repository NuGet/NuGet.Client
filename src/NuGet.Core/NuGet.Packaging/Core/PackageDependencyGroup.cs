// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet.Packaging
{
    /// <summary>
    /// Package dependencies grouped to a target framework.
    /// </summary>
    public class PackageDependencyGroup : IEquatable<PackageDependencyGroup>, IFrameworkSpecific
    {
        private readonly NuGetFramework _targetFramework;
        private readonly IEnumerable<PackageDependency> _packages;

        /// <summary>
        /// Dependency group
        /// </summary>
        /// <param name="targetFramework">target framework</param>
        /// <param name="packages">dependant packages</param>
        public PackageDependencyGroup(NuGetFramework targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException(nameof(targetFramework));
            }

            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            _targetFramework = targetFramework;
            _packages = packages;
        }

        /// <summary>
        /// Dependency group target framework
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get { return _targetFramework; }
        }

        /// <summary>
        /// Package dependencies
        /// </summary>
        public IEnumerable<PackageDependency> Packages
        {
            get { return _packages; }
        }

        public bool Equals(PackageDependencyGroup other)
        {
            return PackageDependencyGroupComparer.Default.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            var dependency = obj as PackageDependencyGroup;

            if (dependency != null)
            {
                return Equals(dependency);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return PackageDependencyGroupComparer.Default.GetHashCode(this);
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "[{0}] ({1})", TargetFramework, String.Join(", ", Packages));
        }
    }
}
