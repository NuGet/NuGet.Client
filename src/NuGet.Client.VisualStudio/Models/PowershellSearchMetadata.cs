using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Client.VisualStudio.Models
{
    /// <summary>
    /// Model for search results shown by PowerShell console search.
    /// *TODOS: Should we extract out ID,version and summary to a base search model ? 
    /// </summary>
    public sealed class PowershellSearchMetadata
    {      
        public PowershellSearchMetadata(string id,NuGetVersion version,string summary)
        {
            Id = id;
            Version = version;
            Summary = summary;
        }
        public string Id { get; private set; }
        public NuGetVersion Version { get; private set; }
        public string Summary { get; private set; }
       
    }
}
