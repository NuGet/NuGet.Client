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

            PrintPackageReferences(packagReferences, packageReferenceArgs);

            return Task.FromResult(0);
        }

        public void PrintPackageReferences(Dictionary<string, IEnumerable<Tuple<string, string>>> packageReferences, PackageReferenceArgs packageReferenceArgs)
        {
            var logger = packageReferenceArgs.Logger;
            var projectPath = packageReferenceArgs.ProjectPath;
            var packageDependency = packageReferenceArgs.PackageDependency;
            if(packageReferences.Keys.Count == 0)
            {
                logger.LogInformation(string.Format(Strings.ListPkg_NoPackageRefsForProject, projectPath));
            }
            else
            {
                logger.LogInformation(string.Format(Strings.ListPkg_References, projectPath));

                foreach (var framework in packageReferences.Keys)
                {
                    logger.LogInformation(string.Format(Strings.ListPkg_Framework, framework));
                    logger.LogInformation("--------------------------------------------------");

                    if (packageReferences[framework] == null)
                    {
                        logger.LogInformation(string.Format(Strings.ListPkg_NonTargetedFramework, framework));
                    }
                    else if(!packageReferences[framework].Any())
                    {
                        if(packageDependency == null)
                        {
                            logger.LogInformation(string.Format(Strings.ListPkg_NoPackageRefsForFramework, framework));
                        }
                        else
                        {
                            logger.LogInformation(string.Format(Strings.ListPkg_PackageNotReferencedForFramework, 
                                packageDependency.Id, 
                                framework));
                        }
                    }
                    else
                    {
                        foreach (var packageReference in packageReferences[framework])
                        {
                            logger.LogInformation(string.Format(Strings.ListPkg_PackageAndVersion, packageReference.Item1, packageReference.Item2));
                        }
                    }
                    logger.LogInformation($"--------------------------------------------------");
                }
            }
        }
    }
}
