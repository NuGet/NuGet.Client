// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyType
    {
        private readonly HashSet<LibraryDependencyTypeFlag> _keywords;

        public static LibraryDependencyType Default;
        public static LibraryDependencyType Build;
        public static LibraryDependencyType Platform;

        static LibraryDependencyType()
        {
            Default = new LibraryDependencyType(LibraryDependencyTypeKeyword.Default.FlagsToAdd);
            Build = new LibraryDependencyType(LibraryDependencyTypeKeyword.Build.FlagsToAdd);
            Platform = new LibraryDependencyType(LibraryDependencyTypeKeyword.Platform.FlagsToAdd);
        }

        public LibraryDependencyType()
        {
            _keywords = new HashSet<LibraryDependencyTypeFlag>();
        }

        private LibraryDependencyType(IEnumerable<LibraryDependencyTypeFlag> flags)
        {
            _keywords = new HashSet<LibraryDependencyTypeFlag>(flags);
        }

        public bool Contains(LibraryDependencyTypeFlag flag)
        {
            return _keywords.Contains(flag);
        }

        public static LibraryDependencyType Parse(IEnumerable<string> keywords)
        {
            var type = new LibraryDependencyType();
            foreach (var keyword in keywords.Select(LibraryDependencyTypeKeyword.Parse))
            {
                type = type.Combine(keyword.FlagsToAdd, keyword.FlagsToRemove);
            }
            return type;
        }

        public LibraryDependencyType Combine(
            IEnumerable<LibraryDependencyTypeFlag> add,
            IEnumerable<LibraryDependencyTypeFlag> remove)
        {
            return new LibraryDependencyType(
                _keywords.Except(remove).Union(add).ToArray());
        }

        public override bool Equals(object obj)
        {
            LibraryDependencyType other = obj as LibraryDependencyType;
            return other != null &&
                _keywords.All(other.Contains) &&
                other._keywords.All(_keywords.Contains);
        }

        public override int GetHashCode()
        {
            return _keywords.GetHashCode();
        }

        public override string ToString()
        {
            return string.Join(",", _keywords.Select(kw => kw.ToString()));
        }
    }
}
