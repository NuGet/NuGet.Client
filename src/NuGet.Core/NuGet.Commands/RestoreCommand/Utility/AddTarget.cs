// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ContentModel;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Client;

namespace NuGet.Commands
{
    internal class AddTarget
    {
        static string TargetFrameworkMoniker = "tfm";
        static string RuntimeIdentifier = "rid";
        static string AnyValue = "any";
        static string ManagedAssembly = "assembly";
        static string Locale = "locale";
        static string MSBuild = "msbuild";
        static string SatelliteAssembly = "satelliteAssembly";
        static string CodeLanguage = "codeLanguage";
        LockFileTargetLibrary LockFileLib { get; set; }

        public List<(ContentItem Item, Asset Asset)> ContentFilesItems { get; set; }
        List<Asset> Paths { get; set; }
        private string _currenPath;
        int StartIndex { get; set; }
        private static readonly FrameworkReducer FrameworkReducer = new();
        private Dictionary<string, NuGetFramework> _frameworkCache
            = new Dictionary<string, NuGetFramework>(StringComparer.Ordinal);
        private RuntimeGraph _runtimeGraph;
        Dictionary<string, ContentPropertyDefinition> _properties;
        private static readonly ContentPropertyDefinition LocaleProperty = new ContentPropertyDefinition(Locale,
            parser: Locale_Parser);

        private static readonly ContentPropertyDefinition AnyProperty = new ContentPropertyDefinition(
            AnyValue,
            parser: (o, t) => o); // Identity parser, all strings are valid for any
        private static readonly ContentPropertyDefinition AssemblyProperty = new ContentPropertyDefinition(ManagedAssembly,
            parser: AllowEmptyFolderParser,
            fileExtensions: new[] { ".dll", ".winmd", ".exe" });
        private static readonly ContentPropertyDefinition MSBuildProperty = new ContentPropertyDefinition(MSBuild,
            parser: AllowEmptyFolderParser,
            fileExtensions: new[] { ".targets", ".props" });
        private static readonly ContentPropertyDefinition SatelliteAssemblyProperty = new ContentPropertyDefinition(SatelliteAssembly,
            parser: AllowEmptyFolderParser,
            fileExtensions: new[] { ".resources.dll" });

        private static readonly ContentPropertyDefinition CodeLanguageProperty = new ContentPropertyDefinition(
            CodeLanguage,
            parser: CodeLanguage_Parser);

        internal AddTarget(RuntimeGraph runtimeGraph, LockFileTargetLibrary lockFileLibrary, List<Asset> paths, int startIndex)
        {
            ContentFilesItems = new List<(ContentItem Item, Asset Asset)>();
            LockFileLib = lockFileLibrary;
            Paths = paths;
            StartIndex = startIndex;
            _runtimeGraph = runtimeGraph;
            _properties = new Dictionary<string, ContentPropertyDefinition>();
            _properties[AnyProperty.Name] = AnyProperty;
            _properties[AssemblyProperty.Name] = AssemblyProperty;
            _properties[LocaleProperty.Name] = LocaleProperty;
            _properties[MSBuildProperty.Name] = MSBuildProperty;
            _properties[SatelliteAssemblyProperty.Name] = SatelliteAssemblyProperty;
            _properties[CodeLanguageProperty.Name] = CodeLanguageProperty;

            _properties[RuntimeIdentifier] = new ContentPropertyDefinition(
                RuntimeIdentifier,
                parser: (o, t) => o, // Identity parser, all strings are valid runtime ids :)
                compatibilityTest: RuntimeIdentifier_CompatibilityTest);

            _properties[TargetFrameworkMoniker] = new ContentPropertyDefinition(
                TargetFrameworkMoniker,
                parser: TargetFrameworkName_Parser,
                compatibilityTest: TargetFrameworkName_CompatibilityTest,
                compareTest: TargetFrameworkName_NearestCompareTest);

        }
        internal void Add()
        {
            foreach (Asset asset in Paths)
            {
                _currenPath = asset.Path;
                int delimiterIndex = FindNextDelimeter(StartIndex);
                string pathToken = _currenPath.Substring(StartIndex, delimiterIndex - StartIndex);
                switch (pathToken)
                {
                    case "contentFiles":
                        AddContentFiles(delimiterIndex + 1, asset);
                        break;
                    default:
                        break;
                }
            }
        }

        internal int FindNextDelimeter(int startIndex)
        {
            for (var i = startIndex; i < _currenPath.Length; i++)
            {
                if (_currenPath[i] == '/' || _currenPath[i] == '\\')
                {
                    return i;
                }
            }
            return _currenPath.Length;

        }

        internal void AddContentFiles(int startIndex, Asset asset)
        {
            //This will match the pattern: contentFiles/{codeLanguage}/{tfm}/{any?}
            ContentItem item = new ContentItem
            {
                Path = _currenPath
            };
            //must match codeLanguage
            int delimiterIndex = FindNextDelimeter(startIndex);
            string pathToken = _currenPath.Substring(startIndex, delimiterIndex - startIndex);
            if (!MatchCodeLanguageProperty(item, null, pathToken))
            {
                return;
            }
            //must match tfm
            startIndex = delimiterIndex + 1;
            delimiterIndex = FindNextDelimeter(startIndex);
            pathToken = _currenPath.Substring(startIndex, delimiterIndex - startIndex);
            if (!MatchtfmProperty(item, null, pathToken))
            {
                return;
            }
            //Match any? 
            startIndex = delimiterIndex + 1;
            delimiterIndex = _currenPath.Length;
            pathToken = _currenPath.Substring(startIndex, delimiterIndex - startIndex);
            if (!MatchOnlyanyProperty(item, null, pathToken))
            {
                return;
            }
            //This path maches a contentfile and it is represented by ContentItem: item
            ContentFilesItems.Add((item, asset));
        }

