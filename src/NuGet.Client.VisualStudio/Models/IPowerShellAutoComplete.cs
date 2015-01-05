using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    public interface IPowerShellAutoComplete
    {
       Task<IEnumerable<string>> GetPackageIdsStartingWith(string packageIdPrefix,System.Threading.CancellationToken cancellationToken);
       Task<IEnumerable<NuGetVersion>> GetAllVersions(string versionPrefix);
    }
}

