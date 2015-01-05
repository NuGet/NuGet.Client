using NuGet.Client.VisualStudio.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.V3.VisualStudio
{
    [Export(typeof(ResourceProvider))]
    [ResourceProviderMetadata("V3PowerShellAutocompleteResourceProvider", typeof(IPowerShellAutoComplete))]
    public class V3PowerShellAutocompleteResourceProvider : V3ResourceProvider
    {
        public async override Task<Resource> Create(PackageSource source)
        {
            Resource resource = await base.Create(source);
            if (resource != null)
            {
                var v3PowerShellAutocompleteResource = new V3PowerShellAutocompleteResource((V3Resource)resource);
                resource = v3PowerShellAutocompleteResource;
                return resource;
            }
            else
            {
                return null;
            }
        }
    }
}
