// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.LibraryModel;

namespace NuGet.Commands
{
    internal static class PathToRef
    {
        internal static LibraryRangeIndex[] Create(LibraryRangeIndex[] existingPath, LibraryRangeIndex currentRef)
        {
            LibraryRangeIndex[] newPath = new LibraryRangeIndex[existingPath.Length + 1];
            Array.Copy(existingPath, newPath, existingPath.Length);
            newPath[newPath.Length - 1] = currentRef;

            return newPath;
        }
    }
}
