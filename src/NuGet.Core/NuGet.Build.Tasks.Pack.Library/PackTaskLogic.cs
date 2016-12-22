﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Versioning;

namespace NuGet.Build.Tasks.Pack
{
    public class PackTaskLogic : IPackTaskLogic
    {
        private const string IdentityProperty = "Identity";
        public PackArgs GetPackArgs(IPackTaskRequest<IMSBuildItem> request)
        {
            var packArgs = new PackArgs
            {
                Logger = request.Logger,
                OutputDirectory = request.PackageOutputPath,
                Serviceable = request.Serviceable,
                Suffix = request.VersionSuffix,
                Tool = request.IsTool,
                Symbols = request.IncludeSymbols,
                NoPackageAnalysis = request.NoPackageAnalysis,
                PackTargetArgs = new MSBuildPackTargetArgs
                {
                    TargetPathsToAssemblies = GetTargetPathsToAssemblies(request),
                    TargetPathsToSymbols = request.TargetPathsToSymbols,
                    AssemblyName = request.AssemblyName,
                    IncludeBuildOutput = request.IncludeBuildOutput,
                    BuildOutputFolder = request.BuildOutputFolder,
                    TargetFrameworks = ParseFrameworks(request)
                }
            };

            if (request.MinClientVersion != null)
            {
                Version version;
                if (!Version.TryParse(request.MinClientVersion, out version))
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidMinClientVersion,
                        request.MinClientVersion));
                }

