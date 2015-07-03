// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class LockFileLibrary : IEquatable<LockFileLibrary>
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public NuGetVersion Version { get; set; }

        public bool IsServiceable { get; set; }

        public string Sha512 { get; set; }

        public IList<string> Files { get; set; } = new List<string>();

        public bool Equals(LockFileLibrary other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Type, other.Type, StringComparison.OrdinalIgnoreCase)
                && IsServiceable == other.IsServiceable
                && Sha512 == other.Sha512
                && Version == other.Version)
            {
                return Files.OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(other.Files.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFileLibrary);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(Name);
            combiner.AddStringIgnoreCase(Type);
            combiner.AddObject(Sha512);
            combiner.AddObject(IsServiceable);
            combiner.AddObject(Version);

            foreach (var file in Files.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
            {
                combiner.AddStringIgnoreCase(file);
            }

            return combiner.CombinedHash;
        }
    }
}
