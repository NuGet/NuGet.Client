// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using NuGet.Shared;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyTypeFlag
    {
        private static readonly ConcurrentDictionary<string, LibraryDependencyTypeFlag> Flags = new ConcurrentDictionary<string, LibraryDependencyTypeFlag>();
        private readonly string _value;

        public static readonly LibraryDependencyTypeFlag MainReference = Declare("MainReference");
        public static readonly LibraryDependencyTypeFlag MainSource = Declare("MainSource");
        public static readonly LibraryDependencyTypeFlag MainExport = Declare("MainExport");
        public static readonly LibraryDependencyTypeFlag PreprocessReference = Declare("PreprocessReference");
        public static readonly LibraryDependencyTypeFlag SharedFramework = Declare("SharedFramework");

        public static readonly LibraryDependencyTypeFlag RuntimeComponent = Declare("RuntimeComponent");
        public static readonly LibraryDependencyTypeFlag DevComponent = Declare("DevComponent");
        public static readonly LibraryDependencyTypeFlag PreprocessComponent = Declare("PreprocessComponent");
        public static readonly LibraryDependencyTypeFlag BecomesNupkgDependency = Declare("BecomesNupkgDependency");

        private LibraryDependencyTypeFlag(string value)
        {
            _value = value;
        }

        public static LibraryDependencyTypeFlag Declare(string keyword)
        {
            return Flags.GetOrAdd(keyword, x => new LibraryDependencyTypeFlag(x));
        }

        public override bool Equals(object obj)
        {
            LibraryDependencyTypeFlag other = obj as LibraryDependencyTypeFlag;
            return other != null && string.Equals(_value, other._value, System.StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.AddSequence(_value);
            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return _value;
        }
    }
}
