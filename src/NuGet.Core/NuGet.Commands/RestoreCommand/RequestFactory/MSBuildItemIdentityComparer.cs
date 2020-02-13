// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.Commands
{
    /// <summary>
    /// A comparer for the <see cref="IMSBuildItem"/> based on the <see cref="IMSBuildItem.Identity"/>.
    /// </summary>
    internal class MSBuildItemIdentityComparer : IEqualityComparer<IMSBuildItem>
    {
        public bool Equals(IMSBuildItem x, IMSBuildItem y)
        {
            return string.Equals(x?.Identity, y?.Identity, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(IMSBuildItem obj)
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Identity);
        }
    }
}
