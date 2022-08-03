using System.Collections.Generic;
using System.Linq;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.Options
{
    public class MappingUIDisplay
    {
        public string ID { get; set; }
        public List<PackageSourceContextInfo> Sources { get; private set; }

        //View binds to this string
        public string SourcesString
        {
            get
            {
                return string.Join(", ", Sources.Select(s => s.Name));
            }
        }

        public MappingUIDisplay(string packageid, List<PackageSourceContextInfo> packageSources)
        {
            ID = packageid;
            Sources = packageSources;
        }
    }
}
