using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
            ParseUserInputForId();
            ParseUserInputForVersion();
            base.Preprocess();
            // The following update to ActiveSourceRepository may get overwritten if the 'Id' was just a path to a nupkg
            UpdateActiveSourceRepository(Source);
        }

        protected override void ProcessRecordCore()
        {
            base.ProcessRecordCore();

            SubscribeToProgressEvents();
            if (!_readFromPackagesConfig && !_readFromDirectPackagePath && _nugetVersion == null)
            {
                Task idTask = InstallPackageById();
            }
            else
            {
                IEnumerable<PackageIdentity> identities = GetPackageIdentities();
                Task identitiesTask = InstallPackages(identities);
            }
            WaitAndLogFromMessageQueue();
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
                completeEvent.Set();
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
                completeEvent.Set();
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
                        Source = Path.Combine(Environment.ExpandEnvironmentVariables("appdata"), @"..\Local\NuGet\Cache");
                    }
                    else
                    {
                        string fullPath = Path.GetFullPath(Id);
                        Source = Path.GetDirectoryName(fullPath);
                    }
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
                // TODO: Fix this
                //identityList = CreatePackageIdentityFromNupkgPath();
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
            List<PackageIdentity> identities = new List<PackageIdentity>();
            IEnumerable<PackageIdentity> parsedIdentities = null;

            try
            {
                // Example: install-package2 https://raw.githubusercontent.com/NuGet/json-ld.net/master/src/JsonLD/packages.config
                if (Id.ToLowerInvariant().StartsWith("http"))
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Id);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    // Read data via the response stream
                    Stream resStream = response.GetResponseStream();

                    PackagesConfigReader reader = new PackagesConfigReader(resStream);
                    IEnumerable<PackageReference> packageRefs = reader.GetPackages();
                    parsedIdentities = packageRefs.Select(v => v.PackageIdentity);
                }
                else
                {
                    // Example: install-package2 c:\temp\packages.config
                    using (FileStream stream = new FileStream(Id, FileMode.Open))
                    {
                        PackagesConfigReader reader = new PackagesConfigReader(stream);
                        IEnumerable<PackageReference> packageRefs = reader.GetPackages();
                        parsedIdentities = packageRefs.Select(v => v.PackageIdentity);
                        if (stream != null)
                        {
                            stream.Close();
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

        // TODO: Fix the logic here.
        private IEnumerable<PackageIdentity> CreatePackageIdentityFromNupkgPath()
        {
            PackageIdentity identity = null;

            try
            {
                // Example: install-package2 https://az320820.vo.msecnd.net/packages/microsoft.aspnet.mvc.4.0.20505.nupkg
                if (_isHttp)
                {
                    PackageIdentity packageIdentity = ParsePackageIdentityFromHttpSource(Id);
                    Uri downloadUri = new Uri(Id);
                    using (var targetPackageStream = new MemoryStream())
                    {
                        UpdateActiveSourceRepository(Id);
                        PackageDownloader.GetPackageStream(ActiveSourceRepository, packageIdentity, targetPackageStream).Wait();
                    }
                }
                else
                {
                    // Example: install-package2 c:\temp\packages\jQuery.1.10.2.nupkg
                    string fullPath = Path.GetFullPath(Id);
                    //package = new OptimizedZipPackage(fullPath);
                }
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, Resources.Cmdlet_FailToParsePackages, Id, ex.Message);
            }

            return new List<PackageIdentity>() { identity };
        }

        /// <summary>
        /// Parse package identity from the http source, such as https://az320820.vo.msecnd.net/packages/microsoft.aspnet.mvc.4.0.20505.nupkg
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <returns></returns>
        private PackageIdentity ParsePackageIdentityFromHttpSource(string sourceUrl)
        {
            if (!string.IsNullOrEmpty(sourceUrl))
            {
                string lastPart = sourceUrl.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                lastPart = lastPart.Replace(Constants.PackageExtension, "");
                string[] parts = lastPart.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder builderForId = new StringBuilder();
                StringBuilder builderForVersion = new StringBuilder();
                foreach (string s in parts)
                {
                    int n;
                    bool isNumeric = int.TryParse(s, out n);
                    if (!isNumeric)
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
        }

        /// <summary>
        /// Resolution Context for Install-Package command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                _allowPrerelease = IncludePrerelease.IsPresent || _versionSpecifiedPrerelease;
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
