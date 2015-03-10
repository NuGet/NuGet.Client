using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;
        private UninstallationContext _uninstallcontext;
        private bool _readFromPackagesConfig;
        private bool _readFromDirectPackagePath;
        private NuGetVersion _nugetVersion;
        private bool _versionSpecifiedPrerelease;
        private bool _allowPrerelease;
        private bool _isHttp;

        public InstallPackageCommand()
            : base()
        {
        }

        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void Preprocess()
        {
            base.Preprocess();
            ParseUserInputForId();
            ParseUserInputForVersion();
            // The following update to ActiveSourceRepository may get overwritten if the 'Id' was just a path to a nupkg
            if (_readFromDirectPackagePath)
            {
                UpdateActiveSourceRepository(Source);
            }
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            SubscribeToProgressEvents();
            if (!_readFromPackagesConfig && !_readFromDirectPackagePath && _nugetVersion == null)
            {
                Task.Run(() => InstallPackageById());
            }
            else
            {
                IEnumerable<PackageIdentity> identities = GetPackageIdentities();
                Task.Run(() => InstallPackages(identities));
            }
            WaitAndLogPackageActions();
            UnsubscribeFromProgressEvents();
        }

        /// <summary>
        /// Async call for install packages from the list of identities.
        /// </summary>
        /// <param name="identities"></param>
        private async Task InstallPackages(IEnumerable<PackageIdentity> identities)
        {
            try
            {
                foreach (PackageIdentity identity in identities)
                {
                    await InstallPackageByIdentityAsync(Project, identity, ResolutionContext, this, WhatIf.IsPresent, Force.IsPresent, UninstallContext);
                }
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, ex.Message);
            }
            finally
            {
                blockingCollection.Add(new ExecutionCompleteMessage());
            }
        }

        /// <summary>
        /// Async call for install a package by Id.
        /// </summary>
        /// <param name="identities"></param>
        private async Task InstallPackageById()
        {
            try
            {
                await InstallPackageByIdAsync(Project, Id, ResolutionContext, this, WhatIf.IsPresent, Force.IsPresent, UninstallContext);
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, ex.Message);
            }
            finally
            {
                blockingCollection.Add(new ExecutionCompleteMessage());
            }
        }

        /// <summary>
        /// Parse user input for Id parameter. 
        /// Id can be the name of a package, path to packages.config file or path to .nupkg file.
        /// </summary>
        private void ParseUserInputForId()
        {
            if (!string.IsNullOrEmpty(Id))
            {
                if (Id.ToLowerInvariant().EndsWith(Constants.PackageReferenceFile))
                {
                    _readFromPackagesConfig = true;
                }
                else if (Id.ToLowerInvariant().EndsWith(Constants.PackageExtension))
                {
                    _readFromDirectPackagePath = true;
                    if (UriHelper.IsHttpSource(Id))
                    {
                        _isHttp = true;
                        Source = Path.GetTempPath();
                    }
                    else
                    {
                        string fullPath = Path.GetFullPath(Id);
                        Source = Path.GetDirectoryName(fullPath);
                    }
                }
                else
                {
                    NormalizePackageId(Project);
                }
            }
        }

        /// <summary>
        /// Returns list of package identities for Package Manager
        /// </summary>
        /// <returns></returns>
        private IEnumerable<PackageIdentity> GetPackageIdentities()
        {
            IEnumerable<PackageIdentity> identityList = Enumerable.Empty<PackageIdentity>();
            
            if (_readFromPackagesConfig)
            {
                identityList = CreatePackageIdentitiesFromPackagesConfig();
            }
            else if (_readFromDirectPackagePath)
            {
                identityList = CreatePackageIdentityFromNupkgPath();
            }
            else
            {
                identityList = GetPackageIdentity();
            }

            return identityList;
        }

        /// <summary>
        /// Get the package identity to be installed based on package Id and Version
        /// </summary>
        /// <returns></returns>
        private IEnumerable<PackageIdentity> GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (_nugetVersion != null)
            {
                identity = new PackageIdentity(Id, _nugetVersion);
            }
            return new List<PackageIdentity>() { identity };
        }

        /// <summary>
        /// Return list of package identities parsed from packages.config
        /// </summary>
        /// <returns></returns>
        private IEnumerable<PackageIdentity> CreatePackageIdentitiesFromPackagesConfig()
        {
            IEnumerable<PackageIdentity> identities = Enumerable.Empty<PackageIdentity>();

            try
            {
                // Example: install-package https://raw.githubusercontent.com/NuGet/json-ld.net/master/src/JsonLD/packages.config
                if (Id.ToLowerInvariant().StartsWith("http"))
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Id);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    // Read data via the response stream
                    Stream resStream = response.GetResponseStream();

                    PackagesConfigReader reader = new PackagesConfigReader(resStream);
                    IEnumerable<PackageReference> packageRefs = reader.GetPackages();
                    identities = packageRefs.Select(v => v.PackageIdentity);
                }
                else
                {
                    // Example: install-package c:\temp\packages.config
                    using (FileStream stream = new FileStream(Id, FileMode.Open))
                    {
                        PackagesConfigReader reader = new PackagesConfigReader(stream);
                        IEnumerable<PackageReference> packageRefs = reader.GetPackages();
                        identities = packageRefs.Select(v => v.PackageIdentity);
                    }
                }

                // Set _allowPrerelease to true if any of the identities is prerelease version.
                if (identities != null && identities.Any())
                {
                    foreach (PackageIdentity identity in identities)
                    {
                        if (identity.Version.IsPrerelease)
                        {
                            _allowPrerelease = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogCore(MessageLevel.Error, string.Format(Resources.Cmdlet_FailToParsePackages, Id, ex.Message));
            }

            return identities;
        }

        /// <summary>
        /// Return package identity parsed from path to .nupkg file
        /// </summary>
        /// <returns></returns>
        private IEnumerable<PackageIdentity> CreatePackageIdentityFromNupkgPath()
        {
            PackageIdentity identity = null;

            try
            {
                // Example: install-package https://az320820.vo.msecnd.net/packages/microsoft.aspnet.mvc.4.0.20505.nupkg
                if (_isHttp)
                {
                    identity = ParsePackageIdentityFromNupkgPath(Id, @"/");
                    if (identity != null)
                    {
                        DirectoryInfo info = Directory.CreateDirectory(Source);
                        string downloadPath = Path.Combine(Source, identity + Constants.PackageExtension);

                        HttpClient client = new HttpClient();
                        Stream downloadStream = client.GetStreamAsync(Id).Result;
                        using (var targetPackageStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                        {
                            downloadStream.CopyToAsync(targetPackageStream).Wait();
                        }
                    }
                }
                else
                {
                    // Example: install-package c:\temp\packages\jQuery.1.10.2.nupkg
                    identity = ParsePackageIdentityFromNupkgPath(Id, @"\");
                }

                // Set _allowPrerelease to true if identity parsed is prerelease version.
                if (identity != null && identity.Version != null && identity.Version.IsPrerelease)
                {
                    _allowPrerelease = true;
                }
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, Resources.Cmdlet_FailToParsePackages, Id, ex.Message);
            }

            return new List<PackageIdentity>() { identity };
        }

        /// <summary>
        /// Parse package identity from path to .nupkg file, such as https://az320820.vo.msecnd.net/packages/microsoft.aspnet.mvc.4.0.20505.nupkg
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <returns></returns>
        private PackageIdentity ParsePackageIdentityFromNupkgPath(string path, string divider)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string lastPart = path.Split(new string[] { divider }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                lastPart = lastPart.Replace(Constants.PackageExtension, "");
                string[] parts = lastPart.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder builderForId = new StringBuilder();
                StringBuilder builderForVersion = new StringBuilder();
                foreach (string s in parts)
                {
                    int n;
                    bool isNumeric = int.TryParse(s, out n);
                    // Take pre-release versions such as EntityFramework.6.1.3-beta1 into account.
                    if ((!isNumeric || string.IsNullOrEmpty(builderForId.ToString())) && string.IsNullOrEmpty(builderForVersion.ToString()))
                    {
                        builderForId.Append(s);
                        builderForId.Append(".");
                    }
                    else
                    {
                        builderForVersion.Append(s);
                        builderForVersion.Append(".");
                    }
                }
                NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(builderForVersion.ToString().TrimEnd('.'));
                // Set _allowPrerelease to true if nVersion is prerelease version.
                if (nVersion != null && nVersion.IsPrerelease)
                {
                    _allowPrerelease = true;
                }
                return new PackageIdentity(builderForId.ToString().TrimEnd('.'), nVersion);
            }
            return null;
        }

        /// <summary>
        /// Parse user input for -Version switch.
        /// If Version is given as prerelease versions, automatically append -Prerelease
        /// </summary>
        private void ParseUserInputForVersion()
        {
            if (!string.IsNullOrEmpty(Version))
            {
                _nugetVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                if (_nugetVersion.IsPrerelease)
                {
                    _versionSpecifiedPrerelease = true;
                }
            }
            _allowPrerelease = IncludePrerelease.IsPresent || _versionSpecifiedPrerelease;
        }

        /// <summary>
        /// Resolution Context for Install-Package command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                _context = new ResolutionContext(GetDependencyBehavior(), _allowPrerelease, false);
                return _context;
            }
        }

        /// <summary>
        /// Uninstall Resolution Context for Install-Package -Force command
        /// </summary>
        public UninstallationContext UninstallContext
        {
            get
            {
                _uninstallcontext = new UninstallationContext(false, Force.IsPresent);
                return _uninstallcontext;
            }
        }

        protected override DependencyBehavior GetDependencyBehavior()
        {
            if (Force.IsPresent)
            {
                return DependencyBehavior.Ignore;
            }
            return base.GetDependencyBehavior();
        }
    }
}
