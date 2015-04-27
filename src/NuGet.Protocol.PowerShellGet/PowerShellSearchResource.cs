using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Protocol.PowerShellGet
{
    /// <summary>
    /// Parses NuGet V3 search json containing additional PS fields into package results.
    /// </summary>
    public class PowerShellSearchResource : INuGetResource
    {
        private readonly RawSearchResourceV3 _rawSearch;

        public PowerShellSearchResource(RawSearchResourceV3 rawSearch)
        {
            _rawSearch = rawSearch;
        }

        /// <summary>
        /// Retrieve search results
        /// </summary>
        public virtual async Task<IEnumerable<PowerShellSearchPackage>> Search(
           string searchTerm,
           SearchFilter filters,
           int skip,
           int take,
           CancellationToken cancellationToken)
        {
            IEnumerable<JObject> jsonResults = await _rawSearch.Search(searchTerm, filters, skip, take, cancellationToken);

            return jsonResults.Select(e => Parse(e)).ToArray();
        }

        /// <summary>
        /// Parse a json package entry from search into a PowerShellSearchPackage
        /// </summary>
        private static PowerShellSearchPackage Parse(JObject json)
        {
            ServerPackageMetadata basePackage = PackageMetadataParser.ParseMetadata(json);

            PSPackageMetadata psMetadata = new PSPackageMetadata(basePackage.Id, basePackage.Version)
            {
                ModuleVersion = GetVersionOrNull(json, "ModuleVersion"),
                CLRVersion = GetVersionOrNull(json, "CLRVersion"),
                CmdletsToExport = GetStringArray(json, "CmdletsToExport"),
                CompanyName = GetStringOrNull(json, "CompanyName"),
                DotNetFrameworkVersion = GetVersionOrNull(json, "DotNetFrameworkVersion"),
                DscResourcesToExport = GetStringArray(json, "DscResourcesToExport"),
                FunctionsToExport = GetStringArray(json, "FunctionsToExport"),
                Guid = GetGuidOrEmpty(json, "GUID"),
                LicenseUrl = GetUriOrNull(json, "licenseUrl"),
                PowerShellHostVersion = GetVersionOrNull(json, "PowerShellHostVersion"),
                ProcessorArchitecture = GetStringOrNull(json, "ProcessorArchitecture"),
                ProjectUrl = GetUriOrNull(json, "projectUrl"),
                ReleaseNotes = GetStringOrNull(json, "releaseNotes")
            };

            return new PowerShellSearchPackage(basePackage, psMetadata);
        }

        private static string GetStringOrNull(JObject json, string propertyName)
        {
            JToken token = null;
            string s = null;

            if (json.TryGetValue(propertyName, out token))
            {
                s = token.ToString();
            }

            return s;
        }

        private static Uri GetUriOrNull(JObject json, string propertyName)
        {
            Uri uri = null;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                uri = new Uri(s);
            }

            return uri;
        }

        private static Guid GetGuidOrEmpty(JObject json, string propertyName)
        {
            Guid guid = Guid.Empty;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                Guid.TryParse(s, out guid);
            }

            return guid;
        }

        private static IEnumerable<string> GetStringArray(JObject json, string propertyName)
        {
            JToken token = null;
            IEnumerable<string> result = null;

            if (json.TryGetValue(propertyName, out token))
            {
                JArray array = token as JArray;

                if (array != null)
                {
                    result = array.Children().Select(e => e.ToString()).ToArray();
                }
            }

            return result ?? Enumerable.Empty<string>();
        }

        private static NuGetVersion GetVersionOrNull(JObject json, string propertyName)
        {
            NuGetVersion version = null;

            string s = GetStringOrNull(json, propertyName);

            if (!String.IsNullOrEmpty(s))
            {
                NuGetVersion.TryParse(s, out version);
            }

            return version;
        }
    }
}