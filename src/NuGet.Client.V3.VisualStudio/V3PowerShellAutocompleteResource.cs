using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client.VisualStudio.Models;
using System.Threading;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace NuGet.Client.V3.VisualStudio
{
    public class V3PowerShellAutocompleteResource : V3Resource, IPowerShellAutoComplete
    {
        public V3PowerShellAutocompleteResource(V3Resource v3Resource)
            : base(v3Resource) { }
        public async Task<IEnumerable<string>> GetPackageIdsStartingWith(string packageIdPrefix,CancellationToken cancellationToken)
        {
            IEnumerable<string> listOfIds = await V3Client.SearchAutocomplete(packageIdPrefix,cancellationToken);
            return listOfIds;
        }

        
        public async Task<IEnumerable<Versioning.NuGetVersion>> GetAllVersions(string packageId)
        {
            //*TODOs : Take prerelease as parameter. Also it should return both listed and unlisted for powershell ? 
            IEnumerable<JObject> packages = await V3Client.GetPackageMetadataById(packageId);
            List<NuGetVersion> versions = new List<NuGetVersion>();
            foreach(var package in packages)
               versions.Add(new NuGetVersion((string)package["version"]));         
            return versions;
        }      
    }
}
