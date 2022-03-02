// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace NuGet.SolutionRestoreManager
{
    internal class VsProjectReferenceComparer : IEqualityComparer<IVsReferenceItem>
    {
        public static VsProjectReferenceComparer Default { get; } = new VsProjectReferenceComparer();

        public bool Equals(IVsReferenceItem x, IVsReferenceItem y)
        {
            if (x == null) { throw new ArgumentNullException(nameof(x)); }
            if (y == null) { throw new ArgumentNullException(nameof(y)); }

            return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(IVsReferenceItem obj)
        {
            if (obj == null) { throw new ArgumentNullException(nameof(obj)); }

            return obj.Name.GetHashCode();
        }
    }
}
