// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NuGet.Protocol.Core.Types;

namespace NuGet.Resolver
{
    public class ResolverPackage : SourcePackageDependencyInfo, IEquatable<ResolverPackage>
    {
        /// <summary>
        /// An absent package represents that the package is not needed in the solution.
        /// </summary>
        public bool Absent { get; }

        public ResolverPackage(string id)
            : this(id, null)
        {
        }

        public ResolverPackage(string id, NuGetVersion version)
            : this(id, version, Enumerable.Empty<PackageDependency>(), true, false)
        {
        }

        public ResolverPackage(string id, NuGetVersion version, IEnumerable<PackageDependency> dependencies, bool listed, bool absent)
            : base(id, version, dependencies, listed, null)
        {
            Debug.Assert(!Absent || (version == null && dependencies == null), "Invalid absent package");

            Absent = absent;
        }

        public ResolverPackage(PackageDependencyInfo info, bool listed, bool absent)
            : this(info.Id, info.Version, info.Dependencies, listed, absent)
        {
        }

        /// <summary>
        /// Find the version range for the given package. The package may not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public VersionRange FindDependencyRange(string id)
        {
            VersionRange range = null;

            Debug.Assert(Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).Count() < 2, "Duplicate dependencies");

            var dependency = Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).FirstOrDefault();

            if (dependency != null)
            {
                range = dependency.VersionRange == null ? VersionRange.All : dependency.VersionRange;
            }

            return range;
        }

        public override string ToString()
        {
            if (Absent)
            {
                return String.Format(CultureInfo.InvariantCulture, "Absent {0}", Id);
            }
            else
            {
                return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Id, Version);
            }
        }

        public bool Equals(ResolverPackage other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return this.Absent == other.Absent && base.Equals(other);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(Absent);
            combiner.AddObject(base.GetHashCode());

            return combiner.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ResolverPackage;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }
    }
}
