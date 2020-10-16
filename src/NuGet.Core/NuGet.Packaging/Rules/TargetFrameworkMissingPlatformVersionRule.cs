// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace NuGet.Packaging.Rules
{
    internal class TargetFrameworkMissingPlatformVersionRule : IPackageRule
    {
        // NOTE: We never actually generate messages, only error on failure.
        public string MessageFormat => "";

        public TargetFrameworkMissingPlatformVersionRule()
        {
        }

        public IEnumerable<PackagingLogMessage> Validate(PackageArchiveReader builder)
        {
            return Validate(new NuspecReader(builder.GetNuspec()), builder.GetFiles());
        }

        internal static IEnumerable<PackagingLogMessage> Validate(NuspecReader reader, IEnumerable<string> files)
        {
            var bads = (new string[]
            {
                ValidateDependencyGroups(reader),
                ValidateReferenceGroups(reader),
                ValidateFrameworkAssemblies(reader),
                ValidateFiles(files)
            }).Where(err => err != null);

            if (bads.Any())
            {
                string items = string.Empty;
                foreach (string badMessage in bads)
                {
                    items += "\n- " + badMessage;
                }
                // PackagingLogMessage technically has an Error level, but
                // that doesn't look like it fails pack, which is what we need
                // to do in this case.
                throw new PackagingException(NuGetLogCode.NU1012, string.Format(CultureInfo.CurrentCulture, Strings.MissingTargetPlatformVersions, items));
            }
            else
            {
                return new List<PackagingLogMessage>();
            }
        }

        internal static string ValidateDependencyGroups(NuspecReader reader)
        {
            var bads = new HashSet<string>(reader.GetDependencyGroups()
                .Select(group => group.TargetFramework)
                .Where(groupFramework => groupFramework.HasPlatform && groupFramework.PlatformVersion == FrameworkConstants.EmptyVersion)
                .Select(framework => framework.GetShortFolderName()));
            if (bads.Any())
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MissingTargetPlatformVersionsFromDependencyGroups, string.Join(", ", bads));
            }
            else
            {
                return null;
            }
        }

        internal static string ValidateReferenceGroups(NuspecReader reader)
        {
            var bads = new HashSet<string>(reader.GetReferenceGroups()
                .Select(group => group.TargetFramework)
                .Where(groupFramework => groupFramework.HasPlatform && groupFramework.PlatformVersion == FrameworkConstants.EmptyVersion)
                .Select(framework => framework.GetShortFolderName()));
            if (bads.Any())
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MissingTargetPlatformVersionsFromReferenceGroups, string.Join(", ", bads));
            }
            else
            {
                return null;
            }
        }

        internal static string ValidateFrameworkAssemblies(NuspecReader reader)
        {
            var bads = new HashSet<string>(reader.GetFrameworkAssemblyGroups()
                .Select(group => group.TargetFramework)
                .Where(groupFramework => groupFramework.HasPlatform && groupFramework.PlatformVersion == FrameworkConstants.EmptyVersion)
                .Select(framework => framework.GetShortFolderName()));
            if (bads.Any())
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MissingTargetPlatformVersionsFromFrameworkAssemblyGroups, string.Join(", ", bads));
            }
            else
            {
                return null;
            }
        }

        internal static string ValidateFiles(IEnumerable<string> files)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files.Select(t => PathUtility.GetPathWithDirectorySeparator(t)))
            {
                set.Add(file);
            }

            var managedCodeConventions = new ManagedCodeConventions(new RuntimeGraph());
            var collection = new ContentItemCollection();
            collection.Load(set.Select(path => path.Replace('\\', '/')).ToArray());

            var patterns = managedCodeConventions.Patterns;

            var frameworkPatterns = new List<PatternSet>()
            {
                patterns.RuntimeAssemblies,
                patterns.CompileRefAssemblies,
                patterns.CompileLibAssemblies,
                patterns.NativeLibraries,
                patterns.ResourceAssemblies,
                patterns.MSBuildFiles,
                patterns.ContentFiles,
                patterns.ToolsAssemblies,
                patterns.EmbedAssemblies,
                patterns.MSBuildTransitiveFiles
            };
            var warnPaths = new HashSet<string>();

            var bads = new HashSet<string>();
            foreach (var pattern in frameworkPatterns)
            {
                IEnumerable<ContentItemGroup> targetedItemGroups = ContentExtractor.GetContentForPattern(collection, pattern);
                foreach (ContentItemGroup group in targetedItemGroups)
                {
                    foreach (ContentItem item in group.Items)
                    {
                        var framework = (NuGetFramework)item.Properties["tfm"];
                        if (framework == null)
                        {
                            continue;
                        }

                        if (framework.HasPlatform && framework.PlatformVersion == FrameworkConstants.EmptyVersion)
                        {
                            bads.Add(framework.GetShortFolderName());
                        }
                    }
                }
            }

            if (bads.Any())
            {
                return string.Format(CultureInfo.CurrentCulture, Strings.MissingTargetPlatformVersionsFromIncludedFiles, string.Join(", ", bads));
            }
            else
            {
                return null;
            }
        }
    }
}

