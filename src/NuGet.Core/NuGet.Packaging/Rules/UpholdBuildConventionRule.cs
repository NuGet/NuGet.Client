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
using NuGet.Frameworks;
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
            var warningMessage = new StringBuilder();
            foreach (var props in conventionViolatorsProps)
            {
                string usedString = props.Key;
                if(usedString == "unsupported")
                {
                    usedString = "build or other";
                }
                warningMessage.AppendLine(string.Format(MessageFormat, _propsExtension, usedString, props.Value));
            }

            foreach (var targets in conventionViolatorsTargets)
            {
                string usedString = targets.Key;
                if (usedString == "unsupported")
                {
                    usedString = "build or other";
                }
                warningMessage.AppendLine(string.Format(MessageFormat, _targetsExtension, usedString, targets.Value));
            }
            if(warningMessage.ToString() != string.Empty)
            {
                issues.Add(PackagingLogMessage.CreateWarning(warningMessage.ToString(), NuGetLogCode.NU5129));
            }
            return issues;
        }

        internal (IDictionary<string, string>, IDictionary<string, string>) IdentifyViolators(IEnumerable<string> files, string packageId)
        {
            var groupedFiles = files.GroupBy(t => GetFolderName(t), m => GetFile(m));
            var conventionViolatorsProps = new Dictionary<string, string>();
            var conventionViolatorsTargets = new Dictionary<string, string>();

            var correctPropsPattern = packageId + _propsExtension;
            var correctTargetsPattern = packageId + _targetsExtension;

            if (files.Count() != 0)
            {
                foreach(var group in groupedFiles)
                {
                    var hasProps = group.Any(t => t.EndsWith(_propsExtension));
                    var hasTargets = group.Any(t => t.EndsWith(_targetsExtension));

                    var correctTargetsFiles = group.Where(t => t.Equals(correctTargetsPattern,StringComparison.OrdinalIgnoreCase));
                    var correctPropsFiles = group.Where(t => t.Equals(correctPropsPattern, StringComparison.OrdinalIgnoreCase));

                    if (correctPropsFiles.Count() == 0 && hasProps)
                    {
                        conventionViolatorsProps.Add(group.Key, correctPropsPattern);
                    }

                    if (correctTargetsFiles.Count() == 0 && hasTargets)
                    {
                        conventionViolatorsTargets.Add(group.Key, correctTargetsPattern);
                    }
                }
            }

            return (conventionViolatorsProps,conventionViolatorsTargets);
        }

        internal string GetFolderName(string filePath)
        {
            return NuGetFramework.ParseFolder(filePath.Split('/')[1]).GetShortFolderName();
        }

        private string GetFile(string filePath)
        {
            return filePath.Split('/')[filePath.Count(p => p == '/')];
        }
    }
}
