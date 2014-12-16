using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    public interface INuspecReader
    {
        string GetId();

        string GetVersion();

        string GetMinClientVersion();

        IEnumerable<KeyValuePair<string, string>> GetMetadata();

        IEnumerable<PackageDependencyGroup> GetDependencyGroups();

        IEnumerable<FrameworkSpecificGroup> GetReferenceGroups();

        IEnumerable<FrameworkSpecificGroup> GetFrameworkReferenceGroups();
    }
}
