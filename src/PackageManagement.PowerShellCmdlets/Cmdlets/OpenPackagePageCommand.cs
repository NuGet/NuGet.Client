using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

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
            GetActiveSourceRepository(Source);
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            UIPackageMetadata package = null;
            try
            {
                UIMetadataResource resource = ActiveSourceRepository.GetResource<UIMetadataResource>();
                IEnumerable<UIPackageMetadata> metadata = Enumerable.Empty<UIPackageMetadata>();
                if (string.IsNullOrEmpty(Version))
                {
                    Task<IEnumerable<UIPackageMetadata>> task = resource.GetMetadata(Id, IncludePrerelease.IsPresent, false, CancellationToken.None);
                    metadata = task.Result;
                }
                else
                {
                    NuGetVersion nVersion;
                    bool success = NuGetVersion.TryParse(Version, out nVersion);
                    if (success)
                    {
                        PackageIdentity identity = new PackageIdentity(Id, nVersion);
                        Task<IEnumerable<UIPackageMetadata>> task = resource.GetMetadata(identity, IncludePrerelease.IsPresent, false, CancellationToken.None);
                        metadata = task.Result;
                    }
                }
                package = metadata.Where(p => string.Equals(p.Identity.Id, Id, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            }
            catch (Exception)
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

            if (package != null)
            {
                Uri targetUrl = null;
                if (License.IsPresent)
                {
                    targetUrl = GetUri(package, "licenseUrl");
                }
                else if (ReportAbuse.IsPresent)
                {
                    //targetUrl = GetUri(package, Properties.ReportAbuseUrl);
                }
                else
                {
                    targetUrl = GetUri(package, "projectUrl");
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
        }

        // TODO: This should be a common method for UI and PoweShell. 
        private static Uri GetUri(UIPackageMetadata metadata, string property)
        {
            if (property == "licenseUrl")
            {
                return metadata.LicenseUrl;
            }
            else if (property == "projectUrl")
            {
                return metadata.ProjectUrl;
            }
            // TODO: Fix ReportAbuseUrl
            else
            {
                return null;
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
