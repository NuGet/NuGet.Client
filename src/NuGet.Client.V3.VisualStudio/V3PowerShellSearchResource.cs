using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client.VisualStudio.Models;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3PowerShellSearchResource :V3Resource, IPowerShellSearch
    {
        public V3PowerShellSearchResource(V3Resource v3Resource)
            : base(v3Resource) { }
        public async Task<IEnumerable<PowershellSearchMetadata>> GetSearchResultsForPowerShell(string searchTerm, SearchFilter filters, int skip, int take, System.Threading.CancellationToken cancellationToken)
        {
            IEnumerable<JObject> searchResultJsonObjects = await V3Client.Search(searchTerm, filters, skip, take, cancellationToken);
            List<PowershellSearchMetadata> powerShellSearchResults = new List<PowershellSearchMetadata>();
            foreach (JObject searchResultJson in searchResultJsonObjects)
                powerShellSearchResults.Add(GetPowerShellSearchResult(searchResultJson, filters.IncludePrerelease));
            return powerShellSearchResults;
        }

        private PowershellSearchMetadata GetPowerShellSearchResult(JObject package, bool includePrerelease)
        {
            string id = package.Value<string>(Properties.PackageId);
            NuGetVersion version = NuGetVersion.Parse(package.Value<string>(Properties.LatestVersion));         

            // get other versions
            var versionList = new List<NuGetVersion>();
            var versions = package.Value<JArray>(Properties.Versions);
            if (versions != null)
            {
                if (versions[0].Type == JTokenType.String)
                {
                    // TODO: this part should be removed once the new end point is up and running.
                    versionList = versions
                        .Select(v => NuGetVersion.Parse(v.Value<string>()))
                        .ToList();
                }
                else
                {
                    versionList = versions
                        .Select(v => NuGetVersion.Parse(v.Value<string>("version")))
                        .ToList();
                }

                if (!includePrerelease)
                {
                    // remove prerelease version if includePrelease is false
                    versionList.RemoveAll(v => v.IsPrerelease);
                }
            }
            if (!versionList.Contains(version))
            {
                versionList.Add(version);
            }

            IEnumerable<NuGetVersion> nuGetVersions = versionList;
            string summary = package.Value<string>(Properties.Summary);
            if (string.IsNullOrWhiteSpace(summary))
            {
                // summary is empty. Use its description instead.
                summary = package.Value<string>(Properties.Description);
            }
            PowershellSearchMetadata searchResult = new PowershellSearchMetadata(id, version, nuGetVersions, summary);
            return searchResult;
        }

     
    }
}
