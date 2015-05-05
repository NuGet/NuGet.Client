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
        Task<bool> ExecuteAsync(string packageInstallPath, string scriptRelativePath, ZipArchive packageZipArchive, EnvDTEProject envDTEProject, NuGetProject nuGetProject, INuGetProjectContext nuGetProjectContext, bool throwOnFailure);
    }
}
