using EnvDTE80;
using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface ITFSSourceControlManagerProvider
    {
        SourceControlManager GetTFSSourceControlManager(SourceControlBindings binding);
    }
}
