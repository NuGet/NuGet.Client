// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Defines a property that can be used in Content Model query patterns
    /// <seealso cref="PatternSet" />
    /// </summary>
    public class ContentPropertyDefinition
    {
        private static readonly Func<object, object, bool> EqualsTest = (left, right) => Equals(left, right);

        public ContentPropertyDefinition(string name)
            : this(name, null, null, null, null, false)
        {
        }

#pragma warning disable RS0016 // Add public types and members to the declared API
        public ContentPropertyDefinition(
            string name,
            MyParserDelegate parser)
            : this(name, parser, null, null, null, false)
        {
        }

        public ContentPropertyDefinition(
            string name,
            Func<object, object, bool> compatibilityTest)
            : this(name, null, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(
            string name,
            MyParserDelegate parser,
            Func<object, object, bool> compatibilityTest)
            : this(name, parser, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name,
            MyParserDelegate parser,
            Func<object, object, bool> compatibilityTest,
            Func<object, object, object, int> compareTest)
            : this(name, parser, compatibilityTest, compareTest, null, false)
        {
        }

        public ContentPropertyDefinition(
            string name,
            IEnumerable<string> fileExtensions)
            : this(name, null, null, null, fileExtensions, false)
        {
        }

        public ContentPropertyDefinition(
            string name,
            MyParserDelegate parser,
            IEnumerable<string> fileExtensions)
            : this(name, parser, null, null, fileExtensions, false)
        {
        }

        public ContentPropertyDefinition(
            string name,
            IEnumerable<string> fileExtensions,
            bool allowSubfolders)
            : this(name, null, null, null, fileExtensions, allowSubfolders)
        {
        }

        public ContentPropertyDefinition(
            string name,
            MyParserDelegate parser,
            IEnumerable<string> fileExtensions,
            bool allowSubfolders)
            : this(name, parser, null, null, fileExtensions, allowSubfolders)
        {
        }

        public ContentPropertyDefinition(
            string name,
            MyParserDelegate parser,
            Func<object, object, bool> compatibilityTest,
            Func<object, object, object, int> compareTest,
            IEnumerable<string> fileExtensions,
            bool allowSubfolders)
        {
            Name = name;
            Parser = parser;
            CompatibilityTest = compatibilityTest ?? EqualsTest;
            CompareTest = compareTest;
            FileExtensions = fileExtensions?.ToList();
            FileExtensionAllowSubFolders = allowSubfolders;
        }

        public string Name { get; }

        public List<string> FileExtensions { get; }

        public bool FileExtensionAllowSubFolders { get; }

#pragma warning restore RS0016 // Add public types and members to the declared API
#pragma warning disable RS0016 // Add public types and members to the declared API
        public delegate object MyParserDelegate(ReadOnlyMemory<char> input, PatternTable table);

        public MyParserDelegate Parser { get; }
#pragma warning restore RS0016 // Add public types and members to the declared API

#pragma warning disable RS0016 // Add public types and members to the declared API
        public virtual bool TryLookup(ReadOnlyMemory<char> name, PatternTable table, out object value)
#pragma warning restore RS0016 // Add public types and members to the declared API
        {
            if (name.IsEmpty)
            {
                value = null;
                return false;
            }

            if (FileExtensions?.Count > 0)
            {
                if (FileExtensionAllowSubFolders || !ContainsSlash(name))
                {
                    foreach (var fileExtension in FileExtensions)
                    {
                        return TryGetFileName(name, fileExtension, out value);
                    }
                }
            }

            if (Parser != null)
            {
                value = Parser.Invoke(name, table);
                if (value != null)
                {
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static bool TryGetFileName(ReadOnlyMemory<char> name, string fileExtension, out object value)
        {
            value = null;
            ReadOnlySpan<char> span = name.Span;
            ReadOnlySpan<char> extensionSpan = fileExtension.AsSpan();

            if (span.Length >= extensionSpan.Length &&
                span.Slice(span.Length - extensionSpan.Length).Equals(extensionSpan, StringComparison.OrdinalIgnoreCase))
            {
                value = new string(span);
                return true;
            }

            return false;
        }


        private static bool ContainsSlash(ReadOnlyMemory<char> name)
        {
            ReadOnlySpan<char> span = name.Span;
            foreach (var ch in span)
            {
                if (ch == '/' || ch == '\\')
                {
                    return true;
                }
            }

            return false;
        }

        public Func<object, object, bool> CompatibilityTest { get; }

        /// <summary>
        /// Find the nearest compatible candidate.
        /// </summary>
        public Func<object, object, object, int> CompareTest { get; }

        public virtual bool IsCriteriaSatisfied(object critieriaValue, object candidateValue)
        {
            return CompatibilityTest.Invoke(critieriaValue, candidateValue);
        }

        public virtual int Compare(object criteriaValue, object candidateValue1, object candidateValue2)
        {
            var betterCoverageFromValue1 = IsCriteriaSatisfied(candidateValue1, candidateValue2);
            var betterCoverageFromValue2 = IsCriteriaSatisfied(candidateValue2, candidateValue1);
            if (betterCoverageFromValue1 && !betterCoverageFromValue2)
            {
                return -1;
            }
            if (betterCoverageFromValue2 && !betterCoverageFromValue1)
            {
                return 1;
            }

            if (CompareTest != null)
            {
                // In the case of a tie call the external compare test to determine the nearest candidate.
                return CompareTest.Invoke(criteriaValue, candidateValue1, candidateValue2);
            }

            // No tie breaker was provided.
            return 0;
        }
    }
}
