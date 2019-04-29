using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    public class SearchArgs
    {
        public bool Prerelease { get; }

        public IList<Configuration.PackageSource> ListEndpoints { get; }

        public SearchArgs(
            bool prerelease,
            IList<Configuration.PackageSource> listEndpoints)
        {
            Prerelease = prerelease;
            ListEndpoints = listEndpoints;
        }
    }
}