                packArgs.MinClientVersion = version;
            }

            InitCurrentDirectoryAndFileName(request, packArgs);
            InitNuspecOutputPath(request, packArgs);

            if (request.IncludeSource)
            {
                packArgs.PackTargetArgs.SourceFiles = GetSourceFiles(request, packArgs.CurrentDirectory);
                packArgs.Symbols = request.IncludeSource;
            }

            PackCommandRunner.SetupCurrentDirectory(packArgs);

            var contentFiles = ProcessContentToIncludeInPackage(request, packArgs);
            packArgs.PackTargetArgs.ContentFiles = contentFiles;

            return packArgs;
        }

        public PackageBuilder GetPackageBuilder(IPackTaskRequest<IMSBuildItem> request)
        {
            var builder = new PackageBuilder
            {
                Id = request.PackageId,
                Description = request.Description,
                Copyright = request.Copyright,
                ReleaseNotes = request.ReleaseNotes,
                RequireLicenseAcceptance = request.RequireLicenseAcceptance,
                PackageTypes = ParsePackageTypes(request)
            };
            
            if (request.PackageVersion != null)
            {
                NuGetVersion version;
                if (!NuGetVersion.TryParse(request.PackageVersion, out version))
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.InvalidPackageVersion,
                        request.PackageVersion));
                }
                builder.Version = version;
            }
            else
            {
                builder.Version = new NuGetVersion("1.0.0");
            }

            if (request.Authors != null)
            {
                builder.Authors.AddRange(request.Authors);
            }

            if (request.Tags != null)
            {
                builder.Tags.AddRange(request.Tags);
            }

            Uri tempUri;
            if (Uri.TryCreate(request.LicenseUrl, UriKind.Absolute, out tempUri))
            {
                builder.LicenseUrl = tempUri;
            }
            if (Uri.TryCreate(request.ProjectUrl, UriKind.Absolute, out tempUri))
            {
                builder.ProjectUrl = tempUri;
            }
            if (Uri.TryCreate(request.IconUrl, UriKind.Absolute, out tempUri))
            {
                builder.IconUrl = tempUri;
            }
            if (!string.IsNullOrEmpty(request.RepositoryUrl) || !string.IsNullOrEmpty(request.RepositoryType))
            {
                builder.Repository = new RepositoryMetadata(request.RepositoryType, request.RepositoryUrl);
            }
            ParseProjectToProjectReferences(request, builder);
            GetPackageReferences(request, builder);
            return builder;
        }

        public PackCommandRunner GetPackCommandRunner(
            IPackTaskRequest<IMSBuildItem> request,
            PackArgs packArgs,
            PackageBuilder packageBuilder)
        {
            var runner = new PackCommandRunner(
                packArgs,
                MSBuildProjectFactory.ProjectCreator,
                packageBuilder);

            runner.GenerateNugetPackage = request.ContinuePackingAfterGeneratingNuspec;

            return runner;
        }

        public void BuildPackage(PackCommandRunner runner)
        {
            runner.BuildPackage();
        }

        private string[] GetTargetPathsToAssemblies(IPackTaskRequest<IMSBuildItem> request)
        {
            if (request.TargetPathsToAssemblies == null)
            {
                return new string[0];
            }

            return request.TargetPathsToAssemblies
                .Where(path => path != null)
                .Select(path => path.Trim())
                .Where(path => path != string.Empty)
                .Distinct()
                .ToArray();
        }

        private ISet<NuGetFramework> ParseFrameworks(IPackTaskRequest<IMSBuildItem> request)
        {
            var nugetFrameworks = new HashSet<NuGetFramework>();
            if (request.TargetFrameworks != null)
            {
                nugetFrameworks = new HashSet<NuGetFramework>(request.TargetFrameworks.Select(t => NuGetFramework.Parse(t)));
            }

            return nugetFrameworks;
        }

        private ICollection<PackageType> ParsePackageTypes(IPackTaskRequest<IMSBuildItem> request)
        {
            var listOfPackageTypes = new List<PackageType>();
            if (request.PackageTypes != null)
            {
                foreach (var packageType in request.PackageTypes)
                {
                    string[] packageTypeSplitInPart = packageType.Split(new char[] { ',' });
                    string packageTypeName = packageTypeSplitInPart[0].Trim();
                    var version = PackageType.EmptyVersion;
                    if (packageTypeSplitInPart.Length > 1)
                    {
                        string versionString = packageTypeSplitInPart[1];
                        Version.TryParse(versionString, out version);
                    }
                    listOfPackageTypes.Add(new PackageType(packageTypeName, version));
                }
            }
            return listOfPackageTypes;
        }

        private LibraryDependency GetLibraryDependency(IMSBuildItem p2pReference, string packageId, string version)
        {
            LibraryIncludeFlags includeFlags, privateAssetsFlag;
            GetAssetMetadata(p2pReference, out includeFlags, out privateAssetsFlag);

            LibraryRange libraryRange = new LibraryRange(packageId, VersionRange.Parse(version, true), LibraryDependencyTarget.All);
            var libDependency = new LibraryDependency()
            {
                LibraryRange = libraryRange,
                IncludeType = includeFlags,
                SuppressParent = privateAssetsFlag
            };
            return libDependency;
        }

        private void InitCurrentDirectoryAndFileName(IPackTaskRequest<IMSBuildItem> request, PackArgs packArgs)
        {
            if (request.PackItem == null)
            {
                throw new InvalidOperationException(Strings.NoPackItemProvided);
            }

            packArgs.CurrentDirectory = Path.Combine(
                request.PackItem.GetProperty("RootDir"),
                request.PackItem.GetProperty("Directory")).TrimEnd(Path.DirectorySeparatorChar);

            packArgs.Arguments = new string[]
            {
                string.Concat(request.PackItem.GetProperty("FileName"), request.PackItem.GetProperty("Extension"))
            };

            packArgs.Path = request.PackItem.GetProperty("FullPath");
            packArgs.Exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private void InitNuspecOutputPath(IPackTaskRequest<IMSBuildItem> request, PackArgs packArgs)
        {
            if (Path.IsPathRooted(request.NuspecOutputPath))
            {
                packArgs.PackTargetArgs.NuspecOutputPath = request.NuspecOutputPath;
            }
            else
            {
                packArgs.PackTargetArgs.NuspecOutputPath = Path.Combine(
                    packArgs.CurrentDirectory,
                    request.NuspecOutputPath);
            }
        }

        private Dictionary<string, HashSet<ContentMetadata>> ProcessContentToIncludeInPackage(
            IPackTaskRequest<IMSBuildItem> request,
            PackArgs packArgs)
        {
            // This maps from source path on disk to target path inside the nupkg.
            var fileModel = new Dictionary<string, HashSet<ContentMetadata>>();
            if (request.PackageFiles != null)
            {
                var excludeFiles = CalculateFilesToExcludeInPack(request);
                foreach (var packageFile in request.PackageFiles)
                {
                    var sourcePath = GetSourcePath(packageFile);
                    if (excludeFiles.Contains(sourcePath))
                    {
                        continue;
                    }

                    var totalContentMetadata = GetContentMetadata(packageFile, sourcePath, packArgs, request.ContentTargetFolders);

                    if (fileModel.ContainsKey(sourcePath))
                    {
                        var existingContentMetadata = fileModel[sourcePath];
                        existingContentMetadata.AddRange(totalContentMetadata);
                    }
                    else
                    {
                        var existingContentMetadata = new HashSet<ContentMetadata>();
                        existingContentMetadata.AddRange(totalContentMetadata);
                        fileModel.Add(sourcePath, existingContentMetadata);
                    }
                }
            }

            return fileModel;
        }

        // The targetpaths returned from this function contain the directory in the nuget package where the file would go to. The filename is added later on to the target path.
        // whether or not the filename is added later on is dependent upon the fact that does the targetpath resolved here ends with a directory separator char or not.
        private IEnumerable<ContentMetadata> GetContentMetadata(IMSBuildItem packageFile, string sourcePath,
            PackArgs packArgs, string[] contentTargetFolders)
        {
            var targetPaths = contentTargetFolders
                .Where(f => !f.EndsWith(Path.DirectorySeparatorChar.ToString()))
                .Select(f => string.Concat(f, Path.DirectorySeparatorChar))
                .ToList();
            
            targetPaths.AddRange(
                contentTargetFolders
                .Where(f => f.EndsWith(Path.DirectorySeparatorChar.ToString())));

            // if user specified a PackagePath, then use that. Look for any ** which are indicated by the RecrusiveDir metadata in msbuild.
            if (packageFile.Properties.Contains("PackagePath"))
            {
                // The rule here is that if the PackagePath is an empty string, then we add the file to the root of the package.
                // Instead if it is a ';' delimited string, then the user needs to specify a '\' to indicate that the file should go to the root of the package.

                var packagePathString = packageFile.GetProperty("PackagePath");
                targetPaths = packagePathString == null
                    ? new string[] { String.Empty }.ToList()
                    : MSBuildStringUtility.Split(packagePathString)
                    .Distinct()
                    .ToList();

                var recursiveDir = packageFile.GetProperty("RecursiveDir");
                if (!string.IsNullOrEmpty(recursiveDir))
                {
                    var newTargetPaths = new List<string>();
                    foreach (var targetPath in targetPaths)
                    {
                        if (targetPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        {
                            newTargetPaths.Add(Path.Combine(targetPath, recursiveDir));
                        }
                        else
                        {
                            newTargetPaths.Add(targetPath);
                        }
                    }
                    targetPaths = newTargetPaths;
                }

                // this else if condition means the file is within the project directory and the target path should preserve this relative directory structure.
                else if (sourcePath.StartsWith(packArgs.CurrentDirectory, StringComparison.CurrentCultureIgnoreCase) &&
                         !Path.GetFileName(sourcePath)
                             .Equals(packageFile.GetProperty(IdentityProperty), StringComparison.CurrentCultureIgnoreCase))
                {
                    var newTargetPaths = new List<string>();
                    var identity = packageFile.GetProperty(IdentityProperty);
                    if (identity.EndsWith(Path.GetFileName(sourcePath), StringComparison.CurrentCultureIgnoreCase))
                    {
                        identity = Path.GetDirectoryName(identity);
                    }
                    foreach (var targetPath in targetPaths)
                    {
                        newTargetPaths.Add(Path.Combine(targetPath, identity) + Path.DirectorySeparatorChar);
                    }
                    targetPaths = newTargetPaths;
                }
            }

            var buildAction = BuildAction.Parse(packageFile.GetProperty("BuildAction"));

            // TODO: Do the work to get the right language of the project, tracked via https://github.com/NuGet/Home/issues/4100
            var language = buildAction.Equals(BuildAction.Compile) ? "cs" : "any";

            var setOfTargetPaths = new HashSet<string>(targetPaths, StringComparer.Ordinal);
            if (setOfTargetPaths.Remove("contentFiles" + Path.DirectorySeparatorChar) 
                || setOfTargetPaths.Remove("contentFiles"))
            {
                foreach (var framework in packArgs.PackTargetArgs.TargetFrameworks)
                {
                    setOfTargetPaths.Add(Path.Combine("contentFiles",
                        Path.Combine(language, framework.GetShortFolderName())) + Path.DirectorySeparatorChar);
                }
            }

            return setOfTargetPaths.Select(target => new ContentMetadata()
            {
                BuildAction = buildAction.IsKnown ? buildAction.Value : null,
                Source = sourcePath,
                Target = target
            });
        }

        private string GetSourcePath(IMSBuildItem packageFile)
        {
            string sourcePath = packageFile.GetProperty("FullPath");
            if (packageFile.Properties.Contains("MSBuildSourceProjectFile"))
            {
                string sourceProjectFile = packageFile.GetProperty("MSBuildSourceProjectFile");
                string identity = packageFile.GetProperty(IdentityProperty);
                sourcePath = Path.Combine(sourceProjectFile.Replace(Path.GetFileName(sourceProjectFile), string.Empty), identity);
            }
            return Path.GetFullPath(sourcePath);
        }

        private ISet<string> CalculateFilesToExcludeInPack(IPackTaskRequest<IMSBuildItem> request)
        {
            var excludeFiles = new HashSet<string>();
            if (request.PackageFilesToExclude != null)
            {
                foreach (var file in request.PackageFilesToExclude)
                {
                    string sourcePath = GetSourcePath(file);
                    excludeFiles.Add(sourcePath);
                }
            }
            return excludeFiles;
        }

        private IDictionary<string, string> GetSourceFiles(IPackTaskRequest<IMSBuildItem> request, string currentProjectDirectory)
        {
            var sourceFiles = new Dictionary<string, string>();
            if (request.SourceFiles != null)
            {
                foreach (var src in request.SourceFiles)
                {
                    var sourcePath = GetSourcePath(src);
                    string sourceProjectFile = currentProjectDirectory;
                    if (src.Properties.Contains("MSBuildSourceProjectFile"))
                    {
                        sourceProjectFile = src.GetProperty("MSBuildSourceProjectFile");
                        sourceProjectFile = Path.GetDirectoryName(sourceProjectFile);
                    }

                    sourceFiles.Add(sourcePath, sourceProjectFile);
                }
            }
            return sourceFiles;
        }

        private void GetPackageReferences(IPackTaskRequest<IMSBuildItem> request, PackageBuilder packageBuilder)
        {
            var dependencyByFramework = new Dictionary<NuGetFramework, HashSet<LibraryDependency>>();
            if (request.PackageReferences != null)
            {
                foreach (var packageRef in request.PackageReferences)
                {
                    var dependencies = new HashSet<LibraryDependency>();
                    string packageId, version;
                    string targetFramework = packageRef.GetProperty("TargetFramework");
                    NuGetFramework framework = NuGetFramework.Parse(targetFramework);
                    if (dependencyByFramework.ContainsKey(framework))
                    {
                        dependencies = dependencyByFramework[framework];
                    }
                    else
                    {
                        dependencyByFramework.Add(framework, dependencies);
                    }

                    ParsePackageReference(packageRef, out packageId, out version);
                    var libDependency = GetLibraryDependency(packageRef, packageId, version);

                    PackCommandRunner.AddLibraryDependency(libDependency, dependencies);
                }

                foreach (var framework in dependencyByFramework.Keys)
                {
                    PackCommandRunner.AddDependencyGroups(dependencyByFramework[framework], framework, packageBuilder);
                }
            }
        }

        private void ParseProjectToProjectReferences(IPackTaskRequest<IMSBuildItem> request, PackageBuilder packageBuilder)
        {
            var dependencyByFramework = new Dictionary<NuGetFramework, HashSet<LibraryDependency>>();
            if (request.ProjectReferences != null)
            {
                foreach (var p2pReference in request.ProjectReferences)
                {
                    var dependencies = new HashSet<LibraryDependency>();
                    var typeOfReference = p2pReference.GetProperty("Type");

                    // TODO: implement project reference type = "project". From the design spec, it seems that this
                    // should be indicated with:
                    // <TreatAsPackageReference>false</TreatAsPackageReference>
                    //
                    // Issue: https://github.com/NuGet/Home/issues/3891

                    if (string.Equals(typeOfReference, "package", StringComparison.OrdinalIgnoreCase))
                    {
                        // This is to be treated as a nupkg dependency, add as library dependency.
                        var packageId = p2pReference.GetProperty("PackageId");
                        var version = p2pReference.GetProperty("PackageVersion");
                        //TODO: Do the work to get the version from AssemblyInfo.cs
                        if (string.IsNullOrEmpty(version))
                        {
                            version = "1.0.0";
                        }
                        var libDependency = GetLibraryDependency(p2pReference, packageId, version);
                        var targetFramework = p2pReference.GetProperty("TargetFramework");
                        var nugetFramework = NuGetFramework.Parse(targetFramework);
                        if (dependencyByFramework.ContainsKey(nugetFramework))
                        {
                            dependencies = dependencyByFramework[nugetFramework];
                        }
                        else
                        {
                            dependencyByFramework.Add(nugetFramework, dependencies);
                        }
                        PackCommandRunner.AddLibraryDependency(libDependency, dependencies);
                    }
                }

                foreach (var nugetFramework in dependencyByFramework.Keys)
                {
                    PackCommandRunner.AddDependencyGroups(dependencyByFramework[nugetFramework], nugetFramework, packageBuilder);
                }
            }
        }

        private void ParsePackageReference(IMSBuildItem packageReference, out string packageId, out string version)
        {
            packageId = packageReference.Identity;
            version = packageReference.GetProperty("Version");
            if (string.IsNullOrEmpty(version))
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.InvalidPackageReferenceVersion,
                    packageId));
            }
        }

        private void GetAssetMetadata(
            IMSBuildItem packageRef,
            out LibraryIncludeFlags include,
            out LibraryIncludeFlags suppressParent)
        {
            var includeFlags = ParseLibraryIncludeFlags(
                packageRef.GetProperty("IncludeAssets"),
                LibraryIncludeFlags.All);

            var excludeFlags = ParseLibraryIncludeFlags(
                packageRef.GetProperty("ExcludeAssets"),
                LibraryIncludeFlags.None);

            include = includeFlags & ~excludeFlags;

            suppressParent = ParseLibraryIncludeFlags(
                packageRef.GetProperty("PrivateAssets"),
                LibraryIncludeFlagUtils.DefaultSuppressParent);
        }

        private LibraryIncludeFlags ParseLibraryIncludeFlags(string input, LibraryIncludeFlags defaultFlags)
        {
            if (input == null)
            {
                return defaultFlags;
            }

            var unparsedFlags = input
                .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim())
                .Where(f => f.Length != 0)
                .ToArray();

            return unparsedFlags.Any() ? LibraryIncludeFlagUtils.GetFlags(unparsedFlags) : defaultFlags;
        }
    }
}
