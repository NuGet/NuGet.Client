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
        
        // List of extensions to allow in the output path
        private static readonly HashSet<string> _allowedOutputExtensions
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll",
            ".exe",
            ".xml",
            ".json",
            ".winmd",
            ".pri"
        };

        // List of extensions to allow in the output path if IncludeSymbols is set
        private static readonly HashSet<string> _allowedOutputExtensionsForSymbols
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll",
            ".exe",
            ".xml",
            ".winmd",
            ".json",
            ".pri",
            ".pdb",
            ".mdb"
        };

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
                if (IncludeSymbols)
                {
                    // Include pdbs for symbol packages
                    AddOutputLibFiles(PackTargetArgs.TargetPathsToSymbols, _allowedOutputExtensionsForSymbols);
                }

                AddOutputLibFiles(PackTargetArgs.TargetPathsToAssemblies, _allowedOutputExtensions);
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
                if (projectDirectory.EndsWith("\\"))
                {
                    projectDirectory = projectDirectory.Substring(0, projectDirectory.LastIndexOf("\\"));
                }
                var projectName = Path.GetFileName(projectDirectory);
                string targetPath = Path.Combine(SourcesFolder, projectName);
                if (sourcePath.Contains(projectDirectory))
                {
                    var relativePath = Path.GetDirectoryName(sourcePath).Replace(projectDirectory, string.Empty);
                    if (relativePath.StartsWith("\\"))
                    {
                        relativePath = relativePath.Substring(1, relativePath.Length - 1);
                    }
                    if (relativePath.EndsWith("\\"))
                    {
                        relativePath = relativePath.Substring(0, relativePath.LastIndexOf("\\"));
                    }
                    targetPath = Path.Combine(targetPath, relativePath);
                }
                var packageFile = new ManifestFile()
                {
                    Source = sourcePath,
                    Target = Path.Combine(targetPath, Path.GetFileName(sourcePath))
                };
                AddFileToBuilder(packageFile);
            }
        }

        private static bool IsContentFile(string contentFileTargetPath)
        {
            return contentFileTargetPath != null && contentFileTargetPath.StartsWith("contentFiles", StringComparison.Ordinal);
        }
    }
}
