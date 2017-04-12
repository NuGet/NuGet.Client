using System;
using System.Collections.Generic;
using System.Linq;
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

            PrintPackageReferences(packagReferences, packageReferenceArgs.Logger, packageReferenceArgs.ProjectPath);

            return Task.FromResult(0);
        }

        public void PrintPackageReferences(Dictionary<string, IEnumerable<Tuple<string, string>>> packageReferences, ILogger logger, string projectPath)
        {
            if(packageReferences.Keys.Count == 0)
            {
                logger.LogInformation($"Project '{projectPath}' has no package references.");
            }
            else
            {
                logger.LogInformation($"Project '{projectPath}' has following package references.");

                foreach (var framework in packageReferences.Keys)
                {
                    logger.LogInformation($"Framework '{framework}' - ");
                    logger.LogInformation($"--------------------------------------------------");

                    if (packageReferences[framework] == null)
                    {
                        logger.LogInformation($"This poject does not target '{framework}'");
                    }
                    else if(!packageReferences[framework].Any())
                    {
                        logger.LogInformation($"This poject does not reference any package for '{framework}'");
                    }
                    else
                    {
                        foreach (var packageReference in packageReferences[framework])
                        {
                            logger.LogInformation($"{packageReference.Item1} {packageReference.Item2}");
                        }
                    }
                    logger.LogInformation($"--------------------------------------------------");
                }
            }
        }
    }
}
