// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using NuGet.PackageManagement.UI;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Open, "PackagePage", DefaultParameterSetName = ParameterAttribute.AllParameterSets, SupportsShouldProcess = true)]
    public class OpenPackagePageCommand : NuGetPowerShellBaseCommand
    {
        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0)]
        public string Id { get; set; }

        [Parameter(Position = 1)]
        [ValidateNotNull]
        public string Version { get; set; }

        [Parameter]
        [ValidateNotNullOrEmpty]
        public virtual string Source { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "License")]
        public SwitchParameter License { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "ReportAbuse")]
        public SwitchParameter ReportAbuse { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        [Parameter]
        public SwitchParameter IncludePrerelease { get; set; }

        private void Preprocess()
        {
            UpdateActiveSourceRepository(Source);
            LogCore(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_CommandRemoved, "Open-PackagePage"));
        }

        [SuppressMessage("Microsoft.Design", "CA1031")]
        protected override void ProcessRecordCore()
        {
            Preprocess();

            IPackageSearchMetadata package = null;
            try
            {
                var metadata = NuGetUIThreadHelper.JoinableTaskFactory.Run(() => GetPackagesFromRemoteSourceAsync(Id, IncludePrerelease.IsPresent));

                if (!string.IsNullOrEmpty(Version))
                {
                    NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                    metadata = metadata.Where(p => p.Identity.Version == nVersion);
                }
                package = metadata
                    .OrderByDescending(v => v.Identity.Version)
                    .FirstOrDefault();
            }
            catch
            {
            }

            if (package != null
                && package.Identity != null)
            {
                Uri targetUrl = null;
                if (License.IsPresent)
                {
                    targetUrl = package.LicenseUrl;
                }
                else if (ReportAbuse.IsPresent)
                {
                    targetUrl = package.ReportAbuseUrl;
                }
                else
                {
                    targetUrl = package.ProjectUrl;
                }

                if (targetUrl != null)
                {
                    OpenUrl(targetUrl);

                    if (PassThru.IsPresent)
                    {
                        WriteObject(targetUrl);
                    }
                }
                else
                {
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_UrlMissing, package.Identity.Id + " " + package.Identity.Version));
                }
            }
            else
            {
                // show appropriate error message depending on whether Version parameter is set.
                if (string.IsNullOrEmpty(Version))
                {
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_PackageIdNotFound, Id));
                }
                else
                {
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_PackageIdAndVersionNotFound, Id, Version));
                }
            }
        }

        private void OpenUrl(Uri targetUrl)
        {
            // ask for confirmation or if WhatIf is specified
            if (ShouldProcess(targetUrl.OriginalString, Resources.Cmdlet_OpenPackagePageAction))
            {
                UriHelper.OpenExternalLink(targetUrl);
            }
        }
    }
}
