using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.Common;

namespace NuGet.CommandLine.XPlat
{
    class ListPackageReferenceCommandRunner : IPackageReferenceCommandRunner
    {
        public Task<int> ExecuteCommand(PackageReferenceArgs packageReferenceArgs, MSBuildAPIUtility msBuild)
        {
            var packagReferences = msBuild.ListPackageReference(packageReferenceArgs.ProjectPath,
                    packageReferenceArgs.PackageDependency, packageReferenceArgs.Frameworks);

            PrintPackageReferences(packagReferences, packageReferenceArgs.Logger);

            return Task.FromResult(0);
        }

        public void PrintPackageReferences(IEnumerable<ProjectItem> packageReferences, ILogger logger)
        {
            foreach(var packageReference in packageReferences)
            {
                logger.LogInformation($"{packageReference.EvaluatedInclude}: {packageReference.GetMetadataValue("Version")}");
            }
        }
    }
}
