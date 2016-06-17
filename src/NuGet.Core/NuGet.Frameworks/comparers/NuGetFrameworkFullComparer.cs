﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A case insensitive compare of the framework, version, and profile
    /// </summary>
#if NUGET_FRAMEWORKS_INTERNAL
    internal
#else
    public
#endif
    class NuGetFrameworkFullComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (ReferenceEquals(x, null)
                || ReferenceEquals(y, null))
            {
                return false;
            }

            return x.Version == y.Version
                   && StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework)
                   && StringComparer.OrdinalIgnoreCase.Equals(x.Profile, y.Profile);
        }

        public int GetHashCode(NuGetFramework obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return 0;
            }

            var combiner = new HashCodeCombiner();

            combiner.AddStringIgnoreCase(obj.Framework);
            combiner.AddObject(obj.Version);
            combiner.AddStringIgnoreCase(obj.Profile);

            return combiner.CombinedHash;
        }
    }
}
