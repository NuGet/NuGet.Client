// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;

namespace NuGet.Frameworks
{
    public class OneWayCompatibilityMappingEntry : IEquatable<OneWayCompatibilityMappingEntry>
    {
        /// <summary>
        /// Creates a one way compatibility mapping.
        /// Ex: net -supports-> native
        /// </summary>
        /// <param name="targetFramework">Project framework</param>
        /// <param name="supportedFramework">Framework that is supported by the project framework</param>
        public OneWayCompatibilityMappingEntry(FrameworkRange targetFramework, FrameworkRange supportedFramework)
        {
            TargetFrameworkRange = targetFramework;
            SupportedFrameworkRange = supportedFramework;
        }

        /// <summary>
        /// Primary framework range or project target framework that supports the SuppportedFrameworkRange
        /// </summary>
        public FrameworkRange TargetFrameworkRange { get; }

        /// <summary>
        /// Framework range that is supported by the TargetFrameworkRange
        /// </summary>
        public FrameworkRange SupportedFrameworkRange { get; }

        public static CompatibilityMappingComparer Comparer
        {
            get { return CompatibilityMappingComparer.Instance; }
        }

        public bool Equals(OneWayCompatibilityMappingEntry? other)
        {
            return Comparer.Equals(this, other);
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} -> {1}", TargetFrameworkRange.ToString(), SupportedFrameworkRange.ToString());
        }
    }
}
