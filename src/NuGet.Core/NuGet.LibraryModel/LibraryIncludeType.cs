// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Common;

namespace NuGet.LibraryModel
{
    public class LibraryIncludeType : IEquatable<LibraryIncludeType>
    {
        private readonly HashSet<LibraryIncludeTypeFlag> _keywords;

        public static LibraryIncludeType Default = new LibraryIncludeType(LibraryIncludeTypeKeyword.Default.FlagsToAdd);
        public static LibraryIncludeType All = new LibraryIncludeType(LibraryIncludeTypeKeyword.All.FlagsToAdd);

        private readonly static HashSet<LibraryIncludeTypeFlag> Empty = new HashSet<LibraryIncludeTypeFlag>();
        public static readonly LibraryIncludeType None = new LibraryIncludeType();
        public static readonly LibraryIncludeType DefaultSuppress 
            = new LibraryIncludeType(new LibraryIncludeTypeFlag[] 
            {
                LibraryIncludeTypeFlag.ContentFiles,
                LibraryIncludeTypeFlag.Build,
                LibraryIncludeTypeFlag.Analyzer
            });

        public LibraryIncludeType()
        {
            _keywords = Empty;
        }

        private LibraryIncludeType(IEnumerable<LibraryIncludeTypeFlag> flags)
        {
            _keywords = new HashSet<LibraryIncludeTypeFlag>(flags);
        }

        public bool Contains(LibraryIncludeTypeFlag flag)
        {
            return _keywords.Contains(flag);
        }

        public bool Contains(LibraryIncludeType second)
        {
            if (second.Keywords.Count > Keywords.Count)
            {
                return false;
            }

            return second.Keywords.All(key => Contains(key));
        }

        public static LibraryIncludeType Parse(IEnumerable<string> keywords)
        {
            var type = new LibraryIncludeType();
            foreach (var keyword in keywords.Select(LibraryIncludeTypeKeyword.Parse))
            {
                type = type.Combine(keyword.FlagsToAdd, keyword.FlagsToRemove);
            }

            return type;
        }

        public LibraryIncludeType Combine(
            IEnumerable<LibraryIncludeTypeFlag> add,
            IEnumerable<LibraryIncludeTypeFlag> remove)
        {
            return new LibraryIncludeType(_keywords.Except(remove).Union(add));
        }

        public LibraryIncludeType Intersect(LibraryIncludeType second)
        {
            if (Equals(second))
            {
                return this;
            }

            return new LibraryIncludeType(_keywords.Intersect(second.Keywords));
        }

        public LibraryIncludeType Combine(LibraryIncludeType second)
        {
            if (Equals(second))
            {
                return this;
            }

            return new LibraryIncludeType(_keywords.Union(second.Keywords));
        }

        public LibraryIncludeType Except(LibraryIncludeType second)
        {
            if (!second.Keywords.Any(key => Contains(key)))
            {
                return this;
            }

            return new LibraryIncludeType(_keywords.Except(second.Keywords));
        }

        public ISet<LibraryIncludeTypeFlag> Keywords
        {
            get
            {
                return _keywords;
            }
        }

        public override string ToString()
        {
            return string.Join(",", _keywords.Select(kw => kw.ToString()));
        }

        public bool Equals(LibraryIncludeType other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null)
            {
                return false;
            }

            return Keywords.Count == other.Keywords.Count
                && Keywords.SetEquals(other.Keywords);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as LibraryIncludeType);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            foreach (var flag in _keywords)
            {
                combiner.AddObject(flag);
            }

            return combiner.CombinedHash;
        }
    }
}
