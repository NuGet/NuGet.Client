using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace NuGet.ProjectManagement
{
    internal class PackageItemComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            // BUG 636: We sort files so that they are added in the correct order
            // e.g aspx before aspx.cs

            if (x.Equals(y, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            // Add files that are prefixes of other files first
            if (x.StartsWith(y, StringComparison.OrdinalIgnoreCase))
            {
                return -1;
            }

            if (y.StartsWith(x, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return y.CompareTo(x);
        }
    }

    /// <summary>
    /// This class represents a NuGetProject based on a .NET project. This also contains an instance of a FolderNuGetProject
    /// </summary>
    public class MSBuildNuGetProject : NuGetProject
    {
        private IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; set; }
        public FolderNuGetProject FolderNuGetProject { get; private set; }
        public PackagesConfigNuGetProject PackagesConfigNuGetProject { get; private set; }

        private readonly IDictionary<FileTransformExtensions, IPackageFileTransformer> FileTransformers =
            new Dictionary<FileTransformExtensions, IPackageFileTransformer>() 
        {
            //{ new FileTransformExtensions(".transform", ".transform"), new XmlTransformer(GetConfigMappings()) },
            { new FileTransformExtensions(".pp", ".pp"), new Preprocessor() }
            //{ new FileTransformExtensions(".install.xdt", ".uninstall.xdt"), new XdtTransformer() }
        };

        public MSBuildNuGetProject(IMSBuildNuGetProjectSystem msbuildNuGetProjectSystem, string folderNuGetProjectPath, string packagesConfigFullPath)
        {
            if (msbuildNuGetProjectSystem == null)
            {
                throw new ArgumentNullException("nugetDotNetProjectSystem");
            }

            if (folderNuGetProjectPath == null)
            {
                throw new ArgumentNullException("folderNuGetProjectPath");
            }

            if (packagesConfigFullPath == null)
            {
                throw new ArgumentNullException("packagesConfigFullPath");
            }

            MSBuildNuGetProjectSystem = msbuildNuGetProjectSystem;
            FolderNuGetProject = new FolderNuGetProject(folderNuGetProjectPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.Name, MSBuildNuGetProjectSystem.ProjectName);
            InternalMetadata.Add(NuGetProjectMetadataKeys.TargetFramework, MSBuildNuGetProjectSystem.TargetFramework);            
            PackagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, InternalMetadata);
        }

        public override IEnumerable<PackageReference> GetInstalledPackages()
        {
            return PackagesConfigNuGetProject.GetInstalledPackages();
        }

        public override bool InstallPackage(PackageIdentity packageIdentity, Stream packageStream, INuGetProjectContext nuGetProjectContext)
        {
            if(packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if(packageStream == null)
            {
                throw new ArgumentNullException("packageStream");
            }

            if(nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            // Step-1: Check if the package already exists after setting the nuGetProjectContext
            MSBuildNuGetProjectSystem.SetNuGetProjectContext(nuGetProjectContext);

            var packageReference = GetInstalledPackages().Where(
                p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference != null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInProject,
                    packageIdentity, MSBuildNuGetProjectSystem.ProjectName);
                return false;
            }

            // Step-2: Create PackageReader using the PackageStream and obtain the various item groups
            var zipArchive = new ZipArchive(packageStream);
            PackageReader packageReader = new PackageReader(zipArchive);
            IEnumerable<FrameworkSpecificGroup> libItemGroups = packageReader.GetLibItems();
            IEnumerable<FrameworkSpecificGroup> frameworkReferenceGroups = packageReader.GetFrameworkItems();
            IEnumerable<FrameworkSpecificGroup> contentFileGroups = packageReader.GetContentItems();
            IEnumerable<FrameworkSpecificGroup> buildFileGroups = packageReader.GetBuildItems();

            // Step-3: Get the most compatible items groups for all items groups
            bool hasCompatibleItems = false;

            FrameworkSpecificGroup compatibleLibItemsGroup =
                GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, libItemGroups);
            FrameworkSpecificGroup compatibleFrameworkReferencesGroup =
                GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, frameworkReferenceGroups);
            FrameworkSpecificGroup compatibleContentFilesGroup =
                GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, contentFileGroups);
            FrameworkSpecificGroup compatibleBuildFilesGroup =
                GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, buildFileGroups);            

            hasCompatibleItems = IsValid(compatibleLibItemsGroup) || IsValid(compatibleFrameworkReferencesGroup) ||
                IsValid(compatibleContentFilesGroup) || IsValid(compatibleBuildFilesGroup);

            // Step-4: Check if there are any compatible items in the package. If not, throw
            if(!hasCompatibleItems)
            {
                throw new InvalidOperationException(
                           String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, MSBuildNuGetProjectSystem.TargetFramework));
            }

            // Step-5: Install package to FolderNuGetProject
            FolderNuGetProject.InstallPackage(packageIdentity, packageStream, nuGetProjectContext);

            // Step-6: MSBuildNuGetProjectSystem operations
            // Step-6.1: Add references to project
            if (IsValid(compatibleLibItemsGroup))
            {
                foreach (var libItem in compatibleLibItemsGroup.Items)
                {
                    if (IsAssemblyReference(libItem))
                    {
                        var libItemFullPath = Path.Combine(FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity), libItem);
                        MSBuildNuGetProjectSystem.AddReference(libItemFullPath);
                    }
                }
            }

            // Step-6.2: Add Frameworkreferences to project
            if (IsValid(compatibleFrameworkReferencesGroup))
            {
                foreach (var frameworkReference in compatibleFrameworkReferencesGroup.Items)
                {
                    MSBuildNuGetProjectSystem.AddFrameworkReference(Path.GetFileName(frameworkReference));
                }
            }

            // Step-6.3: Add Content Files
            if(IsValid(compatibleContentFilesGroup))
            {
                MSBuildNuGetProjectSystemUtility.AddFiles(MSBuildNuGetProjectSystem,
                    zipArchive, compatibleContentFilesGroup, FileTransformers);
            }

            // Step-6.4: Add Build imports
            if(IsValid(compatibleBuildFilesGroup))
            {
                throw new NotImplementedException("Build files are not implemented");
            }
            
            // Step-6.5: Execute powershell script

            // Step-7: Install package to PackagesConfigNuGetProject
            PackagesConfigNuGetProject.InstallPackage(packageIdentity, packageStream, nuGetProjectContext);

            // Step-8: Add packages.config to MSBuildNuGetProject

            return true;
        }

        public override bool UninstallPackage(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            // Step-0: Check if the package already exists after setting the nuGetProjectContext
            MSBuildNuGetProjectSystem.SetNuGetProjectContext(nuGetProjectContext);

            var packageReference = GetInstalledPackages().Where(
                p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference == null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageDoesNotExistInProject,
                    packageIdentity, MSBuildNuGetProjectSystem.ProjectName);
                return false;
            }

            throw new NotImplementedException();
        }

        protected static FrameworkSpecificGroup GetMostCompatibleGroup(NuGetFramework projectTargetFramework, IEnumerable<FrameworkSpecificGroup> itemGroups)
        {
            FrameworkReducer reducer = new FrameworkReducer();
            NuGetFramework mostCompatibleFramework = reducer.GetNearest(projectTargetFramework, itemGroups.Select(i => NuGetFramework.Parse(i.TargetFramework)));
            if(mostCompatibleFramework != null)
            {
                IEnumerable<FrameworkSpecificGroup> mostCompatibleGroups = itemGroups.Where(i => NuGetFramework.Parse(i.TargetFramework).Equals(mostCompatibleFramework));
                var mostCompatibleGroup = mostCompatibleGroups.FirstOrDefault();
                if(IsValid(mostCompatibleGroup))
                {
                    mostCompatibleGroup = new FrameworkSpecificGroup(mostCompatibleGroup.TargetFramework,
                        mostCompatibleGroup.Items.Select(item => MSBuildNuGetProjectSystemUtility.ReplaceAltDirSeparatorWithDirSeparator(item)));
                }

                return mostCompatibleGroup;
            }
            return null;
        }

        private static bool IsValid(FrameworkSpecificGroup frameworkSpecificGroup)
        {
            // It is possible for a compatible frameworkSpecificGroup, there are no items
            return (frameworkSpecificGroup != null && frameworkSpecificGroup.Items != null);
        }

        private void LogTargetFrameworkInfo(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext,
            FrameworkSpecificGroup libItemsGroup, FrameworkSpecificGroup contentItemsGroup, FrameworkSpecificGroup buildFilesGroup)
        {
            if(libItemsGroup.Items.Any() || contentItemsGroup.Items.Any() || buildFilesGroup.Items.Any())
            {
                string shortFramework = MSBuildNuGetProjectSystem.TargetFramework.GetShortFolderName();
                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                    this.GetMetadata<string>(NuGetProjectMetadataKeys.Name), shortFramework);
            }
        }

        private static string GetTargetFrameworkLogString(NuGetFramework targetFramework)
        {
            return (targetFramework == null || targetFramework == NuGetFramework.AnyFramework) ? Strings.Debug_TargetFrameworkInfo_NotFrameworkSpecific : String.Empty;
        }

        private static bool IsAssemblyReference(string filePath)
        {
            // assembly reference must be under lib/
            if (!filePath.StartsWith(Constants.LibDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !filePath.StartsWith(Constants.LibDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = Path.GetFileName(filePath);

            // if it's an empty folder, yes
            if (fileName == Constants.PackageEmptyFileName)
            {
                return true;
            }

            // Assembly reference must have a .dll|.exe|.winmd extension and is not a resource assembly;
            return !filePath.EndsWith(Constants.ResourceAssemblyExtension, StringComparison.OrdinalIgnoreCase) &&
                Constants.AssemblyReferencesExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        private static IDictionary<XName, Action<XElement, XElement>> GetConfigMappings()
        {
            // REVIEW: This might be an edge case, but we're setting this rule for all xml files.
            // If someone happens to do a transform where the xml file has a configSections node
            // we will add it first. This is probably fine, but this is a config specific scenario
            return new Dictionary<XName, Action<XElement, XElement>>() {
                { "configSections" , (parent, element) => parent.AddFirst(element) }
            };
        }
    }

    public static class Constants
    {
        /// <summary>
        /// Represents the ".nupkg" extension.
        /// </summary>
        public static readonly string PackageExtension = ".nupkg";

        /// <summary>
        /// Represents the ".nuspec" extension.
        /// </summary>
        public static readonly string ManifestExtension = ".nuspec";

        /// <summary>
        /// Represents the content directory in the package.
        /// </summary>
        public static readonly string ContentDirectory = "content";

        /// <summary>
        /// Represents the lib directory in the package.
        /// </summary>
        public static readonly string LibDirectory = "lib";

        /// <summary>
        /// Represents the tools directory in the package.
        /// </summary>
        public static readonly string ToolsDirectory = "tools";

        /// <summary>
        /// Represents the build directory in the package.
        /// </summary>
        public static readonly string BuildDirectory = "build";

        public static readonly string BinDirectory = "bin";
        public static readonly string SettingsFileName = "NuGet.Config";
        public static readonly string PackageReferenceFile = "packages.config";
        public static readonly string MirroringReferenceFile = "mirroring.config";

        public static readonly string BeginIgnoreMarker = "NUGET: BEGIN LICENSE TEXT";
        public static readonly string EndIgnoreMarker = "NUGET: END LICENSE TEXT";

        internal const string PackageRelationshipNamespace = "http://schemas.microsoft.com/packaging/2010/07/";

        // Starting from nuget 2.0, we use a file with the special name '_._' to represent an empty folder.
        internal const string PackageEmptyFileName = "_._";

        // This is temporary until we fix the gallery to have proper first class support for this.
        // The magic unpublished date is 1900-01-01T00:00:00
        public static readonly DateTimeOffset Unpublished = new DateTimeOffset(1900, 1, 1, 0, 0, 0, TimeSpan.FromHours(-8));

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Security",
            "CA2104:DoNotDeclareReadOnlyMutableReferenceTypes",
            Justification = "The type is immutable.")]
        public static readonly ICollection<string> AssemblyReferencesExtensions
            = new ReadOnlyCollection<string>(new string[] { ".dll", ".exe", ".winmd" });

        public const string ResourceAssemblyExtension = ".resources.dll";
    }
}
