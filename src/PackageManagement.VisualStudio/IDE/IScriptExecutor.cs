using NuGet.ProjectManagement;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTEProject = EnvDTE.Project;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IScriptExecutor
    {
        bool Execute(ZipArchive zipArchive, string scriptArchiveEntryFullName, EnvDTEProject envDTEProject, INuGetProjectContext nuGetProjectContext);
    }
}
