using Newtonsoft.Json.Linq;
using NuGet.NuGet.PackageManagement.PowerShellCmdlets;
using NuGet.Versioning;
using System;
using System.Globalization;
using System.Management.Automation;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    [Cmdlet(VerbsCommon.Open, "PackagePage", DefaultParameterSetName = ParameterAttribute.AllParameterSets, SupportsShouldProcess = true)]
    public class OpenPackagePageCommand : NuGetPowerShellBaseCommand
    {
        public OpenPackagePageCommand() 
        {
        }

        [Parameter(Mandatory = true, ValueFromPipelineByPropertyName = true, Position = 0)]
        public string Id { get; set; }

        [Parameter(Position = 1)]
        [ValidateNotNull]
        public SemanticVersion Version { get; set; }

        [Parameter(Position = 2)]
        [ValidateNotNullOrEmpty]
        public string Source { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "License")]
        public SwitchParameter License { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "ReportAbuse")]
        public SwitchParameter ReportAbuse { get; set; }

        [Parameter]
        public SwitchParameter PassThru { get; set; }

        private void Preprocess()
        {
        }

        protected override void ProcessRecordCore()
        {
            Preprocess();

            JObject package = null;
            try
            {
                //if (Version == null || string.IsNullOrEmpty(Version.ToString()))
                //{
                //    string latestVersion = PowerShellPackage.GetLastestVersionForPackage(ActiveSourceRepository, Id, Enumerable.Empty<FrameworkName>(), true);
                //    Version = new SemanticVersion(latestVersion);
                //}
                //package = PowerShellPackage.GetPackageByIdAndVersion(ActiveSourceRepository, Id, Version.ToString(), true);
            }
            catch (InvalidOperationException) { }

            if (package != null)
            {
                Uri targetUrl = null;
                if (License.IsPresent)
                {
                    //targetUrl = GetUri(package, Properties.LicenseUrl);
                }
                else if (ReportAbuse.IsPresent)
                {
                    // TODO: ReportAbuseUrl is not exposed in the package registration. 
                    //targetUrl = GetUri(package, Properties.ReportAbuseUrl);
                    targetUrl = null;
                }
                else
                {
                    //targetUrl = GetUri(package, Properties.ProjectUrl);
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
                if (Version == null)
                {
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_PackageIdNotFound, Id));
                }
                else
                {
                    WriteError(String.Format(CultureInfo.CurrentCulture, Resources.Cmdlet_PackageIdAndVersionNotFound, Id, Version));
                }
            }
        }

        // TODO: This should be a common method for UI and PoweShell. 
        private static Uri GetUri(JObject json, string property)
        {
            if (json[property] == null)
            {
                return null;
            }
            string str = json[property].ToString();
            if (String.IsNullOrEmpty(str))
            {
                return null;
            }
            return new Uri(str.TrimEnd('/'));
        }

        private void OpenUrl(Uri targetUrl)
        {
            // ask for confirmation or if WhatIf is specified
            if (ShouldProcess(targetUrl.OriginalString, Resources.Cmdlet_OpenPackagePageAction))
            {
                //UriHelper.OpenExternalLink(targetUrl);
            }
        }
    }
}
