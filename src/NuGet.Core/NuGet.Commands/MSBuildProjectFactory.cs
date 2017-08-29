// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class MSBuildProjectFactory : IProjectFactory
    {
        private ILogger _logger;
        
        // Packaging folders
        private static readonly string ReferenceFolder = PackagingConstants.Folders.Lib;
        private static readonly string ToolsFolder = PackagingConstants.Folders.Tools;
        private static readonly string SourcesFolder = PackagingConstants.Folders.Source;

        private MSBuildPackTargetArgs PackTargetArgs { get; set; }
        private PackArgs PackArgs { get; set; }

        public void SetIncludeSymbols(bool includeSymbols)
        {
            IncludeSymbols = includeSymbols;
        }
        public bool IncludeSymbols { get; set; }

        public bool Build { get; set; }

        public Dictionary<string, string> GetProjectProperties()
        {
            return ProjectProperties;
        }
        public Dictionary<string, string> ProjectProperties { get; private set; }

        public bool IsTool { get; set; }
        public ICollection<ManifestFile> Files { get; set; } 
        
        public ILogger Logger
        {
            get
            {
                return _logger ?? NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }

        public IMachineWideSettings MachineWideSettings { get; set; }

        public static IProjectFactory ProjectCreator(PackArgs packArgs, string path)
        {
            return new MSBuildProjectFactory()
            {
                PackArgs = packArgs,
                IsTool = packArgs.Tool,
                Logger = packArgs.Logger,
                MachineWideSettings = packArgs.MachineWideSettings,
                Build = false,
                PackTargetArgs = packArgs.PackTargetArgs,
                Files = new HashSet<ManifestFile>()
            };
        }

        public PackageBuilder CreateBuilder(string basePath, NuGetVersion version, string suffix, bool buildIfNeeded, PackageBuilder builder)
        {
            // Add output files
            Files.Clear();

            AddOutputFiles(builder);

            // Add content files if there are any. They could come from a project or nuspec file
            AddContentFiles(builder);
            
            // Add sources if this is a symbol package
            if (IncludeSymbols)
            {
                AddSourceFiles();
            }

            Manifest manifest = new Manifest(new ManifestMetadata(builder), Files);
            var manifestPath = PackCommandRunner.GetOutputPath(
                builder,
                PackArgs,
                IncludeSymbols,
                builder.Version,
                PackTargetArgs.NuspecOutputPath,
                isNupkg: false);

            var manifestDirectory = Path.GetDirectoryName(manifestPath);
            if (!Directory.Exists(manifestDirectory))
            {
                Directory.CreateDirectory(manifestDirectory);
            }

            using (Stream stream = new FileStream(manifestPath, FileMode.Create))
            {
                manifest.Save(stream);
            }
            
            builder.PopulateFiles(string.Empty, Files);
            
            return builder;
        }

        private void AddOutputFiles(PackageBuilder builder)
        {
            if (PackTargetArgs.IncludeBuildOutput)
            {
                AddOutputLibFiles(PackTargetArgs.TargetPathsToSymbols, IncludeSymbols ? PackTargetArgs.AllowedOutputExtensionsInSymbolsPackageBuildOutputFolder : PackTargetArgs.AllowedOutputExtensionsInPackageBuildOutputFolder);

                AddOutputLibFiles(PackTargetArgs.TargetPathsToAssemblies, PackTargetArgs.AllowedOutputExtensionsInPackageBuildOutputFolder);
            }
        }

        private void AddOutputLibFiles(IEnumerable<OutputLibFile> libFiles, HashSet<string> allowedExtensions)
        {
            var targetFolder = PackTargetArgs.BuildOutputFolder;
            foreach (var file in libFiles)
            {
                var extension = Path.GetExtension(file.FinalOutputPath);

                // Only look at files we care about
                if (!allowedExtensions.Contains(extension))
                {
                    continue;
                }
                var tfm = NuGetFramework.Parse(file.TargetFramework).GetShortFolderName();
                var targetPath = file.TargetPath;
                var packageFile = new ManifestFile()
                {
                    Source = file.FinalOutputPath,
                    Target = IsTool ? Path.Combine(targetFolder, targetPath) : Path.Combine(targetFolder, tfm, targetPath)
                };

                AddFileToBuilder(packageFile);
            }
        }

        private void AddFileToBuilder(ManifestFile packageFile)
        {
            if (!Files.Any(p => packageFile.Target.Equals(p.Target, StringComparison.CurrentCultureIgnoreCase)))
            {
                Files.Add(packageFile);
            }
            else
            {
                _logger.LogWarning(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.FileNotAddedToPackage,
                        packageFile.Source,
                        packageFile.Target));
            }
        }

        private void AddContentFiles(PackageBuilder builder)
        {
            foreach (var sourcePath in PackTargetArgs.ContentFiles.Keys)
            {
                var listOfContentMetadata = PackTargetArgs.ContentFiles[sourcePath];
                foreach (var contentMetadata in listOfContentMetadata)
                {
                    string target = contentMetadata.Target;
                    var packageFile = new ManifestFile()
                    {
                        Source = sourcePath,
                        Target = target.EndsWith(Path.DirectorySeparatorChar.ToString()) || string.IsNullOrEmpty(target)
                        ? Path.Combine(target, Path.GetFileName(sourcePath))
                        : target
                    };
                    AddFileToBuilder(packageFile);

                    // Add contentFiles entry to the nuspec if applicable
                    if (IsContentFile(contentMetadata.Target))
                    {
                        var includePath = PathUtility.GetRelativePath("contentFiles" + Path.DirectorySeparatorChar, packageFile.Target, '/');
                        // This is just a check to see if the filename has already been appended to the target path. 
                        // We do this by comparing extensions of the file
                        if (!Path.GetExtension(includePath)
                                .Equals(Path.GetExtension(sourcePath), StringComparison.OrdinalIgnoreCase))
                        {
                            includePath = Path.Combine(includePath, Path.GetFileName(sourcePath));
                        }

                        var manifestContentFile = new ManifestContentFiles()
                        {
                            BuildAction = contentMetadata.BuildAction,
                            Include = includePath,
                            CopyToOutput = contentMetadata.CopyToOutput,
                            Flatten = contentMetadata.Flatten
                        };
                        
                        builder.ContentFiles.Add(manifestContentFile);
                    }
                }
            }
        }

        private void AddSourceFiles()
        {
            foreach (var sourcePath in PackTargetArgs.SourceFiles.Keys)
            {
                var projectDirectory = PackTargetArgs.SourceFiles[sourcePath];
                var finalTargetPath = GetTargetPathForSourceFile(sourcePath, projectDirectory);

                var packageFile = new ManifestFile()
                {
                    Source = sourcePath,
                    Target = finalTargetPath
                };
                AddFileToBuilder(packageFile);
            }
        }

        public static string GetTargetPathForSourceFile(string sourcePath, string projectDirectory)
        {
            if(string.IsNullOrEmpty(sourcePath))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_EmptySourceFilePath));
            }

            if (string.IsNullOrEmpty(projectDirectory))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_EmptySourceFileProjectDirectory, sourcePath));
            }

            if (PathUtility.HasTrailingDirectorySeparator(projectDirectory))
            {
                projectDirectory = projectDirectory.Substring(0, projectDirectory.Length - 1);
            }
            var projectName = Path.GetFileName(projectDirectory);
            var targetPath = Path.Combine(SourcesFolder, projectName);
            if (sourcePath.Contains(projectDirectory))
            {
                // This is needed because Path.GetDirectoryName returns a path with Path.DirectorySepartorChar
                var projectDirectoryWithSeparatorChar = PathUtility.GetPathWithDirectorySeparator(projectDirectory);

                var relativePath = Path.GetDirectoryName(sourcePath).Replace(projectDirectoryWithSeparatorChar, string.Empty);
                if (!string.IsNullOrEmpty(relativePath) && PathUtility.IsDirectorySeparatorChar(relativePath[0]))
                {
                    relativePath = relativePath.Substring(1, relativePath.Length - 1);
                }
                if (PathUtility.HasTrailingDirectorySeparator(relativePath))
                {
                    relativePath = relativePath.Substring(0, relativePath.Length - 1);
                }
                targetPath = Path.Combine(targetPath, relativePath);
            }

            var finalTargetPath = Path.Combine(targetPath, Path.GetFileName(sourcePath));
            return finalTargetPath;
        }

        private static bool IsContentFile(string contentFileTargetPath)
        {
            return contentFileTargetPath != null && contentFileTargetPath.StartsWith("contentFiles", StringComparison.Ordinal);
        }
    }
}
