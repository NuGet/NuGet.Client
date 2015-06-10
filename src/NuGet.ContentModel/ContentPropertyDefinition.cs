// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace NuGet.ContentModel
{
    /// <summary>
    /// Defines a property that can be used in Content Model query patterns
    /// <seealso cref="PatternSet" />
    /// </summary>
    public class ContentPropertyDefinition
    {
        public ContentPropertyDefinition(string name)
            : this(name, null, null, null, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, IDictionary<string, object> table)
            : this(name, table, null, null, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, Func<string, object> parser)
            : this(name, null, parser, null, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, Func<object, object, bool> compatibilityTest)
            : this(name, null, null, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, IDictionary<string, object> table, Func<string, object> parser)
            : this(name, table, parser, null, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, IDictionary<string, object> table, Func<object, object, bool> compatibilityTest)
            : this(name, table, null, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, Func<string, object> parser, Func<object, object, bool> compatibilityTest)
            : this(name, null, parser, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name, IDictionary<string, object> table, Func<string, object> parser, Func<object, object, bool> compatibilityTest)
            : this(name, table, parser, compatibilityTest, null, null, false)
        {
        }

        public ContentPropertyDefinition(string name,
            IDictionary<string, object> table,
            Func<string, object> parser, 
            Func<object, object, bool> compatibilityTest,
            Func<object, object, object, int> compareTest)
            : this(name, table, parser, compatibilityTest, compareTest, null, false)
        {
        }

        public ContentPropertyDefinition(string name, IEnumerable<string> fileExtensions)
            : this(name, null, null, null, null, fileExtensions, false)
        {
        }

        public ContentPropertyDefinition(string name, Func<string, object> parser, IEnumerable<string> fileExtensions)
            : this(name, null, parser, null, null, fileExtensions, false)
        {
        }

        public ContentPropertyDefinition(string name, IEnumerable<string> fileExtensions, bool allowSubfolders)
            : this(name, null, null, null, null, fileExtensions, allowSubfolders)
        {
        }

        public ContentPropertyDefinition(string name, Func<string, object> parser, IEnumerable<string> fileExtensions, bool allowSubfolders)
            : this(name, null, parser, null, null, fileExtensions, allowSubfolders)
        {
        }

        public ContentPropertyDefinition(
            string name,
            IDictionary<string, object> table,
            Func<string, object> parser,
            Func<object, object, bool> compatibilityTest,
            Func<object, object, object, int> compareTest,
            IEnumerable<string> fileExtensions,
            bool allowSubfolders)
        {
            Name = name;

            if (table == null)
            {
                table = new Dictionary<string, object>();
            }
            else
            {
                table = new Dictionary<string, object>(table); // Copies the contents of the dictionary... though we can't control the mutability of the objects :(
            }
            Table = new ReadOnlyDictionary<string, object>(table); // Wraps the dictionary in a read-only container. Does NOT copy!

            Parser = parser;
            CompatibilityTest = compatibilityTest ?? Equals;
            CompareTest = compareTest;
            FileExtensions = (fileExtensions ?? Enumerable.Empty<string>()).ToList();
            FileExtensionAllowSubFolders = allowSubfolders;
        }

        public string Name { get; }

        public IDictionary<string, object> Table { get; }

        public List<string> FileExtensions { get; }

        public bool FileExtensionAllowSubFolders { get; }

        public Func<string, object> Parser { get; }

        public virtual bool TryLookup(string name, out object value)
        {
            if (name == null)
            {
                value = null;
                return false;
            }

            if (Table != null
                && Table.TryGetValue(name, out value))
            {
                return true;
            }

            if (FileExtensions != null
                && FileExtensions.Any())
            {
                if (FileExtensionAllowSubFolders == true
                    || name.IndexOfAny(new[] { '/', '\\' }) == -1)
                {
                    foreach (var fileExtension in FileExtensions)
                    {
                        if (name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                        {
                            value = name;
                            return true;
                        }
                    }
                }
            }

            if (Parser != null)
            {
                value = Parser.Invoke(name);
                if (value != null)
                {
                    return true;
                }
            }

            value = null;
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
