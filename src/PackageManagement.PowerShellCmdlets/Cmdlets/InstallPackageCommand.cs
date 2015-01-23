using NuGet.Client;
using NuGet.Packaging;
using NuGet.PackagingCore;
using NuGet.ProjectManagement;
using NuGet.Resolver;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;
        private UninstallationContext _uninstallcontext;
        private SourceRepository _currentSource = null;
        private bool _readFromPackagesConfig;
        private bool _readFromDirectPackagePath;
        private bool _isHttp;
        private bool _isNetworkAvailable;
        private string _fallbackToLocalCacheMessge = Resources.Cmdlet_FallbackToCache;
        private string _localCacheFailureMessage = Resources.Cmdlet_LocalCacheFailure;
        private string _cacheStatusMessage = String.Empty;
        private NuGetVersion _nugetVersion;
        private bool _versionSpecifiedPrerelease;

        public InstallPackageCommand()
            : base()
        {
            _isNetworkAvailable = isNetworkAvailable();
        }

        [Parameter]
        public SwitchParameter Force { get; set; }

        protected override void Preprocess()
        {
            //FallbackToCacheIfNeccessary();
            ParseUserInputForId();
            ParseUserInputForVersion();
            base.Preprocess();
        }

        protected override void ProcessRecordCore()
        {
            base.ProcessRecordCore();
            IEnumerable<PackageIdentity> identities = GetPackageIdentities();

            SubscribeToProgressEvents();
            InstallPackages(identities);
            WaitAndLogFromMessageQueue();
            UnsubscribeFromProgressEvents();
        }

        private async void InstallPackages(IEnumerable<PackageIdentity> identities)
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
                LogCore(MessageLevel.Error, ex.Message);
            }
            completeEvent.Set();
        }

        private static bool isNetworkAvailable()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        private void FallbackToCacheIfNeccessary()
        {
            /**** Fallback to Cache logic***/
            //1. Check if there is any http source (in active sources or Source switch)
            //2. Check if any one of the UNC or local sources is available (in active sources)
            //3. If none of the above is true, fallback to cache

            //Check if any of the active package source is available. This function will return true if there is any http source in active sources
            //For http sources, we will continue and fallback to cache at a later point if the resource is unavailable

            if (String.IsNullOrEmpty(Source))
            {
                bool isAnySourceAvailable = false;
                _currentSource = ActiveSourceRepository;
                isAnySourceAvailable = UriHelper.IsAnySourceAvailable(PackageSourceProvider, _isNetworkAvailable);

                //if no local or UNC source is available or no source is http, fallback to local cache
                if (!isAnySourceAvailable)
                {
                    //Source = NuGet.MachineCache.Default.Source;
                    Source = Path.Combine(Environment.ExpandEnvironmentVariables("appdata"), @"..\Local\NuGet\Cache");
                    CacheStatusMessage(_currentSource.PackageSource.Name, Source);
                }
            }

            //At this point, Source might be value from -Source switch or NuGet Local Cache
            /**** End of Fallback to Cache logic ***/
        }

        private void CacheStatusMessage(object currentSource, string cacheSource)
        {
            if (!String.IsNullOrEmpty(cacheSource))
            {
                _cacheStatusMessage = String.Format(CultureInfo.CurrentCulture, _fallbackToLocalCacheMessge, currentSource, Source);
            }
            else
            {
                _cacheStatusMessage = String.Format(CultureInfo.CurrentCulture, _localCacheFailureMessage, currentSource);
            }

            LogCore(MessageLevel.Warning, String.Format(CultureInfo.CurrentCulture, _cacheStatusMessage, ActiveSourceRepository.PackageSource, Source));
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
                        //Source = NuGet.MachineCache.Default.Source;
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
        /// Returns single package identity for resolver when Id is specified
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

        private IEnumerable<PackageIdentity> GetPackageIdentity()
        {
            PackageIdentity identity = null;
            if (_nugetVersion != null)
            {
                identity = new PackageIdentity(Id, _nugetVersion);
            }
            else
            {
                identity = PowerShellCmdletsUtility.GetLatestPackageIdentityForId(ActiveSourceRepository, Id, Project, IncludePrerelease.IsPresent);
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
        /// Resolution Context for the command
        /// </summary>
        public ResolutionContext ResolutionContext
        {
            get
            {
                bool allowPrerelease = IncludePrerelease.IsPresent || _versionSpecifiedPrerelease;
                _context = new ResolutionContext(GetDependencyBehavior(), allowPrerelease, false);
                return _context;
            }
        }

        /// <summary>
        /// Uninstall Resolution Context for the command
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
