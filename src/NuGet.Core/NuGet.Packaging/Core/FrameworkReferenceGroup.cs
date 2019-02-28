// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.Packaging
{
    /// <summary>
    /// Package dependencies grouped to a target framework.
    /// </summary>
    public class FrameworkReferenceGroup : IEquatable<FrameworkReferenceGroup>, IFrameworkSpecific
    {

        /// <summary>
        /// framework reference group
        /// </summary>
        /// <param name="targetFramework">target framework</param>
        /// <param name="frameworkReferences">framework references</param>
        public FrameworkReferenceGroup(NuGetFramework targetFramework, IEnumerable<string> frameworkReferences)
        {
            TargetFramework = targetFramework ?? throw new ArgumentNullException(nameof(targetFramework));
            FrameworkReferences = frameworkReferences ?? throw new ArgumentNullException(nameof(frameworkReferences));
        }

        /// <summary>
        /// Framework reference group target framework
        /// </summary>
        public NuGetFramework TargetFramework { get; }

        /// <summary>
        /// Framework references
        /// </summary>
        public IEnumerable<string> FrameworkReferences { get; }

        public bool Equals(FrameworkReferenceGroup other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return GetHashCode() == other.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var other = obj as FrameworkReferenceGroup;

            if (other != null)
            {
                return Equals(other);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(TargetFramework);

            if (FrameworkReferences != null)
            {
                foreach (var hash in FrameworkReferences.Select(e => e.GetHashCode()).OrderBy(e => e))
                {
                    combiner.AddObject(hash);
                }
            }

            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0}] ({1})", TargetFramework, string.Join(", ", FrameworkReferences));
        }
    }
}
