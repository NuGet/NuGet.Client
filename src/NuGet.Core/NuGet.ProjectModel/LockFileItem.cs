// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ProjectModel
{
    public class LockFileItem : IEquatable<LockFileItem>
    {
        public LockFileItem(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public override string ToString() => Path;

        public bool Equals(LockFileItem other)
        {
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if (string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase))
            {
                return Properties.OrderBy(pair => pair.Key)
                    .SequenceEqual(other.Properties.OrderBy(pair => pair.Key));
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LockFileItem);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(Path);

            foreach (var pair in Properties.OrderBy(pair => pair.Key))
            {
                combiner.AddObject(pair.Key);
                combiner.AddObject(pair.Value);
            }

            return combiner.CombinedHash;
        }
    }
}
