// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Shared;

namespace NuGet.LibraryModel
{
    public class LibraryDependencyType : IEquatable<LibraryDependencyType>
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

        public bool Equals(LibraryDependencyType other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return _keywords.All(other.Contains) &&
                   other._keywords.All(_keywords.Contains);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryDependencyType);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            foreach (var val in _keywords)
            {
                combiner.AddObject(val);
            }
            return combiner.CombinedHash;
        }

        public override string ToString()
        {
            return string.Join(",", _keywords.Select(kw => kw.ToString()));
        }
    }
}
