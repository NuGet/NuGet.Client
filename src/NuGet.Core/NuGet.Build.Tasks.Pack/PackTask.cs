using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.Versioning;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;

namespace NuGet.Build.Tasks.Pack
{
    public class PackTask : Microsoft.Build.Utilities.Task
    {
        [Required]
        public ITaskItem PackItem { get; set; }
        public ITaskItem[] PackageFiles { get; set; }
        public ITaskItem[] PackageFilesToExclude { get; set; }
        public string[] TargetFrameworks { get; set; }
        public string[] PackageTypes { get; set; }
        public string PackageId { get; set; }
        public string PackageVersion { get; set; }
        public string[] Authors { get; set; }
        public string Description { get; set; }
        public string Copyright { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public string LicenseUrl { get; set; }
        public string ProjectUrl { get; set; }
        public string IconUrl { get; set; }
        public string[] Tags { get; set; }
        public string ReleaseNotes { get; set; }
        public string Configuration { get; set; }
        public string[] TargetPaths { get; set; }
        public string AssemblyName { get; set; }
        public string PackageOutputPath { get; set; }
        public bool IsTool { get; set; }
        public bool IncludeSymbols { get; set; }
        public bool IncludeSource { get; set; }
        public string RepositoryUrl { get; set; }
        public string RepositoryType { get; set; }
        public ITaskItem[] SourceFiles { get; set; }
        public bool NoPackageAnalysis { get; set; }
        public string MinClientVersion { get; set; }
        public bool Serviceable { get; set; }
        public string VersionSuffix { get; set; }
        public ITaskItem[] AssemblyReferences { get; set; }
        public ITaskItem[] PackageReferences { get; set; }
        public ITaskItem[] ProjectReferences { get; set; }
        public bool ContinuePackingAfterGeneratingNuspec { get; set; }
        public string NuspecOutputPath { get; set; }

        public override bool Execute()
        {
            var packArgs = GetPackArgs();
            var packageBuilder = GetPackageBuilder(packArgs);
            var contentFiles = ProcessContentToIncludeInPackage(packArgs.CurrentDirectory);
            packArgs.PackTargetArgs.ContentFiles = contentFiles;
            PackCommandRunner packRunner = new PackCommandRunner(packArgs, MSBuildProjectFactory.ProjectCreator, packageBuilder);
            packRunner.GenerateNugetPackage = ContinuePackingAfterGeneratingNuspec;
            packRunner.BuildPackage();
            return true;
        }

        private PackArgs GetPackArgs()
        {
            var packArgs = new PackArgs();
            packArgs.Logger = new MSBuildLogger(Log);
            packArgs.OutputDirectory = PackageOutputPath;
            packArgs.Serviceable = Serviceable;
            packArgs.Suffix = VersionSuffix;
            packArgs.Tool = IsTool;
            packArgs.Symbols = IncludeSymbols;
            packArgs.NoPackageAnalysis = NoPackageAnalysis;
            packArgs.PackTargetArgs = new MSBuildPackTargetArgs()
            {
                TargetPaths = TargetPaths,
                Configuration = Configuration,
                AssemblyName = AssemblyName
            };
            packArgs.PackTargetArgs.TargetFrameworks = ParseFrameworks();
            if (MinClientVersion != null)
            {
                Version version;
                if (!Version.TryParse(MinClientVersion, out version))
                {
                    throw new ArgumentException("MinClientVersion is invalid");
                }
                packArgs.MinClientVersion = version;
            }

            InitCurrentDirectoryAndFileName(packArgs);
            InitNuspecOutputPath(packArgs);

            if (IncludeSource)
            {
                packArgs.PackTargetArgs.SourceFiles = GetSourceFiles(packArgs.CurrentDirectory);
                packArgs.Symbols = IncludeSource;
            }

            PackCommandRunner.SetupCurrentDirectory(packArgs);

            return packArgs;
        }

        private ISet<NuGetFramework> ParseFrameworks()
        {
            var nugetFrameworks = new HashSet<NuGetFramework>();
            if (TargetFrameworks != null)
            {
                nugetFrameworks = new HashSet<NuGetFramework>(TargetFrameworks.Select(t => NuGetFramework.Parse(t)));
            }
            return nugetFrameworks;
        } 

        private PackageBuilder GetPackageBuilder(PackArgs packArgs)
        {
            PackageBuilder builder = new PackageBuilder();
            builder.Id = PackageId;
            if (PackageVersion != null)
            {
                NuGetVersion version;
                if (!NuGetVersion.TryParse(PackageVersion, out version))
                {
                    throw new ArgumentException(String.Format(CultureInfo.CurrentCulture, "PackageVersion string specified '{0}' is invalid.", PackageVersion));
                }
                builder.Version = version;
            }
            else
            {
                builder.Version = new NuGetVersion("1.0.0");
            }

            if (Authors != null)
            {
                builder.Authors.AddRange(Authors);
            }

            if (Tags != null)
            {
                builder.Tags.AddRange(Tags);
            }

            builder.Description = Description;
            builder.Copyright = Copyright;
            builder.ReleaseNotes = ReleaseNotes;
            Uri tempUri;
            if (Uri.TryCreate(LicenseUrl, UriKind.Absolute, out tempUri))
            {
                builder.LicenseUrl = tempUri;
            }
            if (Uri.TryCreate(ProjectUrl, UriKind.Absolute, out tempUri))
            {
                builder.ProjectUrl = tempUri;
            }
            if (Uri.TryCreate(IconUrl, UriKind.Absolute, out tempUri))
            {
                builder.IconUrl = tempUri;
            }
            builder.RequireLicenseAcceptance = RequireLicenseAcceptance;
            if (!string.IsNullOrEmpty(RepositoryUrl) || !string.IsNullOrEmpty(RepositoryType))
            {
                builder.Repository = new RepositoryMetadata(RepositoryType, RepositoryUrl);
            }
            builder.PackageTypes = ParsePackageTypes();
            ParseProjectToProjectReferences(builder, packArgs);
            GetPackageReferences(builder);
            return builder;
        }

        private ICollection<PackageType> ParsePackageTypes()
        {
            var listOfPackageTypes = new List<PackageType>();
            if (PackageTypes != null)
            {
                foreach (var packageType in PackageTypes)
                {
                    string[] packageTypeSplitInPart = packageType.Split(new char[] {','});
                    string packageTypeName = packageTypeSplitInPart[0];
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

        private LibraryDependency GetLibraryDependency(ITaskItem p2pReference, string packageId, string version)
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

        private void InitCurrentDirectoryAndFileName(PackArgs packArgs)
        {
            if (PackItem == null)
            {
                throw new InvalidOperationException(nameof(PackItem));
            }
            else
            {
                packArgs.CurrentDirectory = Path.Combine(PackItem.GetMetadata("RootDir"), PackItem.GetMetadata("Directory"));
                packArgs.Arguments = new string[] {string.Concat(PackItem.GetMetadata("FileName"), PackItem.GetMetadata("Extension"))};
                packArgs.Path = PackItem.GetMetadata("FullPath");
                packArgs.Exclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private void InitNuspecOutputPath(PackArgs packArgs)
        {
            if (Path.IsPathRooted(NuspecOutputPath))
            {
                packArgs.PackTargetArgs.NuspecOutputPath = NuspecOutputPath;
            }
            else
            {
                NuspecOutputPath = Path.Combine(packArgs.CurrentDirectory, NuspecOutputPath);
                packArgs.PackTargetArgs.NuspecOutputPath = NuspecOutputPath;
            }
        }
        
        private Dictionary<string, HashSet<string>> ProcessContentToIncludeInPackage(string currentProjectDirectory)
        {
            // This maps from source path on disk to target path inside the nupkg.
            var fileModel = new Dictionary<string, HashSet<string>>();
            if (PackageFiles != null)
            {
                var excludeFiles = CalculateFilesToExcludeInPack();
                foreach (var packageFile in PackageFiles)
                {
                    string[] targetPaths;
                    var customMetadata = packageFile.CloneCustomMetadata();
                    var sourcePath = GetSourcePath(packageFile, customMetadata);
                    if (excludeFiles.Contains(sourcePath))
                    {
                        continue;
                    }

                    GetTargetPath(packageFile, customMetadata, sourcePath, currentProjectDirectory, out targetPaths);

                    if (fileModel.ContainsKey(sourcePath))
                    {
                        var setOfTargetPaths = fileModel[sourcePath];
                        setOfTargetPaths.AddRange(targetPaths);
                    }
                    else
                    {
                        var setOfTargetPaths = new HashSet<string>();
                        setOfTargetPaths.AddRange(targetPaths);
                        fileModel.Add(sourcePath, setOfTargetPaths);
                    }
                }
            }

            return fileModel;
        }


        // The targetpaths returned from this function contain the directory in the nuget package where the file would go to. The filename is added later on to the target path.
        private void GetTargetPath(ITaskItem packageFile, IDictionary customMetadata, string sourcePath, string currentProjectDirectory, out string[] targetPaths)
        {
            targetPaths = new string[] { PackagingConstants.Folders.Content, PackagingConstants.Folders.ContentFiles };
            // if user specified a PackagePath, then use that. Look for any ** which are indicated by the RecrusiveDir metadata in msbuild.
            if (customMetadata.Contains("PackagePath"))
            {
                targetPaths = packageFile.GetMetadata("PackagePath").Split(';');
                var recursiveDir = packageFile.GetMetadata("RecursiveDir");
                if (!string.IsNullOrEmpty(recursiveDir))
                {
                    var newTargetPaths = new string[targetPaths.Length];
                    var i = 0;
                    foreach (var targetPath in targetPaths)
                    {
                        newTargetPaths[i] = Path.Combine(targetPath, recursiveDir);
                        i++;
                    }
                    targetPaths = newTargetPaths;
                }
            }
            // this else if condition means the file is within the project directory and the target path should preserve this relative directory structure.
            else if (sourcePath.StartsWith(currentProjectDirectory, StringComparison.CurrentCultureIgnoreCase) && 
                !Path.GetFileName(sourcePath).Equals(packageFile.GetMetadata("Identity"), StringComparison.CurrentCultureIgnoreCase))
            {
                var newTargetPaths = new string[targetPaths.Length];
                var i = 0;
                var identity = packageFile.GetMetadata("Identity");
                if (identity.EndsWith(Path.GetFileName(sourcePath), StringComparison.CurrentCultureIgnoreCase))
                {
                    identity = Path.GetDirectoryName(identity);
                }
                foreach (var targetPath in targetPaths)
                {
                    newTargetPaths[i] = Path.Combine(targetPath, identity);
                    i++;
                }
                targetPaths = newTargetPaths;
            }
        }

        private string GetSourcePath(ITaskItem packageFile, IDictionary customMetadata)
        {
            string sourcePath = packageFile.GetMetadata("FullPath");
            if (customMetadata.Contains("MSBuildSourceProjectFile"))
            {
                string sourceProjectFile = packageFile.GetMetadata("MSBuildSourceProjectFile");
                string identity = packageFile.GetMetadata("Identity");
                sourcePath = Path.Combine(sourceProjectFile.Replace(Path.GetFileName(sourceProjectFile), string.Empty), identity);
            }
            return Path.GetFullPath(sourcePath);
        }

        private ISet<string> CalculateFilesToExcludeInPack()
        {
            var excludeFiles = new HashSet<string>();
            if (PackageFilesToExclude != null)
            {
                foreach (var file in PackageFilesToExclude)
                {
                    var customMetadata = file.CloneCustomMetadata();
                    string sourcePath = GetSourcePath(file, customMetadata);
                    excludeFiles.Add(sourcePath);
                }
            }
            return excludeFiles;
        }

        private IDictionary<string,string> GetSourceFiles(string currentProjectDirectory)
        {
            var sourceFiles = new Dictionary<string, string>();
            if (SourceFiles != null)
            {
                foreach (var src in SourceFiles)
                {
                    var customMetadata = src.CloneCustomMetadata();
                    var sourcePath = GetSourcePath(src, customMetadata);
                    string sourceProjectFile = currentProjectDirectory;
                    if (customMetadata.Contains("MSBuildSourceProjectFile"))
                    {
                        sourceProjectFile = src.GetMetadata("MSBuildSourceProjectFile");
                        sourceProjectFile = Path.GetDirectoryName(sourceProjectFile);
                    }

                    sourceFiles.Add(sourcePath, sourceProjectFile);
                }
            }
            return sourceFiles;
        }

        private void GetReferences(PackageBuilder builder)
        {
            /*References can be of three types:
             * a) Reference to an existing dll on disk - In this case there will be a hint path, and this should be packed with a project.
             * b) Reference to an assembly in GAC - No hint path, this should be added to frameworkAssemblyReference in nuspec.
             * c) Reference to a DLL that comes from a nupkg - This has custom metadata like NuGetSourceType set to package, the nupkg should be added as a dependency.
             */
            var dependencies = new HashSet<LibraryDependency>();

            if (AssemblyReferences != null)
            {
                foreach (var assemblyRef in AssemblyReferences)
                {
                    var customMetadata = assemblyRef.CloneCustomMetadata();
                    if (customMetadata.Contains("NuGetSourceType"))
                    {
                        //TODO: Make sure LibraryDependencyTarget.All is correct
                        var packageId = assemblyRef.GetMetadata("NuGetPackageId");
                        var packageVersion = assemblyRef.GetMetadata("NuGetPackageVersion");
                        LibraryRange library = new LibraryRange(packageId, VersionRange.Parse(packageVersion), LibraryDependencyTarget.All);
                        LibraryDependency dependency = new LibraryDependency()
                        {
                            LibraryRange = library
                        };
                        LibraryDependency.AddLibraryDependency(dependency, dependencies);
                    }
                    else if(customMetadata.Contains("HintPath"))
                    {
                        //TODO: Add implementation. need to figure out the target path for this dll file.
                    }
                    else
                    {
                        //TODO: Add supported framework implementation and logic to avoid adding duplicates in the FrameworkReferences list.
                        var frameworkReference = new FrameworkAssemblyReference(assemblyRef.ItemSpec,
                            new List<NuGetFramework>());
                        builder.FrameworkReferences.Add(frameworkReference);
                    }
                }

                PackCommandRunner.AddDependencyGroups(dependencies, NuGetFramework.AnyFramework, builder);
            }
        }

        private void GetPackageReferences(PackageBuilder packageBuilder)
        {
            var dependencyByFramework = new Dictionary<NuGetFramework, HashSet<LibraryDependency>>();
            if (PackageReferences != null)
            {
                foreach (var packageRef in PackageReferences)
                {
                    var dependencies = new HashSet<LibraryDependency>();
                    string packageId, version;
                    string targetFramework = packageRef.GetMetadata("TargetFramework");
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

                    LibraryDependency.AddLibraryDependency(libDependency, dependencies);
                }

                foreach (var framework in dependencyByFramework.Keys)
                {
                    PackCommandRunner.AddDependencyGroups(dependencyByFramework[framework], framework, packageBuilder);
                }
            }
        }

        private void ParseProjectToProjectReferences(PackageBuilder packageBuilder, PackArgs packArgs)
        {
            var dependencyByFramework = new Dictionary<NuGetFramework, HashSet<LibraryDependency>>();
            var projectReferences = new List<ProjectToProjectReference>();
            if (ProjectReferences != null)
            {
                foreach (var p2pReference in ProjectReferences)
                {
                    var dependencies = new HashSet<LibraryDependency>();
                    var typeOfReference = p2pReference.GetMetadata("Type");
                    if (string.Equals(typeOfReference, "project", StringComparison.OrdinalIgnoreCase))
                    {
                        // This is a project reference, and the DLL should be copied over to lib.
                        projectReferences.Add(new ProjectToProjectReference()
                        {
                            TargetPath = p2pReference.GetMetadata("TargetPath"),
                            AssemblyName = p2pReference.GetMetadata("AssemblyName")
                        });
                        string projectPath = p2pReference.GetMetadata("MSBuildProjectFullPath");
                    }
                    else if (string.Equals(typeOfReference, "package", StringComparison.OrdinalIgnoreCase))
                    {
                        // This is to be treated as a nupkg dependency, add as library dependency.
                        var packageId = p2pReference.GetMetadata("PackageId");
                        var version = p2pReference.GetMetadata("PackageVersion");
                        //TODO: Do the work to get the version from AssemblyInfo.cs
                        if (string.IsNullOrEmpty(version))
                        {
                            version = "1.0.0";
                        }
                        var libDependency = GetLibraryDependency(p2pReference, packageId, version);
                        var targetFramework = p2pReference.GetMetadata("TargetFramework");
                        var nugetFramework = NuGetFramework.Parse(targetFramework);
                        if (dependencyByFramework.ContainsKey(nugetFramework))
                        {
                            dependencies = dependencyByFramework[nugetFramework];
                        }
                        else
                        {
                            dependencyByFramework.Add(nugetFramework, dependencies);
                        }
                        LibraryDependency.AddLibraryDependency(libDependency, dependencies);
                    }
                }

                foreach (var nugetFramework in dependencyByFramework.Keys)
                {
                    PackCommandRunner.AddDependencyGroups(dependencyByFramework[nugetFramework], nugetFramework, packageBuilder);
                }
                packArgs.PackTargetArgs.ProjectReferences = projectReferences;
            }
        }

        private void ParsePackageReference(ITaskItem packageReference, out string packageId, out string version)
        {
            packageId = packageReference.ItemSpec;
            version = packageReference.GetMetadata("Version");
            if (string.IsNullOrEmpty(version))
            {
                throw new InvalidOperationException("Package Reference needs to have a valid version");
            }
        }

        private void GetAssetMetadata(ITaskItem packageRef, out LibraryIncludeFlags include,
            out LibraryIncludeFlags suppressParent)
        {
            var includeAssets = packageRef.GetMetadata("IncludeAssets")
                .Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);
            var excludeAssets = packageRef.GetMetadata("ExcludeAssets")
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var privateAssets = packageRef.GetMetadata("PrivateAssets")
                .Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var includeFlags = includeAssets.Any()
                ? LibraryIncludeFlagUtils.GetFlags(includeAssets)
                : LibraryIncludeFlags.All;
            var excludeFlags = excludeAssets.Any()
                ? LibraryIncludeFlagUtils.GetFlags(excludeAssets)
                : LibraryIncludeFlags.None;
            include = includeFlags & ~excludeFlags;
            suppressParent = privateAssets.Any()
                ? LibraryIncludeFlagUtils.GetFlags(privateAssets)
                : LibraryIncludeFlagUtils.DefaultSuppressParent;
        }
    }
}
