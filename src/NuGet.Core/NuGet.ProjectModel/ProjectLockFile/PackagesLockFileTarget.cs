// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// FrameworkName/RuntimeIdentifier combination
    /// </summary>
    public class PackagesLockFileTarget : IEquatable<PackagesLockFileTarget>
    {
        /// <summary>
        /// Target framework.
        /// </summary>
        public NuGetFramework TargetFramework { get; set; }

        /// <summary>	
        /// Null for RIDless graphs.	
        /// </summary>	
        public string RuntimeIdentifier { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IList<LockFileDependency> Dependencies { get; set; } = new List<LockFileDependency>();

        /// <summary>
        /// Full framework name.
        /// </summary>
        public string Name => GetNameString(TargetFramework.DotNetFrameworkName, RuntimeIdentifier);

        public bool Equals(PackagesLockFileTarget other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StringComparer.Ordinal.Equals(RuntimeIdentifier, other.RuntimeIdentifier)
                && NuGetFramework.Comparer.Equals(TargetFramework, other.TargetFramework)
                && EqualityUtility.SequenceEqualWithNullCheck(Dependencies, other.Dependencies);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PackagesLockFileTarget);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(TargetFramework);
            combiner.AddObject(RuntimeIdentifier);
            combiner.AddSequence(Dependencies);

            return combiner.CombinedHash;
        }

        private static string GetNameString(string framework, string runtime)
        {
            if (!string.IsNullOrEmpty(runtime))
            {
                return $"{framework}/{runtime}";
            }

            return framework;
        }
    }
}