        internal bool MatchCodeLanguageProperty(ContentItem item, PatternTable table, string pathToken)
        {
            //Match codeLanguage
            object value;
            if (CodeLanguageProperty.TryLookup(pathToken, table, out value))
            {
                item.Properties.Add(CodeLanguage, value);
                return true;
            }
            return false;
        }
        internal bool MatchtfmProperty(ContentItem item, PatternTable table, string pathToken)
        {
            //Match codeLanguage
            object value;
            if (_properties[TargetFrameworkMoniker].TryLookup(pathToken, table, out value))
            {
                item.Properties.Add("tfm_raw", pathToken);
                item.Properties.Add(TargetFrameworkMoniker, value);
                return true;
            }
            return false;
        }

        internal bool MatchOnlyanyProperty(ContentItem item, PatternTable table, string pathToken)
        {
            //Match only any. Do not add to contentItem
            object value;
            if (_properties[AnyValue].TryLookup(pathToken, table, out value))
            {
                return true;
            }
            return false;
        }
        private bool RuntimeIdentifier_CompatibilityTest(object criteria, object available)
        {
            if (_runtimeGraph == null)
            {
                return Equals(criteria, available);
            }
            else
            {
                var criteriaRid = criteria as string;
                var availableRid = available as string;

                if (criteriaRid != null
                    && availableRid != null)
                {
                    return _runtimeGraph.AreCompatible(criteriaRid, availableRid);
                }
                return false;
            }
        }

        private static object CodeLanguage_Parser(string name, PatternTable table)
        {
            if (table != null)
            {
                object val;
                if (table.TryLookup(CodeLanguage, name, out val))
                {
                    return val;
                }
            }

            // Code language values must be alpha numeric.
            return name.All(c => char.IsLetterOrDigit(c)) ? name : null;
        }

        private static object Locale_Parser(string name, PatternTable table)
        {
            if (table != null)
            {
                object val;
                if (table.TryLookup(Locale, name, out val))
                {
                    return val;
                }
            }

            if (name.Length == 2)
            {
                return name;
            }
            else if (name.Length >= 4 && name[2] == '-')
            {
                return name;
            }

            return null;
        }

        private object TargetFrameworkName_Parser(
            string name,
            PatternTable table)
        {
            object obj = null;

            // Check for replacements
            if (table != null)
            {
                if (table.TryLookup(TargetFrameworkMoniker, name, out obj))
                {
                    return obj;
                }
            }

            // Check the cache for an exact match
            if (!string.IsNullOrEmpty(name))
            {
                NuGetFramework cachedResult;
                if (!_frameworkCache.TryGetValue(name, out cachedResult))
                {
                    // Parse and add the framework to the cache
                    cachedResult = TargetFrameworkName_ParserCore(name);
                    _frameworkCache.Add(name, cachedResult);
                }

                return cachedResult;
            }

            // Let the framework parser handle null/empty and create the error message.
            return TargetFrameworkName_ParserCore(name);
        }

        private static NuGetFramework TargetFrameworkName_ParserCore(string name)
        {
            var result = NuGetFramework.ParseFolder(name);

            if (!result.IsUnsupported)
            {
                return result;
            }

            // Everything should be in the folder format, but fallback to
            // full parsing for legacy support.
            result = NuGetFramework.ParseFrameworkName(name, DefaultFrameworkNameProvider.Instance);

            if (!result.IsUnsupported)
            {
                return result;
            }

            // For unknown frameworks return the name as is.
            return new NuGetFramework(name, FrameworkConstants.EmptyVersion);
        }

        private static object AllowEmptyFolderParser(string s, PatternTable table)
        {
            // Accept "_._" as a pseudo-assembly
            return PackagingCoreConstants.EmptyFolder.Equals(s, StringComparison.Ordinal) ? s : null;
        }

        private static bool TargetFrameworkName_CompatibilityTest(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null
                && availableFrameworkName != null)
            {
                // We only consider 'any' matches when the criteria explicitly asks for them
                if (criteriaFrameworkName.IsAny
                    && availableFrameworkName.IsAny)
                {
                    return true;
                }
                else if (Object.Equals(AnyFramework.AnyFramework, availableFrameworkName))
                {
                    // If the convention does not contain a TxM it will use AnyFramework, this is
                    // always compatible with other frameworks.
                    return true;
                }
                else if (criteriaFrameworkName.IsAny
                         || availableFrameworkName.IsAny)
                {
                    // Otherwise, ignore 'any' framework values
                    return false;
                }

                return NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(criteriaFrameworkName, availableFrameworkName);
            }

            return false;
        }

        private static int TargetFrameworkName_NearestCompareTest(object projectFramework, object criteria, object available)
        {
            var projectFrameworkName = projectFramework as NuGetFramework;
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null
                && availableFrameworkName != null
                && projectFrameworkName != null)
            {
                // If the frameworks are the same this can be skipped
                if (!criteriaFrameworkName.Equals(availableFrameworkName))
                {
                    var frameworks = new NuGetFramework[] { criteriaFrameworkName, availableFrameworkName };

                    // Find the nearest compatible framework to the project framework.
                    var nearest = FrameworkReducer.GetNearest(projectFrameworkName, frameworks);

                    if (criteriaFrameworkName.Equals(nearest))
                    {
                        return -1;
                    }

                    if (availableFrameworkName.Equals(nearest))
                    {
                        return 1;
                    }
                }
            }

            return 0;
        }
    }
}
