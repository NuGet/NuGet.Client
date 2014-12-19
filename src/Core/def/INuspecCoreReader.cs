using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackagingCore
{
    public interface INuspecCoreReader
    {
        string GetId();

        NuGetVersion GetVersion();

        SemanticVersion GetMinClientVersion();

        IEnumerable<KeyValuePair<string, string>> GetMetadata();
    }
}
