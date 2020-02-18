// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Client;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    internal class UpholdBuildConventionRule : IPackageRule
    {
        private static ManagedCodeConventions ManagedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
        string[] _folders = new string[]
        {
            PackagingConstants.Folders.Build,
            PackagingConstants.Folders.BuildTransitive,
            PackagingConstants.Folders.BuildCrossTargeting
        };

        public string MessageFormat => AnalysisResources.BuildConventionIsViolatedWarning;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var warnings = new List<PackagingLogMessage>();
            foreach (var folder in _folders)
            {
                var files = builder.GetFiles().Where(t => t.StartsWith(folder));
                var packageId = builder.NuspecReader.GetId();
                var conventionViolators = IdentifyViolators(files, packageId);
                warnings.AddRange(GenerateWarnings(conventionViolators));
            }
            return warnings;
        }

        internal IEnumerable<PackagingLogMessage> GenerateWarnings(IEnumerable<ConventionViolator> conventionViolators)
        {

            var issues = new List<PackagingLogMessage>();
            foreach (var folder in _folders)
            {
                var warningMessage = new StringBuilder();
                var currentConventionViolators = conventionViolators.Where(t => t.ExpectedPath.StartsWith(folder + '/'));
                foreach (var conViolator in currentConventionViolators)
                {
                    warningMessage.AppendLine(string.Format(MessageFormat, conViolator.Extension, conViolator.Path, conViolator.ExpectedPath));
                }

                if (warningMessage.ToString() != string.Empty)
                {
                    issues.Add(PackagingLogMessage.CreateWarning(warningMessage.ToString(), NuGetLogCode.NU5129));
                }
            }
            return issues;
        }

        internal IEnumerable<ConventionViolator> IdentifyViolators(IEnumerable<string> files, string packageId)
        {
            var groupedFiles = files.GroupBy(t => GetFolderName(t));
            var conventionViolators = new List<ConventionViolator>();

            if (files.Count() != 0)
            {
                foreach (var group in groupedFiles)
                {
                    foreach (var extension in ManagedCodeConventions.Properties["msbuild"].FileExtensions)
                    {
                        var correctFilePattern = group.Key + packageId + extension;
                        var hasFiles = group.Any(t => t.EndsWith(extension));
                        var correctFiles = group.Where(t => t.Equals(correctFilePattern, StringComparison.OrdinalIgnoreCase));

                        if (correctFiles.Count() == 0 && hasFiles)
                        {
                            conventionViolators.Add(new ConventionViolator(group.First(), extension, correctFilePattern));
                        }
                    }
                }
            }
            return conventionViolators;
        }

        internal string GetFolderName(string filePath)
        {
            var hi = NuGetFramework.ParseFolder(filePath.Split('/')[1]).GetShortFolderName();
            if (hi == "unsupported")
            {
                return filePath.Split('/')[0] + '/';
            }
            return filePath.Split('/')[0] + '/' + hi + '/';
        }

        internal class ConventionViolator
        {
            public string Path { get; }

            public string Extension { get; }

            public string ExpectedPath { get; }

            public ConventionViolator(string filePath, string extension, string expectedFile)
            {
                Path = filePath.Replace(filePath.Split('/')[filePath.Count(p => p == '/')], string.Empty);
                Extension = extension;
                ExpectedPath = expectedFile;
            }
        }
            
    }
}
