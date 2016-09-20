using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Versioning;

namespace NuGet.Commands
{
    public class MSBuildProjectFactory : IProjectFactory
    {
        private Common.ILogger _logger;
        
        // Packaging folders
        private static readonly string ReferenceFolder = PackagingConstants.Folders.Lib;
        private static readonly string ToolsFolder = PackagingConstants.Folders.Tools;
        private static readonly string SourcesFolder = PackagingConstants.Folders.Source;
        
        // List of extensions to allow in the output path
        private static readonly HashSet<string> _allowedOutputExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".dll",
                ".exe",
                ".xml",
                ".winmd"
            };

        // List of extensions to allow in the output path if IncludeSymbols is set
        private static readonly HashSet<string> _allowedOutputExtensionsForSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                ".dll",
                ".exe",
                ".xml",
                ".winmd",
                ".pdb"
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
        
        public Common.ILogger Logger
        {
            get
            {
                return _logger ?? Common.NullLogger.Instance;
            }
            set
            {
                _logger = value;
            }
        }

        public Configuration.IMachineWideSettings MachineWideSettings { get; set; }

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
            AddContentFiles();
            
            // Add sources if this is a symbol package
            if (IncludeSymbols)
            {
                AddSourceFiles();
            }

            Manifest manifest = new Manifest(new ManifestMetadata(builder), Files);
            using (Stream stream = new FileStream(
                        PackCommandRunner.GetOutputPath(builder, PackArgs, IncludeSymbols, builder.Version,
                            PackTargetArgs.NuspecOutputPath, false), FileMode.Create))
            {
                manifest.Save(stream);
            }
            
            builder.PopulateFiles(string.Empty, Files);
            
            return builder;
        }

        private void AddOutputFiles(PackageBuilder builder)
        {
            // Get the target file path
            foreach (string targetPath in PackTargetArgs.TargetPaths)
            {
                var allowedOutputExtensions = _allowedOutputExtensions;

                if (IncludeSymbols)
                {
                    // Include pdbs for symbol packages
                    allowedOutputExtensions = _allowedOutputExtensionsForSymbols;
                }

                string projectOutputDirectory = Path.GetDirectoryName(targetPath);

                string targetFileName = PackTargetArgs.AssemblyName;
                IList<string> filesToCopy = GetFiles(projectOutputDirectory, targetFileName, allowedOutputExtensions, SearchOption.AllDirectories);

                //AddReferencedProjectsToOutputFiles(projectOutputDirectory, allowedOutputExtensions, filesToCopy);

                // By default we add all files in the project's output directory
                foreach (var file in filesToCopy)
                {
                    string extension = Path.GetExtension(file);

                    // Only look at files we care about
                    if (!allowedOutputExtensions.Contains(extension))
                    {
                        continue;
                    }

                    string targetFolder = ReferenceFolder;

                    if (IsTool)
                    {
                        targetFolder = ToolsFolder;
                    }
                    else
                    {
                        if (Directory.Exists(targetPath))
                        {
                            // In the new MSBuild world, this should never happen, as TargetPath will always be a list of output DLLs.
                            targetFolder = Path.Combine(ReferenceFolder, Path.GetDirectoryName(file.Replace(targetPath, string.Empty)));
                        }
                        else if (PackTargetArgs.TargetFrameworks.Count > 0)
                        {
                            //This should always execute in the new MSBuild world. This is the case where project.json is not being read,
                            // therefore packagebuilder has no targetframeworks
                            string frameworkName = Path.GetFileName(projectOutputDirectory);
                            NuGetFramework folderNameAsNuGetFramework = NuGetFramework.Parse(frameworkName);
                            string shortFolderName = string.Empty;
                            if (PackTargetArgs.TargetFrameworks.Contains(folderNameAsNuGetFramework))
                            {
                                shortFolderName = folderNameAsNuGetFramework.GetShortFolderName();
                            }
                            targetFolder = Path.Combine(ReferenceFolder, shortFolderName);
                        }
                    }
                    var packageFile = new ManifestFile()
                    {
                        Source = file,
                        Target = Path.Combine(targetFolder, Path.GetFileName(file))
                    };
                    AddFileToBuilder(packageFile);
                }
            }
        }

        private static IList<string> GetFiles(string path, string fileNameWithoutExtension, HashSet<string> allowedExtensions, SearchOption searchOption)
        {
            return allowedExtensions.Select(extension => Directory.GetFiles(path, fileNameWithoutExtension + extension, searchOption)).SelectMany(a => a).ToList();
        }

        private void AddFileToBuilder(ManifestFile packageFile)
        {
            if (!Files.Any(p => packageFile.Target.Equals(p.Target, StringComparison.CurrentCultureIgnoreCase)))
            {
                Files.Add(packageFile);
            }
            else
            {
                //TODO:  log warning : File '{0}' is not added because the package already contains file '{1}'
            }
        }

        private void AddReferencedProjectsToOutputFiles(string ownerProjectOutputDirectory, HashSet<string> allowedExtensions, IList<string> outputFiles)
        {
            if (PackTargetArgs != null && PackTargetArgs.ProjectReferences.Any())
            {
                foreach (var p2pReference in PackTargetArgs.ProjectReferences)
                {
                    string targetFileName = p2pReference.AssemblyName;
                    IEnumerable<string> referencedFilesInOwnerOutputDirectory = GetFiles(ownerProjectOutputDirectory,
                        targetFileName, allowedExtensions, SearchOption.AllDirectories);
                    outputFiles.AddRange(referencedFilesInOwnerOutputDirectory);
                }
            }
        }

        private void AddContentFiles()
        {
            foreach (var sourcePath in PackTargetArgs.ContentFiles.Keys)
            {
                var listOfTargetPaths = PackTargetArgs.ContentFiles[sourcePath];
                foreach (var targetPath in listOfTargetPaths)
                {
                    string target = targetPath;
                    var packageFile = new ManifestFile()
                    {
                        Source = sourcePath,
                        Target = Path.Combine(target, Path.GetFileName(sourcePath))
                    };
                    AddFileToBuilder(packageFile);
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
    }
}
