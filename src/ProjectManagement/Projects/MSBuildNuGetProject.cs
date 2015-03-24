using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageInstalling;

        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageInstalled;

        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageUninstalling;

        /// <summary>
        /// Event to be raised while installing a package
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageUninstalled;

        /// <summary>
        /// Event to be raised while added references to project
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageReferenceAdded;

        /// <summary>
        /// Event to be raised while removed references from project
        /// </summary>
        public event EventHandler<PackageEventArgs> PackageReferenceRemoved;

        public IMSBuildNuGetProjectSystem MSBuildNuGetProjectSystem { get; private set; }
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

        private static IMSBuildNuGetProjectContext GetMSBuildNuGetProjectContext(INuGetProjectContext nuGetProjectContext)
        {
            if (nuGetProjectContext != null)
            {
                var msBuildNuGetProjectContext = nuGetProjectContext as IMSBuildNuGetProjectContext;
                if (msBuildNuGetProjectContext != null)
                {
                    return msBuildNuGetProjectContext;
                }
            }

            return null;
        }

        private static bool IsBindingRedirectsDisabled(INuGetProjectContext nuGetProjectContext)
        {
            var msBuildNuGetProjectContext = nuGetProjectContext as IMSBuildNuGetProjectContext;
            return msBuildNuGetProjectContext != null ? msBuildNuGetProjectContext.BindingRedirectsDisabled : false;
        }

        private static bool IsSkipAssemblyReferences(INuGetProjectContext nuGetProjectContext)
        {
            var msBuildNuGetProjectContext = nuGetProjectContext as IMSBuildNuGetProjectContext;
            return msBuildNuGetProjectContext != null ? msBuildNuGetProjectContext.SkipAssemblyReferences : false;
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
            IEnumerable<FrameworkSpecificGroup> toolItemGroups = packageReader.GetToolItems();            

            // Step-3: Get the most compatible items groups for all items groups
            bool hasCompatibleProjectLevelContent = false;

            FrameworkSpecificGroup compatibleReferenceItemsGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, referenceItemGroups);
            FrameworkSpecificGroup compatibleFrameworkReferencesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, frameworkReferenceGroups);
            FrameworkSpecificGroup compatibleContentFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, contentFileGroups);
            FrameworkSpecificGroup compatibleBuildFilesGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, buildFileGroups);
            FrameworkSpecificGroup compatibleToolItemsGroup =
                MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework, toolItemGroups);

            hasCompatibleProjectLevelContent = MSBuildNuGetProjectSystemUtility.IsValid(compatibleReferenceItemsGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleFrameworkReferencesGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup) ||
                MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup);

            // Check if package has any content for project
            bool hasProjectLevelContent = referenceItemGroups.Any() || frameworkReferenceGroups.Any()
                || contentFileGroups.Any() || buildFileGroups.Any();            
            bool onlyHasCompatibleTools = false;
            bool onlyHasDependencies = false;

            if(!hasProjectLevelContent)
            {
                // Since it does not have project-level content, check if it has dependencies or compatible tools
                // Note that we are not checking if it has compatible project level content, but, just that it has project level content
                // If the package has project-level content, but nothing compatible, we still need to throw
                // If a package does not have any project-level content, it can be a
                // Legacy solution level packages which only has compatible tools group
                onlyHasCompatibleTools = MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemsGroup) && compatibleToolItemsGroup.Items.Any();
                if(!onlyHasCompatibleTools)
                {
                    // If it does not have compatible tool items either, check if it at least has dependencies
                    onlyHasDependencies = packageReader.GetPackageDependencies().Any();
                }
            }
            else
            {
                string shortFramework = MSBuildNuGetProjectSystem.TargetFramework.GetShortFolderName();
                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                    this.GetMetadata<string>(NuGetProjectMetadataKeys.Name), shortFramework);
            }

            // Step-4: Check if there are any compatible items in the package or that this is not a package with only tools group. If not, throw
            if (!hasCompatibleProjectLevelContent && !onlyHasCompatibleTools && !onlyHasDependencies)
            {
                throw new InvalidOperationException(
                           String.Format(CultureInfo.CurrentCulture,
                           Strings.UnableToFindCompatibleItems, packageIdentity, MSBuildNuGetProjectSystem.TargetFramework));
            }

            if (hasCompatibleProjectLevelContent)
            {
                string shortFramework = MSBuildNuGetProjectSystem.TargetFramework.GetShortFolderName();
                nuGetProjectContext.Log(MessageLevel.Debug, Strings.Debug_TargetFrameworkInfoPrefix, packageIdentity,
                    this.GetMetadata<string>(NuGetProjectMetadataKeys.Name), shortFramework);
            }
            else if (onlyHasCompatibleTools)
            {
                nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageWithOnlyToolsGroup, packageIdentity,
                    this.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
            }
            else if (onlyHasDependencies)
            {
                nuGetProjectContext.Log(MessageLevel.Info, Strings.AddingPackageWithOnlyDependencies, packageIdentity,
                    this.GetMetadata<string>(NuGetProjectMetadataKeys.Name));
            }

            // Step-5: Raise PackageInstalling event
            // At this point, GetInstalledPath is pointless since the package is, likely, not already installed. It will be empty
            // Using PackagePathResolver.GetInstallPath would be wrong, since, package version from the nuspec is always used
            var packageEventArgs = new PackageEventArgs(FolderNuGetProject, packageIdentity, FolderNuGetProject.GetInstalledPath(packageIdentity));
            if (PackageInstalling != null)
            {
                PackageInstalling(this, packageEventArgs);
            }
            PackageEventsProvider.Instance.NotifyInstalling(packageEventArgs);

            // Step-6: Install package to FolderNuGetProject     
            await FolderNuGetProject.InstallPackageAsync(packageIdentity, packageStream, nuGetProjectContext, token);

            // Step-7: Raise PackageInstalled event
            // Call GetInstalledPath again, to get the package installed path
            packageEventArgs = new PackageEventArgs(FolderNuGetProject, packageIdentity, FolderNuGetProject.GetInstalledPath(packageIdentity));
            if (PackageInstalled != null)
            {
                PackageInstalled(this, packageEventArgs);
            }
            PackageEventsProvider.Instance.NotifyInstalled(packageEventArgs);

            // Step-8: MSBuildNuGetProjectSystem operations
            // Step-8.1: Add references to project
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleReferenceItemsGroup) && !IsSkipAssemblyReferences(nuGetProjectContext))
            {
                foreach (var referenceItem in compatibleReferenceItemsGroup.Items)
                {
                    if (IsAssemblyReference(referenceItem))
                    {
                        var referenceItemFullPath = Path.Combine(FolderNuGetProject.GetInstalledPath(packageIdentity), referenceItem);
                        var referenceName = Path.GetFileName(referenceItem);
                        if (MSBuildNuGetProjectSystem.ReferenceExists(referenceName))
                        {
                            MSBuildNuGetProjectSystem.RemoveReference(referenceName);
                        }
                        MSBuildNuGetProjectSystem.AddReference(referenceItemFullPath);
                    }
                }
            }

            // Step-8.2: Add Frameworkreferences to project
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

            // Step-8.3: Add Content Files
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
            {
                MSBuildNuGetProjectSystemUtility.AddFiles(MSBuildNuGetProjectSystem,
                    zipArchive, compatibleContentFilesGroup, FileTransformers);
            }

            // Step-8.4: Add Build imports
            if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup))
            {
                foreach(var buildImportFile in compatibleBuildFilesGroup.Items)
                {
                    string fullImportFilePath = Path.Combine(FolderNuGetProject.GetInstalledPath(packageIdentity), buildImportFile);
                    MSBuildNuGetProjectSystem.AddImport(fullImportFilePath,
                        fullImportFilePath.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ? ImportLocation.Top : ImportLocation.Bottom);
                }
            }

            // Step-9: Install package to PackagesConfigNuGetProject
            await PackagesConfigNuGetProject.InstallPackageAsync(packageIdentity, packageStream, nuGetProjectContext, token);

            // Step-10: Add packages.config to MSBuildNuGetProject
            MSBuildNuGetProjectSystem.AddExistingFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));

            // Step 11: Raise PackageReferenceAdded event
            if (PackageReferenceAdded != null)
            {
                PackageReferenceAdded(this, packageEventArgs);
            }
            PackageEventsProvider.Instance.NotifyReferenceAdded(packageEventArgs);

            // Step-12: Execute powershell script - install.ps1
            string packageInstallPath = FolderNuGetProject.GetInstalledPath(packageIdentity);
            FrameworkSpecificGroup anyFrameworkToolsGroup = toolItemGroups.Where(g => g.TargetFramework.Equals(NuGetFramework.AnyFramework)).FirstOrDefault();
            if(anyFrameworkToolsGroup != null)
            {
                string initPS1RelativePath = anyFrameworkToolsGroup.Items.Where(p =>
                    p.StartsWith(PowerShellScripts.InitPS1RelativePath)).FirstOrDefault();
                if (!String.IsNullOrEmpty(initPS1RelativePath))
                {
                    initPS1RelativePath = PathUtility.ReplaceAltDirSeparatorWithDirSeparator(initPS1RelativePath);
                    await MSBuildNuGetProjectSystem.ExecuteScriptAsync(packageInstallPath, initPS1RelativePath, zipArchive, this);
                }
            }

            if(MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemsGroup))
            {
                string installPS1RelativePath = compatibleToolItemsGroup.Items.Where(p =>
                    p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Install)).FirstOrDefault();
                if(!String.IsNullOrEmpty(installPS1RelativePath))
                {                    
                    await MSBuildNuGetProjectSystem.ExecuteScriptAsync(packageInstallPath, installPS1RelativePath, zipArchive, this);
                }
            }
            return true;
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

            var packageTargetFramework = packageReference.TargetFramework ?? NuGetFramework.UnsupportedFramework;
            var packageEventArgs = new PackageEventArgs(FolderNuGetProject, packageIdentity, FolderNuGetProject.GetInstalledPath(packageIdentity));
            using (var packageStream = File.OpenRead(FolderNuGetProject.GetInstalledPackageFilePath(packageIdentity)))
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

                // Step-4: Raise PackageUninstalling event
                if (PackageUninstalling != null)
                {
                    PackageUninstalling(this, packageEventArgs);
                }
                PackageEventsProvider.Instance.NotifyUninstalling(packageEventArgs);

                // Step-5: Uninstall package from packages.config
                await PackagesConfigNuGetProject.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

                // Step-6: Remove packages.config from MSBuildNuGetProject if there are no packages
                //         OR Add it again (to ensure that Source Control works), when there are some packages
                if(!(await PackagesConfigNuGetProject.GetInstalledPackagesAsync(token)).Any())
                {
                    MSBuildNuGetProjectSystem.RemoveFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));
                }
                else
                {
                    MSBuildNuGetProjectSystem.AddExistingFile(Path.GetFileName(PackagesConfigNuGetProject.FullPath));
                }

                // Step-7: Uninstall package from the msbuild project
                // Step-7.1: Remove references
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

                // Step-7.2: Framework references are never removed. This is a no-op

                // Step-7.3: Remove content files
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleContentFilesGroup))
                {
                    MSBuildNuGetProjectSystemUtility.DeleteFiles(MSBuildNuGetProjectSystem,
                        zipArchive,
                        (await GetInstalledPackagesAsync(token)).Select(pr => FolderNuGetProject.GetInstalledPackageFilePath(pr.PackageIdentity)),
                        compatibleContentFilesGroup,
                        FileTransformers);
                }

                // Step-7.4: Remove build imports
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleBuildFilesGroup))
                {
                    foreach (var buildImportFile in compatibleBuildFilesGroup.Items)
                    {
                        string fullImportFilePath = Path.Combine(FolderNuGetProject.GetInstalledPath(packageIdentity), buildImportFile);
                        MSBuildNuGetProjectSystem.RemoveImport(fullImportFilePath);
                    }
                }

                // Step-7.5: Remove binding redirects. This is a no-op

                // Step-8: Raise PackageReferenceRemoved event
                if (PackageReferenceRemoved != null)
                {
                    PackageReferenceRemoved(this, packageEventArgs);
                }
                PackageEventsProvider.Instance.NotifyReferenceRemoved(packageEventArgs);

                // Step-9: Execute powershell script - uninstall.ps1
                IEnumerable<FrameworkSpecificGroup> toolItemGroups = packageReader.GetToolItems();
                FrameworkSpecificGroup compatibleToolItemsGroup = MSBuildNuGetProjectSystemUtility.GetMostCompatibleGroup(MSBuildNuGetProjectSystem.TargetFramework,
                    toolItemGroups);
                if (MSBuildNuGetProjectSystemUtility.IsValid(compatibleToolItemsGroup))
                {
                    string uninstallPS1RelativePath = compatibleToolItemsGroup.Items.Where(p =>
                        p.EndsWith(Path.DirectorySeparatorChar + PowerShellScripts.Uninstall)).FirstOrDefault();
                    if (!String.IsNullOrEmpty(uninstallPS1RelativePath))
                    {
                        string packageInstallPath = FolderNuGetProject.GetInstalledPath(packageIdentity);
                        await MSBuildNuGetProjectSystem.ExecuteScriptAsync(packageInstallPath, uninstallPS1RelativePath, zipArchive, this);
                    }
                }
            }

            // Step-10: Uninstall package from the folderNuGetProject
            await FolderNuGetProject.UninstallPackageAsync(packageIdentity, nuGetProjectContext, token);

            // Step-11: Raise PackageUninstalled event
            if (PackageUninstalled != null)
            {
                PackageUninstalled(this, packageEventArgs);
            }
            PackageEventsProvider.Instance.NotifyUninstalled(packageEventArgs);

            return true;
        }

        public override Task PostProcessAsync(INuGetProjectContext nuGetProjectContext, CancellationToken token)
        {
            if (!IsBindingRedirectsDisabled(nuGetProjectContext))
            {
                MSBuildNuGetProjectSystem.AddBindingRedirects();
            }
            return base.PostProcessAsync(nuGetProjectContext, token);
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
