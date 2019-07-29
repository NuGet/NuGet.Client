// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    internal class DependeciesGroupsForEachTFMRule : IPackageRule
    {
        private static readonly NuGetFramework Net00 = NuGetFramework.Parse("net00");

        public string MessageFormat => AnalysisResources.DependenciesGroupsForEachTFMHasNoExactMatch;

        public string CompatMatchFoundWarningMessageFormat => AnalysisResources.DependenciesGroupsForEachTFMHasCompatMatch;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var files = builder.GetFiles().
                Where(t => PathUtility.GetPathWithDirectorySeparator(t).Count(m => m == Path.DirectorySeparatorChar) > 1);
            var packageNuspec = ExtractTFMsFromNuspec(builder.GetNuspec());
            var (compatNotExactMatches, noExactMatchesFromFile, noExactMatchesFromNuspec) = CatagoriseTFMs(files, packageNuspec);
            return GenerateWarnings(compatNotExactMatches, noExactMatchesFromFile, noExactMatchesFromNuspec);
        }

        internal IEnumerable<PackagingLogMessage> GenerateWarnings(HashSet<NuGetFramework> compatNotExactMatches, HashSet<NuGetFramework> noExactMatchesFromFile, HashSet<NuGetFramework> noExactMatchesFromNuspec)
        {
            (string noExactMatchString, string compatMatchString) = GenerateWarningString(noExactMatchesFromFile, noExactMatchesFromNuspec, (ICollection<NuGetFramework>)compatNotExactMatches);

            var issues = new List<PackagingLogMessage>();

            if (noExactMatchesFromFile.Count != 0 || noExactMatchesFromNuspec.Count != 0)
            {
                issues.Add(PackagingLogMessage.CreateWarning(string.Join("\n", MessageFormat, noExactMatchString),
                    NuGetLogCode.NU5128));
            }

            if (compatNotExactMatches.Count != 0)
            {
                issues.Add(PackagingLogMessage.CreateWarning(string.Join("\n", CompatMatchFoundWarningMessageFormat, compatMatchString),
                    NuGetLogCode.NU5130));
            }

            if (issues.Count != 0)
            {
                return issues;
            }

            return Array.Empty<PackagingLogMessage>();
        }

        internal (HashSet<NuGetFramework>, HashSet<NuGetFramework>, HashSet<NuGetFramework>) CatagoriseTFMs(IEnumerable<string> files, IEnumerable<NuGetFramework> tfmsFromNuspec)
        {
            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            Func<object, object, bool> isCompatible = managedCodeConventions.Properties["tfm"].CompatibilityTest;
            var collection = new ContentItemCollection();
            collection.Load(files);

            var libItems = ContentExtractor.GetContentForPattern(collection, managedCodeConventions.Patterns.CompileLibAssemblies);
            var refItems = ContentExtractor.GetContentForPattern(collection, managedCodeConventions.Patterns.CompileRefAssemblies);

            var tfmsFromFilesSet = new HashSet<NuGetFramework>();
            tfmsFromFilesSet.AddRange(ContentExtractor.GetGroupFrameworks(libItems));
            tfmsFromFilesSet.AddRange(ContentExtractor.GetGroupFrameworks(refItems));

            var tfmsFromFiles = tfmsFromFilesSet.Where(t => t != Net00).ToList();

            var noExactMatchesFromFile = new HashSet<NuGetFramework>();
            var noExactMatchesFromNuspec = new HashSet<NuGetFramework>();
            var compatNotExactMatches = new HashSet<NuGetFramework>();

            foreach (var fileTFM in tfmsFromFiles)
            {
                if (!tfmsFromNuspec.Contains(fileTFM))
                {
                    noExactMatchesFromFile.Add(fileTFM);
                }
            }

            foreach (var nuspecTFM in tfmsFromNuspec)
            {
                if (!tfmsFromFiles.Contains(nuspecTFM))
                {
                    noExactMatchesFromNuspec.Add(nuspecTFM);
                }
            }

            foreach (var fileTFM in noExactMatchesFromFile)
            {
                foreach (var nuspecTFM in noExactMatchesFromNuspec)
                {
                    if (isCompatible(fileTFM, nuspecTFM))
                    {
                        compatNotExactMatches.Add(fileTFM);
                    }
                }
            }

            if (compatNotExactMatches.Count != 0)
            {
                foreach(var compatTFM in compatNotExactMatches)
                {
                    noExactMatchesFromFile.Remove(compatTFM);
                }
            }

            return (compatNotExactMatches, noExactMatchesFromFile, noExactMatchesFromNuspec);
        }
        
        internal (string noExactMatchString, string compatMatchString) GenerateWarningString(ICollection<NuGetFramework> noExactMatchesFromFile,
                ICollection<NuGetFramework> noExactMatchesFromNuspec, ICollection<NuGetFramework> compatNotExactMatches)
        {
            var noExactMatchString = new StringBuilder();
            var compatMatchString = new StringBuilder();

            var beginning = AnalysisResources.DependenciesGroupsForEachTFMBeginningToNuspec;
            var ending = " " + AnalysisResources.DependenciesGroupsForEachTFMEndingToNuspec;

            var firstElement = new StringBuilder();

            if (noExactMatchesFromFile.Count != 0)
            {
                firstElement.Append(beginning);
                firstElement.Append(noExactMatchesFromFile.Select(t => t.GetFrameworkString()).First());

                noExactMatchString.Append(noExactMatchesFromFile.Count > 1 ? beginning +
                    string.Join(ending + beginning,
                        noExactMatchesFromFile.Select(t => t.GetFrameworkString()).ToArray()) :
                        firstElement.ToString());
                noExactMatchString.Append(ending);
            }

            if (noExactMatchesFromNuspec.Count != 0)
            {
                beginning = AnalysisResources.DependenciesGroupsForEachTFMBeginningToFiles;
                ending = " " + AnalysisResources.DependenciesGroupsForEachTFMEndingToFile;
                firstElement.Clear();
                firstElement.Append(beginning);
                firstElement.Append(noExactMatchesFromNuspec.Select(t => t.GetShortFolderName()).First());

                noExactMatchString.Append(noExactMatchesFromNuspec.Count > 1 ? beginning +
                    string.Join(ending + beginning,
                        noExactMatchesFromNuspec.Select(t => t.GetShortFolderName()).ToArray()) :
                    firstElement.ToString());
                noExactMatchString.Append(ending);
            }

            if (compatNotExactMatches.Count != 0)
            {
                beginning = AnalysisResources.DependenciesGroupsForEachTFMBeginningToNuspec;
                ending = " " + AnalysisResources.DependenciesGroupsForEachTFMEndingToNuspec;
                firstElement.Clear();
                firstElement.Append(beginning);
                firstElement.Append(compatNotExactMatches.Select(t => t.GetFrameworkString()).First());

                compatMatchString.Append(compatNotExactMatches.Count > 1 ? beginning +
                    string.Join(ending + beginning,
                        compatNotExactMatches.Select(t => t.GetFrameworkString()).ToArray()):
                    firstElement.ToString());
                compatMatchString.Append(ending);
            }

            return (noExactMatchString.ToString(), compatMatchString.ToString());
        }

        private static IEnumerable<NuGetFramework> ExtractTFMsFromNuspec(Stream packageNuspec)
        {
            var tfmsFromNuspec = new List<NuGetFramework>();
            var stream = new NuspecReader(packageNuspec);
            var nuspec = stream.Xml;
            if (nuspec != null)
            {
                XNamespace name = nuspec.Root.Name.Namespace;
                return nuspec.Descendants(XName.Get("{" + name.NamespaceName + "}dependencies")).Elements().Attributes("targetFramework").Select(f => NuGetFramework.Parse(f.Value)).ToList();
            }
            return new List<NuGetFramework>();
        }
    }
}
