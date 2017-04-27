// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public class CacheFile : IEquatable<CacheFile>
    {
        public string DgSpecHash { get; set; }

        public bool IsValid { get { return DgSpecHash != null;  } }


        public bool Equals(CacheFile other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return StringComparer.Ordinal.Equals(DgSpecHash, other.DgSpecHash);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CacheFile);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddObject(DgSpecHash);
            return combiner.CombinedHash;
        }

    }

}
