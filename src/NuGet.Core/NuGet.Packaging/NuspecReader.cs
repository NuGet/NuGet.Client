// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    /// <summary>
    /// Reads .nuspec files
    /// </summary>
    public class NuspecReader : NuspecCoreReaderBase
    {
        // node names
        private const string Dependencies = "dependencies";
        private const string Group = "group";
        private const string TargetFramework = "targetFramework";
        private const string Dependency = "dependency";
        private const string References = "references";
        private const string Reference = "reference";
        private const string File = "file";
        private const string FrameworkAssemblies = "frameworkAssemblies";
        private const string FrameworkAssembly = "frameworkAssembly";
        private const string AssemblyName = "assemblyName";
        private const string Language = "language";
        private const string ContentFiles = "contentFiles";
        private const string Files = "files";
        private const string BuildAction = "buildAction";
        private const string Flatten = "flatten";
        private const string CopyToOutput = "copyToOutput";
        private const string IncludeFlags = "include";
        private const string ExcludeFlags = "exclude";
        private static readonly char[] CommaArray = new char[] { ',' };
        private readonly IFrameworkNameProvider _frameworkProvider;

        /// <summary>
        /// Nuspec file reader
        /// </summary>
        /// <param name="stream">Nuspec file stream.</param>
        public NuspecReader(Stream stream)
            : this(stream, DefaultFrameworkNameProvider.Instance)
        {

        }

        /// <summary>
        /// Nuspec file reader
        /// </summary>
        /// <param name="xml">Nuspec file xml data.</param>
        public NuspecReader(XDocument xml)
            : this(xml, DefaultFrameworkNameProvider.Instance)
        {

        }

        /// <summary>
        /// Nuspec file reader
        /// </summary>
        /// <param name="stream">Nuspec file stream.</param>
        /// <param name="frameworkProvider">Framework mapping provider for NuGetFramework parsing.</param>
        public NuspecReader(Stream stream, IFrameworkNameProvider frameworkProvider)
            : base(stream)
        {
            _frameworkProvider = frameworkProvider;
        }

        /// <summary>
        /// Nuspec file reader
        /// </summary>
        /// <param name="xml">Nuspec file xml data.</param>
        /// <param name="frameworkProvider">Framework mapping provider for NuGetFramework parsing.</param>
        public NuspecReader(XDocument xml, IFrameworkNameProvider frameworkProvider)
            : base(xml)
        {
            _frameworkProvider = frameworkProvider;
        }

        /// <summary>
        /// Read package dependencies for all frameworks
        /// </summary>
        public IEnumerable<PackageDependencyGroup> GetDependencyGroups()
        {
            var ns = MetadataNode.GetDefaultNamespace().NamespaceName;

            var groupFound = false;

            foreach (var depGroup in MetadataNode.Elements(XName.Get(Dependencies, ns)).Elements(XName.Get(Group, ns)))
            {
                groupFound = true;

                var groupFramework = GetAttributeValue(depGroup, TargetFramework);

                var packages = new List<PackageDependency>();

                foreach (var depNode in depGroup.Elements(XName.Get(Dependency, ns)))
                {
                    VersionRange range = null;

                    var rangeNode = GetAttributeValue(depNode, Version);

                    if (!String.IsNullOrEmpty(rangeNode))
                    {
                        VersionRange.TryParse(rangeNode, out range);

                        Debug.Assert(range != null, "Unable to parse range: " + rangeNode);
                    }

                    var includeFlags = GetFlags(GetAttributeValue(depNode, IncludeFlags));
                    var excludeFlags = GetFlags(GetAttributeValue(depNode, ExcludeFlags));

                    var dependency = new PackageDependency(
                        GetAttributeValue(depNode, Id), 
                        range,
                        includeFlags,
                        excludeFlags);

                    packages.Add(dependency);
                }

                var framework = String.IsNullOrEmpty(groupFramework) 
                    ? NuGetFramework.AnyFramework 
                    : NuGetFramework.Parse(groupFramework, _frameworkProvider);

                yield return new PackageDependencyGroup(framework, packages);
            }

            // legacy behavior
            if (!groupFound)
            {
                var depNodes = MetadataNode.Elements(XName.Get(Dependencies, ns))
                    .Elements(XName.Get(Dependency, ns));

                var packages = new List<PackageDependency>();

                foreach (var depNode in depNodes)
                {
                    VersionRange range = null;

                    var rangeNode = GetAttributeValue(depNode, Version);

                    if (!String.IsNullOrEmpty(rangeNode))
                    {
                        VersionRange.TryParse(rangeNode, out range);

                        Debug.Assert(range != null, "Unable to parse range: " + rangeNode);
                    }

                    packages.Add(new PackageDependency(GetAttributeValue(depNode, Id), range));
                }

                if (packages.Any())
                {
                    yield return new PackageDependencyGroup(NuGetFramework.AnyFramework, packages);
                }
            }

            yield break;
        }

        /// <summary>
        /// Reference item groups
        /// </summary>
        public IEnumerable<FrameworkSpecificGroup> GetReferenceGroups()
        {
            var ns = MetadataNode.GetDefaultNamespace().NamespaceName;

            var groupFound = false;

            foreach (var group in MetadataNode.Elements(XName.Get(References, ns)).Elements(XName.Get(Group, ns)))
            {
                groupFound = true;

                var groupFramework = GetAttributeValue(group, TargetFramework);

                var items = group.Elements(XName.Get(Reference, ns)).Select(n => GetAttributeValue(n, File)).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                var framework = String.IsNullOrEmpty(groupFramework) ? NuGetFramework.AnyFramework : NuGetFramework.Parse(groupFramework, _frameworkProvider);

                yield return new FrameworkSpecificGroup(framework, items);
            }

            // pre-2.5 flat list of references, this should only be used if there are no groups
            if (!groupFound)
            {
                var items = MetadataNode.Elements(XName.Get(References, ns))
                    .Elements(XName.Get(Reference, ns)).Select(n => GetAttributeValue(n, File)).Where(n => !String.IsNullOrEmpty(n)).ToArray();

                if (items.Length > 0)
                {
                    yield return new FrameworkSpecificGroup(NuGetFramework.AnyFramework, items);
                }
            }

            yield break;
        }

        /// <summary>
        /// Framework reference groups
        /// </summary>
        public IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups()
        {
            var results = new List<FrameworkSpecificGroup>();

            var ns = Xml.Root.GetDefaultNamespace().NamespaceName;

            var groups = new Dictionary<NuGetFramework, HashSet<string>>(new NuGetFrameworkFullComparer());

            foreach (var group in MetadataNode.Elements(XName.Get(FrameworkAssemblies, ns)).Elements(XName.Get(FrameworkAssembly, ns))
                .GroupBy(n => GetAttributeValue(n, TargetFramework)))
            {
                // Framework references may have multiple comma delimited frameworks
                var frameworks = new List<NuGetFramework>();

                // Empty frameworks go under Any
                if (String.IsNullOrEmpty(group.Key))
                {
                    frameworks.Add(NuGetFramework.AnyFramework);
                }
                else
                {
                    foreach (var fwString in group.Key.Split(CommaArray, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!String.IsNullOrEmpty(fwString))
                        {
                            frameworks.Add(NuGetFramework.Parse(fwString.Trim(), _frameworkProvider));
                        }
                    }
                }

                // apply items to each framework
                foreach (var framework in frameworks)
                {
                    HashSet<string> items = null;
                    if (!groups.TryGetValue(framework, out items))
                    {
                        items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        groups.Add(framework, items);
                    }

                    // Merge items and ignore duplicates
                    items.UnionWith(group.Select(item => GetAttributeValue(item, AssemblyName)).Where(item => !String.IsNullOrEmpty(item)));
                }
            }

            // Sort items to make this deterministic for the caller
            foreach (var framework in groups.Keys.OrderBy(e => e, new NuGetFrameworkSorter()))
            {
                var group = new FrameworkSpecificGroup(framework, groups[framework].OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

                results.Add(group);
            }

            return results;
        }

        /// <summary>
        /// Package language
        /// </summary>
        public string GetLanguage()
        {
            var node = MetadataNode.Elements(XName.Get(Language, MetadataNode.GetDefaultNamespace().NamespaceName)).FirstOrDefault();
            return node == null ? null : node.Value;
        }

        /// <summary>
        /// Build action groups
        /// </summary>
        public IEnumerable<ContentFilesEntry> GetContentFiles()
        {
            var ns = MetadataNode.GetDefaultNamespace().NamespaceName;

            foreach (var filesNode in MetadataNode
                .Elements(XName.Get(ContentFiles, ns))
                .Elements(XName.Get(Files, ns)))
            {
                var include = GetAttributeValue(filesNode, "include");

                if (include == null)
                {
                    // Invalid include
                    var message = Strings.FormatInvalidNuspecEntry(
                        filesNode.ToString().Trim(),
                        GetIdentity());

                    throw new PackagingException(message);
                }

                var exclude = GetAttributeValue(filesNode, "exclude");

                if (string.IsNullOrEmpty(exclude))
                {
                    exclude = null;
                }

                var buildAction = GetAttributeValue(filesNode, BuildAction);
                var flatten = AttributeAsNullableBool(filesNode, Flatten);
                var copyToOutput = AttributeAsNullableBool(filesNode, CopyToOutput);

                yield return new ContentFilesEntry(include, exclude, buildAction, copyToOutput, flatten);
            }

            yield break;
        }

        private static bool? AttributeAsNullableBool(XElement element, string attributeName)
        {
            bool? result = null;

            var attributeValue = GetAttributeValue(element, attributeName);

            if (attributeValue != null)
            {
                if (Boolean.TrueString.Equals(attributeValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = true;
                }
                else if (Boolean.FalseString.Equals(attributeValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = false;
                }
                else
                {
                    var message = string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.InvalidNuspecEntry,
                            element.ToString().Trim());

                    throw new PackagingException(message);
                }
            }

            return result;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            var attribute = element.Attribute(XName.Get(attributeName));
            return attribute == null ? null : attribute.Value;
        }

        private static readonly List<string> EmptyList = new List<string>();
        private static List<string> GetFlags(string flags)
        {
            if (string.IsNullOrEmpty(flags))
            {
                return EmptyList;
            }

            var set = new HashSet<string>(
                flags.Split(CommaArray, StringSplitOptions.RemoveEmptyEntries)
                    .Select(flag => flag.Trim()),
                StringComparer.OrdinalIgnoreCase);

            return set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
