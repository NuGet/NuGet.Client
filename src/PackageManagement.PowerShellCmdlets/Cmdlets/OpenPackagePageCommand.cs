using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using NuGet.ProjectManagement;
using NuGet.Protocol.VisualStudio;
using NuGet.Versioning;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Open, "PackagePage", DefaultParameterSetName = ParameterAttribute.AllParameterSets, SupportsShouldProcess = true)]
    public class OpenPackagePageCommand : NuGetPowerShellBaseCommand
    {
        public OpenPackagePageCommand()
            : base()
        {
        }

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

        protected override void Preprocess()
        {
            base.Preprocess();
            UpdateActiveSourceRepository(Source);
            LogCore(MessageLevel.Warning, string.Format(Resources.Cmdlet_CommandRemoved, "Open-PackagePage"));
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            UIPackageMetadata package = null;
            try
            {
                UIMetadataResource resource = ActiveSourceRepository.GetResource<UIMetadataResource>();
                Task<IEnumerable<UIPackageMetadata>> task = resource.GetMetadata(Id, IncludePrerelease.IsPresent, false, CancellationToken.None);
                var metadata = task.Result;
                if (!string.IsNullOrEmpty(Version))
                {
                    NuGetVersion nVersion = PowerShellCmdletsUtility.GetNuGetVersionFromString(Version);
                    metadata = metadata.Where(p => p.Identity.Version == nVersion);
                }
                package = metadata.Where(p => string.Equals(p.Identity.Id, Id, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(v => v.Identity.Version)
                    .FirstOrDefault();
            }
            catch (Exception)
            {
            }

            if (package != null)
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
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_UrlMissing, Id + " " + Version));
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
