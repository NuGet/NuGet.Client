// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    internal class UpholdBuildConventionRule : IPackageRule
    {
        private static ManagedCodeConventions ManagedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
        private string _propsExtension = ManagedCodeConventions.Properties["msbuild"].FileExtensions[1];
        private string _targetsExtension = ManagedCodeConventions.Properties["msbuild"].FileExtensions[0];

        public string MessageFormat => AnalysisResources.BuildConventionIsViolatedWarning;

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            var files = builder.GetFiles().Where(t => t.StartsWith(PackagingConstants.Folders.Build));
            var packageId = builder.NuspecReader.GetId();
            var (conventionViolatorsProps, conventionViolatorsTargets) = IdentifyViolators(files, packageId);
            return GenerateWarnings(conventionViolatorsProps, conventionViolatorsTargets);
        }

        internal IEnumerable<PackagingLogMessage> GenerateWarnings(IDictionary<string, string> conventionViolatorsProps, IDictionary<string, string> conventionViolatorsTargets)
        {
            var issues = new List<PackagingLogMessage>();
            foreach (var props in conventionViolatorsProps)
            {
                var warning = string.Format(MessageFormat, _propsExtension, props.Key, props.Value);
                issues.Add(PackagingLogMessage.CreateWarning(warning, NuGetLogCode.NU5129));
            }

            foreach (var targets in conventionViolatorsTargets)
            {
                var warning = string.Format(MessageFormat, _targetsExtension, targets.Key, targets.Value);
                issues.Add(PackagingLogMessage.CreateWarning(warning, NuGetLogCode.NU5129));
            }
            return issues;
        }

        internal (IDictionary<string, string>, IDictionary<string, string>) IdentifyViolators(IEnumerable<string> files, string packageId)
        {
            var collection = new ContentItemCollection();
            collection.Load(files.Where(t => PathUtility.GetPathWithDirectorySeparator(t).Count(m => m == Path.DirectorySeparatorChar) > 1));

            var buildItems = ContentExtractor.GetContentForPattern(collection, ManagedCodeConventions.Patterns.MSBuildFiles);
            var tfms = ContentExtractor.GetGroupFrameworks(buildItems).Select(m => m.GetShortFolderName());

            var conventionViolatorsProps = new Dictionary<string, string>();
            var conventionViolatorsTargets = new Dictionary<string, string>();

            var correctProps = packageId + _propsExtension;
            var correctTargets = packageId + _targetsExtension;

            var filesUnderTFM = files.Where(t => t.Count(m => m == '/') > 1);
            var filesUnderBuild = files.Where(t => t.Count(m => m == '/') == 1);

            if (files.Count() != 0)
            {
                if (filesUnderBuild.Count() != 0)
                {
                    var hasPropsUnderBuild = filesUnderBuild.Any(t => t.EndsWith(_propsExtension));
                    var hasTargetsUnderBuild = filesUnderBuild.Any(t => t.EndsWith(_targetsExtension));

                    var correctPropsFilesUnderBuild = filesUnderBuild.Where(t => t.EndsWith(_propsExtension) && t.EndsWith(correctProps));
                    var correctTargetsFilesUnderBuild = filesUnderBuild.Where(t => t.EndsWith(_targetsExtension) && t.EndsWith(correctTargets));

                    if (correctPropsFilesUnderBuild.Count() == 0 && hasPropsUnderBuild)
                    {
                        conventionViolatorsProps.Add(PackagingConstants.Folders.Build, correctProps);
                    }

                    if (correctTargetsFilesUnderBuild.Count() == 0 && hasTargetsUnderBuild)
                    {
                        conventionViolatorsTargets.Add(PackagingConstants.Folders.Build, correctTargets);
                    }
                }

                foreach (var tfm in tfms)
                {
                    var hasPropsUnderTFMs = filesUnderTFM.Any(t => t.StartsWith(PackagingConstants.Folders.Build + "/" + tfm) && t.EndsWith(_propsExtension));
                    var hasTargetsUnderTFMs = filesUnderTFM.Any(t => t.StartsWith(PackagingConstants.Folders.Build + "/" + tfm) && t.EndsWith(_targetsExtension));

                    var correctTargetsFiles = filesUnderTFM.Where(t => t.StartsWith(PackagingConstants.Folders.Build + "/" + tfm) && t.EndsWith(correctTargets));
                    var correctPropsFiles = filesUnderTFM.Where(t => t.StartsWith(PackagingConstants.Folders.Build + "/" + tfm) && t.EndsWith(correctProps));

                    if (correctPropsFiles.Count() == 0 && hasPropsUnderTFMs)
                    {
                        conventionViolatorsProps.Add(PackagingConstants.Folders.Build + "/" + tfm, correctProps);
                    }

                    if (correctTargetsFiles.Count() == 0 && hasTargetsUnderTFMs)
                    {
                        conventionViolatorsTargets.Add(PackagingConstants.Folders.Build + "/" + tfm, correctTargets);
                    }
                }
            }

            return (conventionViolatorsProps,conventionViolatorsTargets);
        }
    }
}
