using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using NuGet.Configuration;
using NuGet.PackageManagement;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.VisualStudio
{
    internal static class PackageManagementHelpers
    {
        /// <summary>
        /// Finds the NuGetProject from a DTE project
        /// </summary>
        public static NuGetProject GetProject(ISolutionManager solutionManager, Project project, VSAPIProjectContext projectContext=null)
        {
            if (solutionManager == null)
            {
                throw new ArgumentNullException("solution");
            }

            var projectSafeName = EnvDTEProjectUtility.GetCustomUniqueName(project);
            NuGetProject nuGetProject = solutionManager.GetNuGetProject(projectSafeName);

            var settings = ServiceLocator.GetInstance<ISettings>();

            // if the project does not exist in the solution (this is true for new templates) create it manually
            if (nuGetProject == null)
            {
                VSNuGetProjectFactory factory = new VSNuGetProjectFactory(() => PackagesFolderPathUtility.GetPackagesFolderPath(solutionManager, settings));
                nuGetProject = factory.CreateNuGetProject(project, projectContext);
            }

            return nuGetProject;
        }

        public static string GetInstallPath(ISolutionManager solution, ISettings settings, Project project, PackageIdentity identity)
        {
            string installPath = string.Empty;

            NuGetProject nuGetProject = GetProject(solution, project);
            FolderNuGetProject folderProject = nuGetProject as FolderNuGetProject;

            if (folderProject != null)
            {
                installPath = folderProject.GetInstalledPath(identity);
            }
            else if (solution != null && settings != null)
            {
                string packagesFolder = PackagesFolderPathUtility.GetPackagesFolderPath(solution, settings);

                FolderNuGetProject solutionLevel = new FolderNuGetProject(packagesFolder);
                installPath = solutionLevel.GetInstalledPath(identity);
            }

            Debug.Fail("unable to get install path");

            return installPath;
        }

        public static IVsPackageMetadata CreateMetadata(string nupkgPath, PackageIdentity package)
        {
            IEnumerable<string> authors = Enumerable.Empty<string>();
            string description = string.Empty;
            string title = package.Id;
            string installPath = string.Empty;

            try
            {
                // installPath is the nupkg path
                FileInfo file = new FileInfo(nupkgPath);
                installPath = file.Directory.FullName;
                PackageReader reader = new PackageReader(file.OpenRead());

                using (var nuspecStream = reader.GetNuspec())
                {
                    NuspecReader nuspec = new NuspecReader(nuspecStream);

                    var metadata = nuspec.GetMetadata();

                    authors = GetNuspecValue(metadata, "authors").Split(',').ToArray();
                    title = GetNuspecValue(metadata, "title");
                    description = GetNuspecValue(metadata, "description");
                }
            }
            catch (Exception ex)
            {
                // ignore errors from reading the extra fields
                Debug.Fail(ex.ToString());
            }

            if (String.IsNullOrEmpty(title))
            {
                title = package.Id;
            }

            return new VsPackageMetadata(package, title, authors, description, installPath);
        }


        /// <summary>
        /// Run a package management task sync and unwrap the aggregate exceptions
        /// </summary>
        public static void RunSync(Func<Task> createTask)
        {
            TaskFactory taskFactory = new TaskFactory(CancellationToken.None,
                  TaskCreationOptions.None,
                  TaskContinuationOptions.None,
                  TaskScheduler.Default);

            var task = taskFactory.StartNew<Task>(createTask);

            task.Unwrap().GetAwaiter().GetResult();
        }

        private static string GetNuspecValue(IEnumerable<KeyValuePair<string, string>> metadata, string field)
        {
            var node = metadata.Where(e => StringComparer.Ordinal.Equals(field, e.Key)).FirstOrDefault();

            return node.Value ?? string.Empty;
        }
    }
}
