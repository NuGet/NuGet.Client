// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.LibraryModel;

namespace NuGet.Commands
{
    internal enum LibraryRangeIndex : int
    {
        Invalid = -1,
    }

    internal sealed class LibraryRangeInterningTable
    {
        private int _nextIndex = 0;
        private readonly Dictionary<string, LibraryRangeIndex> _table = new Dictionary<string, LibraryRangeIndex>(StringComparer.OrdinalIgnoreCase);

        internal LibraryRangeIndex Intern(LibraryRange libraryRange)
        {
            string key = libraryRange.ToString();
            if (!_table.TryGetValue(key, out LibraryRangeIndex index))
            {
                index = (LibraryRangeIndex)_nextIndex++;
                _table.Add(key, index);
            }

            return index;
        }
    }
    internal enum LibraryDependencyIndex : int
    {
        Invalid = -1,
    }

    internal sealed class LibraryDependencyInterningTable
    {
        private int _nextIndex = 0;
        private readonly Dictionary<string, LibraryDependencyIndex> _table = new Dictionary<string, LibraryDependencyIndex>(StringComparer.OrdinalIgnoreCase);

        internal LibraryDependencyIndex Intern(LibraryDependency libraryDependency)
        {
            string key = libraryDependency.Name;
            if (!_table.TryGetValue(key, out LibraryDependencyIndex index))
            {
                index = (LibraryDependencyIndex)_nextIndex++;
                _table.Add(key, index);
            }

            return index;
        }
    }
}
