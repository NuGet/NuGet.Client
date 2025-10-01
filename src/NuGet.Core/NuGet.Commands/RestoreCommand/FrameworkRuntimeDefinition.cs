// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Globalization;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.Commands
{
    internal sealed record FrameworkRuntimeDefinition : IEquatable<FrameworkRuntimeDefinition>, IComparable<FrameworkRuntimeDefinition>
    {
        public string TargetAlias { get; }

        public NuGetFramework Framework { get; }

        public string RuntimeIdentifier { get; }

        public string Name { get; }

        public FrameworkRuntimeDefinition(string targetAlias, NuGetFramework framework, string? runtimeIdentifier)
        {
            TargetAlias = targetAlias ?? string.Empty;
            Framework = framework ?? throw new ArgumentNullException(nameof(framework));
            RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
            Name = FrameworkRuntimePair.GetTargetGraphName(framework, runtimeIdentifier);
        }

        public bool Equals(FrameworkRuntimeDefinition? other)
        {
            return other != null &&
                string.Equals(TargetAlias, other.TargetAlias, StringComparison.OrdinalIgnoreCase) &&
                Equals(Framework, other.Framework) &&
                string.Equals(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddStringIgnoreCase(TargetAlias);
            combiner.AddObject(Framework);
            combiner.AddObject(RuntimeIdentifier);
            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "{0}~{1}~{2}",
                TargetAlias,
                Framework.GetShortFolderName(),
                RuntimeIdentifier);
        }

        public int CompareTo(FrameworkRuntimeDefinition? other)
        {
            if (other == null) return 1;

            var fxCompare = string.Compare(Framework.GetShortFolderName(), other.Framework.GetShortFolderName(), StringComparison.Ordinal);
            if (fxCompare != 0)
            {
                return fxCompare;
            }

            fxCompare = string.Compare(TargetAlias, other.TargetAlias, StringComparison.OrdinalIgnoreCase);
            if (fxCompare != 0)
            {
                return fxCompare;
            }

            return string.Compare(RuntimeIdentifier, other.RuntimeIdentifier, StringComparison.Ordinal);
        }
    }
}
