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
using System.Threading;
using System.Threading.Tasks;
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
            { new FileTransformExtensions(".transform", ".transform"), new XmlTransformer(GetConfigMappings()) },
            { new FileTransformExtensions(".pp", ".pp"), new Preprocessor() },
            { new FileTransformExtensions(".install.xdt", ".uninstall.xdt"), new XdtTransformer() }
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
            InternalMetadata.Add(NuGetProjectMetadataKeys.FullPath, msbuildNuGetProjectSystem.ProjectFullPath);
            InternalMetadata.Add(NuGetProjectMetadataKeys.UniqueName, msbuildNuGetProjectSystem.ProjectUniqueName);
            PackagesConfigNuGetProject = new PackagesConfigNuGetProject(packagesConfigFullPath, InternalMetadata);
        }

        public override Task<IEnumerable<PackageReference>> GetInstalledPackagesAsync(CancellationToken token)
        {
            return PackagesConfigNuGetProject.GetInstalledPackagesAsync(token);
        }

        public void AddBindingRedirects()
        {
            MSBuildNuGetProjectSystem.AddBindingRedirects();
        }

        public async override Task<bool> InstallPackageAsync(PackageIdentity packageIdentity, Stream packageStream,
            INuGetProjectContext nuGetProjectContext, CancellationToken token)
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

            if(!packageStream.CanSeek)
            {
                throw new ArgumentException(Strings.PackageStreamShouldBeSeekable);
            }

            // Step-1: Check if the package already exists after setting the nuGetProjectContext
            MSBuildNuGetProjectSystem.SetNuGetProjectContext(nuGetProjectContext);

            var packageReference = (await GetInstalledPackagesAsync(token)).Where(
                p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference != null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageAlreadyExistsInProject,
                    packageIdentity, MSBuildNuGetProjectSystem.ProjectName);
                return false;
            }

            // Step-2: Create PackageReader using the PackageStream and obtain the various item groups            
            packageStream.Seek(0, SeekOrigin.Begin);
            var zipArchive = new ZipArchive(packageStream);
            PackageReader packageReader = new PackageReader(zipArchive);
            IEnumerable<FrameworkSpecificGroup> referenceItemGroups = packageReader.GetReferenceItems();
            IEnumerable<FrameworkSpecificGroup> frameworkReferenceGroups = packageReader.GetFrameworkItems();
            IEnumerable<FrameworkSpecificGroup> contentFileGroups = packageReader.GetContentItems();
            IEnumerable<FrameworkSpecificGroup> buildFileGroups = packageReader.GetBuildItems();

            // Step-3: Get the most compatible items groups for all items groups
            bool hasCompatibleItems = false;

            FrameworkSpecificGroup compatibleReferenceItemsGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, referenceItemGroups);
            FrameworkSpecificGroup compatibleFrameworkReferencesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, frameworkReferenceGroups);
            FrameworkSpecificGroup compatibleContentFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, contentFileGroups);
            FrameworkSpecificGroup compatibleBuildFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, buildFileGroups);

            hasCompatibleItems = MSBuildNuGetProjectSystemUtility.IsValid(compatibleReferenceItemsGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleFrameworkReferencesGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup);

            // Step-4: Check if there are any compatible items in the package. If not, throw
            if(!hasCompatibleItems)
            {
                throw new InvalidOperationException(
                           String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, MSBuildNuGetProjectSystem.TargetFramework));
            }

            // Step-5: Install package to FolderNuGetProject     
            await FolderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, nuGetProjectContext, token);

            // Step-6: MSBuildNuGetProjectSystem operations
            // Step-6.1: Add references to project
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleReferenceItemsGroup))
            {
                foreach (var referenceItem in compatibleReferenceItemsGroup.Items)
                {
                    if (IsAssemblyReference(referenceItem))
                    {
                        var referenceItemFullPath = Path.Combine(FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity), referenceItem);
                        var referenceName = Path.GetFileName(referenceItem);
                        if (MSBuildNuGetProjectSystem.ReferenceExists(referenceName))
                        {
                            MSBuildNuGetProjectSystem.RemoveReference(referenceName);
                        }
                        MSBuildNuGetProjectSystem.AddReference(referenceItemFullPath);
                    }
                }
            }

            // Step-6.2: Add Frameworkreferences to project
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleFrameworkReferencesGroup))
            {
                foreach (var frameworkReference in compatibleFrameworkReferencesGroup.Items)
                {
                    var frameworkReferenceName = Path.GetFileName(frameworkReference);
                    if (!MSBuildNuGetProjectSystem.ReferenceExists(frameworkReference))
                    {
                        MSBuildNuGetProjectSystem.AddFrameworkReference(frameworkReference);
                    }
                }
            }

            // Step-6.3: Add Content Files
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
            {
                MSBuildNuGetProjectSystemUtility.AddFiles(MSBuildNuGetProjectSystem,
                    zipArchive, compatibleContentFilesGroup, FileTransformers);
            }

            // Step-6.4: Add Build imports
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup))
            {
                foreach(var buildImportFile in compatibleBuildFilesGroup.Items)
                {
                    string fullImportFilePath = Path.Combine(FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity), buildImportFile);
                    MSBuildNuGetProjectSystem.AddImport(fullImportFilePath,
                        fullImportFilePath.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ? ImportLocation.Top : ImportLocation.Bottom);
                }
            }
            
            // Step-6.5: Add binding redirects. Project system is supposed to check if binding redirects are needed and add as needed
            MSBuildNuGetProjectSystem.AddBindingRedirects();

            // Step-7: Install package to PackagesConfigNuGetProject
            await PackagesConfigNuGetProject.InstallPackageAsync(packageIdentity, packageStream, nuGetProjectContext, token);

            // Step-8: Add packages.config to MSBuildNuGetProject
            MSBuildNuGetProjectSystem.AddExistingFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));

            // Step-9: Execute powershell script - install.ps1      
            IEnumerable<FrameworkSpecificGroup> toolItemGroups = packageReader.GetToolItems();
            string packageInstallPath = FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity);
            FrameworkSpecificGroup anyFrameworkToolsGroup = toolItemGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).FirstOrDefault();
            if(anyFrameworkToolsGroup != null)
            {
                string initPS1RelativePath = anyFrameworkToolsGroup.Items.Where(p =>
                    p.StartsWith(PowerShellScripts.InitPS1RelativePath)).FirstOrDefault();
                if (!String.IsNullOrEmpty(initPS1RelativePath))
                {
                    initPS1RelativePath = MSBuildNuGetProjectSystemUtility.ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);
                    MSBuildNuGetProjectSystem.ExecuteScript(packageInstallPath, initPS1RelativePath, zipArchive, this);
                }
            }

            FrameworkSpecificGroup compatibleToolItemsGroup = MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework,
                toolItemGroups);
            if(MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemsGroup))
            {
                string installPS1RelativePath = compatibleToolItemsGroup.Items.Where(p =>
                    p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Install)).FirstOrDefault();
                if(!String.IsNullOrEmpty(installPS1RelativePath))
                {                    
                    MSBuildNuGetProjectSystem.ExecuteScript(packageInstallPath, installPS1RelativePath, zipArchive, this);
                }
            }
            return true;
        }

        private static string GetPackagePath(FolderNuGetProject folderNuGetProject, PackageIdentity packageIdentity)
        {
            return folderNuGetProject.GetPackagePath(packageIdentity);
        }

        public async override Task<bool> UninstallPackageAsync(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException("packageIdentity");
            }

            if (nuGetProjectContext == null)
            {
                throw new ArgumentNullException("nuGetProjectContext");
            }

            // Step-1: Check if the package already exists after setting the nuGetProjectContext
            MSBuildNuGetProjectSystem.SetNuGetProjectContext(nuGetProjectContext);

            var packageReference = (await GetInstalledPackagesAsync(token)).Where(
                p => p.PackageIdentity.Equals(packageIdentity)).FirstOrDefault();
            if (packageReference == null)
            {
                nuGetProjectContext.Log(MessageLevel.Warning, Strings.PackageDoesNotExistInProject,
                    packageIdentity, MSBuildNuGetProjectSystem.ProjectName);
                return false;
            }

            var packageTargetFramework = packageReference.TargetFramework;
            using (var packageStream = File.OpenRead(GetPackagePath(FolderNuGetProject, packageIdentity)))
            {
                // Step-2: Create PackageReader using the PackageStream and obtain the various item groups
                // Get the package target framework instead of using project targetframework
                var zipArchive = new ZipArchive(packageStream);
                var packageReader = new PackageReader(zipArchive);

                IEnumerable<FrameworkSpecificGroup> referenceItemGroups = packageReader.GetReferenceItems();
                IEnumerable<FrameworkSpecificGroup> frameworkReferenceGroups = packageReader.GetFrameworkItems();
                IEnumerable<FrameworkSpecificGroup> contentFileGroups = packageReader.GetContentItems();
                IEnumerable<FrameworkSpecificGroup> buildFileGroups = packageReader.GetBuildItems();

                // Step-3: Get the most compatible items groups for all items groups
                FrameworkSpecificGroup compatibleReferenceItemsGroup =
                    MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(packageTargetFramework, referenceItemGroups);
                FrameworkSpecificGroup compatibleFrameworkReferencesGroup =
                    MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(packageTargetFramework, frameworkReferenceGroups);
                FrameworkSpecificGroup compatibleContentFilesGroup =
                    MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(packageTargetFramework, contentFileGroups);
                FrameworkSpecificGroup compatibleBuildFilesGroup =
                    MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(packageTargetFramework, buildFileGroups);

                // TODO: Need to handle References element??

                // Step-4: Uninstall package from packages.config
                await PackagesConfigNuGetProject.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

                // Step-5: Remove packages.config from MSBuildNuGetProject if there are no packages
                //         OR Add it again (to ensure that Source Control works), when there are some packages
                if(!(await PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).Any())
                {
                    MSBuildNuGetProjectSystem.RemoveFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));
                }
                else
                {
                    MSBuildNuGetProjectSystem.AddExistingFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));
                }

                // Step-6: Uninstall package from the msbuild project
                // Step-6.1: Remove references
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleReferenceItemsGroup))
                {
                    foreach (var item in compatibleReferenceItemsGroup.Items)
                    {
                        if (IsAssemblyReference(item))
                        {
                            MSBuildNuGetProjectSystem.RemoveReference(Path.GetFileName(item));
                        }
                    }
                }

                // Step-6.2: Framework references are never removed. This is a no-op

                // Step-6.3: Remove content files
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
                {
                    MSBuildNuGetProjectSystemUtility.DeleteFiles(MSBuildNuGetProjectSystem,
                        zipArchive,
                        (await GetInstalledPackagesAsync(token)).Select(pr => GetPackagePath(FolderNuGetProject, pr.PackageIdentity)),
                        compatibleContentFilesGroup,
                        FileTransformers);
                }

                // Step-6.4: Remove build imports
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup))
                {
                    foreach (var buildImportFile in compatibleBuildFilesGroup.Items)
                    {
                        string fullImportFilePath = Path.Combine(FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity), buildImportFile);
                        MSBuildNuGetProjectSystem.RemoveImport(fullImportFilePath);
                    }
                }

                // Step-6.5: Remove binding redirects. This is a no-op

                // Step-7: Execute powershell script - uninstall.ps1
                IEnumerable<FrameworkSpecificGroup> toolItemGroups = packageReader.GetToolItems();
                FrameworkSpecificGroup compatibleToolItemsGroup = MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework,
                    toolItemGroups);
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemsGroup))
                {
                    string uninstallPS1RelativePath = compatibleToolItemsGroup.Items.Where(p =>
                        p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Uninstall)).FirstOrDefault();
                    if (!String.IsNullOrEmpty(uninstallPS1RelativePath))
                    {
                        string packageInstallPath = FolderNuGetProject.PackagePathResolver.GetInstallPath(packageIdentity);
                        MSBuildNuGetProjectSystem.ExecuteScript(packageInstallPath, uninstallPS1RelativePath, zipArchive, this);
                    }
                }
            }

            // Step-8: Uninstall package from the folderNuGetProject
            await FolderNuGetProject.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

            return true;
        }

        private void LogTargetFrameworkInfo(PackageIdentity packageIdentity, INuGetProjectContext nuGetProjectContext,
            FrameworkSpecificGroup referenceItemsGroup, FrameworkSpecificGroup contentItemsGroup, FrameworkSpecificGroup buildFilesGroup)
        {
            if(referenceItemsGroup.Items.Any() || contentItemsGroup.Items.Any() || buildFilesGroup.Items.Any())
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

    public static class PowerShellScripts
    {
        public static readonly string Install = "install.ps1";
        public static readonly string Uninstall = "uninstall.ps1";
        public static readonly string Init = "init.ps1";
        public static readonly string InitPS1RelativePath = Constants.ToolsDirectory + Path.AltDirectorySeparatorChar + Init;
    }
}
