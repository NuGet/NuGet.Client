﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Text;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.PackageManagement.UI;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsLifecycle.Install, "Package")]
    public class InstallPackageCommand : PackageActionBaseCommand
    {
        private ResolutionContext _context;
        private bool _readFromPackagesConfig;
        private bool _readFromDirectPackagePath;
        private NuGetVersion _nugetVersion;
        private bool _versionSpecifiedPrerelease;
        private bool _allowPrerelease;
        private bool _isHttp;

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

            ActionType = NuGetActionType.Install;
        }

        protected override void ProcessRecordCore()
        {
            var startTime = DateTimeOffset.Now;

            // Set to log telemetry granular events for this install operation 
            TelemetryService = new TelemetryServiceHelper();

            // start timer for telemetry event
            TelemetryUtility.StartorResumeTimer();

            using (var lck = _lockService.AcquireLock())
            {
                Preprocess();

                SubscribeToProgressEvents();
                if (!_readFromPackagesConfig
                    && !_readFromDirectPackagePath
                    && _nugetVersion == null)
                {
                    Task.Run(InstallPackageByIdAsync);
                }
                else
                {
                    var identities = GetPackageIdentities();
                    Task.Run(() => InstallPackagesAsync(identities));
                }
                WaitAndLogPackageActions();
                UnsubscribeFromProgressEvents();
            }

            // stop timer for telemetry event and create action telemetry event instance
            TelemetryUtility.StopTimer();
            var actionTelemetryEvent = TelemetryUtility.GetActionTelemetryEvent(
                new[] { Project },
                NuGetOperationType.Install,
                OperationSource.PMC,
                startTime,
                _status,
                _packageCount,
                TelemetryUtility.GetTimerElapsedTimeInSeconds());

            // emit telemetry event along with granular level events
            ActionsTelemetryService.Instance.EmitActionEvent(actionTelemetryEvent, TelemetryService.TelemetryEvents);
        }

        /// <summary>
        /// Async call for install packages from the list of identities.
        /// </summary>
        /// <param name="identities"></param>
        private async Task InstallPackagesAsync(IEnumerable<PackageIdentity> identities)
        {
            try
            {
                foreach (PackageIdentity identity in identities)
                {
                    await InstallPackageByIdentityAsync(Project, identity, ResolutionContext, this, WhatIf.IsPresent);
                }
            }
            catch (Exception ex)
            {
                // set nuget operation status to failed when an exception is thrown
                _status = NuGetOperationStatus.Failed;
                Log(MessageLevel.Error, ExceptionUtilities.DisplayMessage(ex));
            }
            finally
            {
                BlockingCollection.Add(new ExecutionCompleteMessage());
            }
        }

        /// <summary>
        /// Async call for install a package by Id.
        /// </summary>
        /// <param name="identities"></param>
        private async Task InstallPackageByIdAsync()
        {
            try
            {
                await InstallPackageByIdAsync(Project, Id, ResolutionContext, this, WhatIf.IsPresent);
            }
            catch (FatalProtocolException ex)
            {
                _status = NuGetOperationStatus.Failed;

                // Additional information about the exception can be observed by using the -verbose switch with the install-package command
                Log(MessageLevel.Debug, ExceptionUtilities.DisplayMessage(ex));

                // Wrap FatalProtocolException coming from the server with a user friendly message
                var error = String.Format(CultureInfo.CurrentUICulture, Strings.Exception_PackageNotFound, Id, Source);
                Log(MessageLevel.Error, error);
            }
            catch (Exception ex)
            {
                _status = NuGetOperationStatus.Failed;
                Log(MessageLevel.Error, ExceptionUtilities.DisplayMessage(ex));
            }
            finally
            {
                BlockingCollection.Add(new ExecutionCompleteMessage());
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
                if (Id.EndsWith(NuGetConstants.PackageReferenceFile, StringComparison.OrdinalIgnoreCase))
                {
                    _readFromPackagesConfig = true;
                }
                else if (Id.EndsWith(PackagingCoreConstants.NupkgExtension, StringComparison.OrdinalIgnoreCase))
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
            return new List<PackageIdentity> { identity };
        }

        /// <summary>
        /// Return list of package identities parsed from packages.config
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1031")]
        private IEnumerable<PackageIdentity> CreatePackageIdentitiesFromPackagesConfig()
        {
            IEnumerable<PackageIdentity> identities = Enumerable.Empty<PackageIdentity>();

            try
            {
                // Example: install-package https://raw.githubusercontent.com/NuGet/json-ld.net/master/src/JsonLD/packages.config
                if (UriHelper.IsHttpSource(Id))
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Id);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    // Read data via the response stream
                    Stream resStream = response.GetResponseStream();

                    PackagesConfigReader reader = new PackagesConfigReader(resStream);
                    var packageRefs = reader.GetPackages();
                    identities = packageRefs.Select(v => v.PackageIdentity);
                }
                else
                {
                    // Example: install-package c:\temp\packages.config
                    using (FileStream stream = new FileStream(Id, FileMode.Open))
                    {
                        PackagesConfigReader reader = new PackagesConfigReader(stream);
                        var packageRefs = reader.GetPackages();
                        identities = packageRefs.Select(v => v.PackageIdentity);
                    }
                }

                // Set _allowPrerelease to true if any of the identities is prerelease version.
                if (identities != null
                    && identities.Any())
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
                LogCore(MessageLevel.Error, string.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_FailToParsePackages, Id, ex.Message));
            }

            return identities;
        }

        /// <summary>
        /// Return package identity parsed from path to .nupkg file
        /// </summary>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Design", "CA1031")]
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
                        Directory.CreateDirectory(Source);
                        string downloadPath = Path.Combine(Source, identity + PackagingCoreConstants.NupkgExtension);

                        using (var client = new System.Net.Http.HttpClient())
                        {
                            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                            {
                                using (Stream downloadStream = await client.GetStreamAsync(Id))
                                {
                                    using (var targetPackageStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write))
                                    {
                                        await downloadStream.CopyToAsync(targetPackageStream);
                                    }
                                }
                            });
                        }
                    }
                }
                else
                {
                    // Example: install-package c:\temp\packages\jQuery.1.10.2.nupkg
                    identity = ParsePackageIdentityFromNupkgPath(Id, @"\");
                }

                // Set _allowPrerelease to true if identity parsed is prerelease version.
                if (identity != null
                    && identity.Version != null
                    && identity.Version.IsPrerelease)
                {
                    _allowPrerelease = true;
                }
            }
            catch (Exception ex)
            {
                Log(MessageLevel.Error, Resources.Cmdlet_FailToParsePackages, Id, ex.Message);
            }

            return new List<PackageIdentity> { identity };
        }

        /// <summary>
        /// Parse package identity from path to .nupkg file, such as
        /// https://az320820.vo.msecnd.net/packages/microsoft.aspnet.mvc.4.0.20505.nupkg
        /// </summary>
        /// <param name="sourceUrl"></param>
        /// <returns></returns>
        private PackageIdentity ParsePackageIdentityFromNupkgPath(string path, string divider)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string lastPart = path.Split(new[] { divider }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                lastPart = lastPart.Replace(PackagingCoreConstants.NupkgExtension, "");
                string[] parts = lastPart.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder builderForId = new StringBuilder();
                StringBuilder builderForVersion = new StringBuilder();
                foreach (string s in parts)
                {
                    int n;
                    bool isNumeric = int.TryParse(s, out n);
                    // Take pre-release versions such as EntityFramework.6.1.3-beta1 into account.
                    if ((!isNumeric || string.IsNullOrEmpty(builderForId.ToString()))
                        && string.IsNullOrEmpty(builderForVersion.ToString()))
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
                if (nVersion != null
                    && nVersion.IsPrerelease)
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
                // ResolutionContext contains a cache, this should only be created once per command
                if (_context == null)
                {
                    _context = new ResolutionContext(GetDependencyBehavior(), _allowPrerelease, false, VersionConstraints.None);
                }

                return _context;
            }
        }
    }
}
