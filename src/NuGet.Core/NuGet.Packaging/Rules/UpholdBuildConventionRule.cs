// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly IReadOnlyList<string> BuildFolders = new string[]
        {
            PackagingConstants.Folders.Build + '/',
            PackagingConstants.Folders.BuildTransitive + '/',
            PackagingConstants.Folders.BuildCrossTargeting + '/',
            "buildMultiTargeting/"
        };

        public string MessageFormat => AnalysisResources.BuildConventionIsViolatedWarning;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            return Validate(new ValidationPackageAdapter(builder));
        }

        internal IEnumerable<PackagingLogMessage> Validate(IValidationPackage builder)
        {
            var packageId = builder.NuspecReader.GetId();
            var filesInPackage = builder.GetFiles();

            var violations = IdentifyViolators(filesInPackage, packageId);
            var warnings = GenerateWarnings(violations);
            return warnings;
        }

        private string GetTfm(string file)
        {
            var index1 = file.IndexOf('/');
            var index2 = file.IndexOf('/', index1 + 1);
            if (index2 == -1)
            {
                return string.Empty;
            }

            var folderName = file.Substring(index1 + 1, index2 - index1 - 1);
            var framework = NuGetFramework.Parse(folderName);

            return framework.IsUnsupported ? string.Empty : folderName;
        }

        private string GetBuildFolder(string file)
        {
            var index = file.IndexOf('/');
            return file.Substring(0, index + 1);
        }

        private bool EndsWithMsbuildFileExtension(string file)
        {
            foreach (var extension in ManagedCodeConventions.Properties["msbuild"].FileExtensions)
            {
                if (file.EndsWith(extension))
                {
                    return true;
                }
            }

            return false;
        }

        private bool InsideBuildFolder(string file)
        {
            foreach (var buildFolder in BuildFolders)
            {
                if (file.StartsWith(buildFolder))
                {
                    return true;
                }
            }

            return false;
        }

        internal IEnumerable<PackagingLogMessage> GenerateWarnings(IEnumerable<ConventionViolator> conventionViolators)
        {

            var issues = new List<PackagingLogMessage>();
            foreach (var folder in BuildFolders)
            {
                var warningMessage = new StringBuilder();
                var currentConventionViolators = conventionViolators.Where(t => t.ExpectedPath.StartsWith(folder));
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

        internal List<ConventionViolator> IdentifyViolators(IEnumerable<string> files, string packageId)
        {
            var violations = new List<ConventionViolator>();

            var msbuildFiles = files.Where(EndsWithMsbuildFileExtension);
            var msbuildFilesInBuildFolder = msbuildFiles.Where(InsideBuildFolder);
            var msbuildFilesGroupedByBuildFolder = msbuildFilesInBuildFolder.GroupBy(GetBuildFolder);

            foreach (var buildFolder in msbuildFilesGroupedByBuildFolder)
            {
                foreach (var tfm in buildFolder.GroupBy(GetTfm))
                {
                    foreach (var extension in tfm.GroupBy(Path.GetExtension))
                    {
                        string expectedFileName = tfm.Key == string.Empty
                            ? buildFolder.Key + packageId + extension.Key
                            : buildFolder.Key + tfm.Key + '/' + packageId + extension.Key;
                        if (!extension.Any(file => string.Equals(expectedFileName, file, StringComparison.OrdinalIgnoreCase)))
                        {
                            string packageFolder = tfm.Key == string.Empty ? buildFolder.Key : buildFolder.Key + tfm.Key + '/';
                            violations.Add(new ConventionViolator(packageFolder, extension.Key, expectedFileName));
                        }
                    }
                }
            }

            violations.Sort(ConventionViolatorComparer.Instance);

            return violations;
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
#if NETCOREAPP
                Path = filePath.Replace(filePath.Split('/')[filePath.Count(p => p == '/')], string.Empty, StringComparison.OrdinalIgnoreCase);
#else
                Path = filePath.Replace(filePath.Split('/')[filePath.Count(p => p == '/')], string.Empty);
#endif
                Extension = extension;
                ExpectedPath = expectedFile;
            }
        }

        internal class ConventionViolatorComparer : IComparer<ConventionViolator>
        {
            internal static ConventionViolatorComparer Instance { get; } = new ConventionViolatorComparer();

            public int Compare(ConventionViolator x, ConventionViolator y)
            {
                var result = x.Path.CompareTo(y.Path);
                if (result != 0)
                {
                    return result;
                }

                result = x.Extension.CompareTo(y.Extension);
                if (result != 0)
                {
                    return result;
                }

                return x.ExpectedPath.CompareTo(y.ExpectedPath);
            }
        }
    }
}
