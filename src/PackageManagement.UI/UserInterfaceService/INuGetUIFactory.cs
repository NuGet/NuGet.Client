using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.UI
{
    public interface INuGetUIFactory
    {

        INuGetUI Create(IEnumerable<NuGetProject> projects);

    }
}
