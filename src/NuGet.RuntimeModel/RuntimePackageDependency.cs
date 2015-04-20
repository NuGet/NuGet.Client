using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace NuGet.RuntimeModel
{
    public class RuntimePackageDependency
    {
        public string Id { get; }
        public NuGetVersion Version { get; }

        public RuntimePackageDependency(string id, NuGetVersion version)
        {
            Id = id;
            Version = version;
        }
    }
}
