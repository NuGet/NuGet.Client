using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace API.Test
{
    public static class VSSolutionHelper
    {
        public static async Task<Solution2> GetSolution2Async()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var dte2 = (DTE2)dte;
            var solution2 = dte2.Solution as Solution2;

            return solution2;
        }

        public static void WaitForSolutionLoad()
        {
            var mre = new ManualResetEvent(false);
            KnownUIContexts.SolutionExistsAndFullyLoadedContext.WhenActivated(() => mre.Set());
            mre.WaitOne();
        }

        public static string GetSolutionFullName()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var solutionFullName = await GetSolutionFullNameAsync();
                return solutionFullName;
            });
        }

        private static async Task<string> GetSolutionFullNameAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            var solutionFullName = solution2.FullName;
            return solutionFullName;
        }

        public static void CreateSolution(string solutionDirectory, string name)
        {
            Utils.ThrowStringArgException(solutionDirectory, nameof(solutionDirectory));
            Utils.ThrowStringArgException(name, nameof(name));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await CreateSolutionAsync(solutionDirectory, name);
            });
        }

        private static async Task CreateSolutionAsync(string solutionDirectory, string name)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            solution2.Create(solutionDirectory, name);

            var solutionPath = Path.Combine(solutionDirectory, name);
            solution2.SaveAs(solutionPath);
        }

        private static string SolutionNameFormat = "Solution_{0}";
        public static void CreateNewSolution(string outputPath, string solutionName)
        {
            Utils.ThrowStringArgException(outputPath, nameof(outputPath));
            Utils.ThrowStringArgException(solutionName, nameof(solutionName));

            var solutionDir = Path.Combine(outputPath, solutionName);

            Directory.CreateDirectory(solutionDir);

            CreateSolution(solutionDir, solutionName);
        }
        
        public static void CreateNewSolution(string outputPath)
        {
            Utils.ThrowStringArgException(outputPath, nameof(outputPath));

            var id = Utils.GetNewGUID();
            var solutionName = string.Format(SolutionNameFormat, id);

            CreateNewSolution(outputPath, solutionName);
        }

        public static void EnsureSolution(string outputPath)
        {
            Utils.ThrowStringArgException(outputPath, nameof(outputPath));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await EnsureSolutionAsync(outputPath);
            });
        }

        public static async Task EnsureSolutionAsync(string outputPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var isSolutionAvailable = await IsSolutionAvailableAsync();
            if (!isSolutionAvailable)
            {
                CreateNewSolution(outputPath);
            }
        }

        public static bool IsSolutionAvailable()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var isSolutionAvailable = await IsSolutionAvailableAsync();
                return isSolutionAvailable;
            });
        }

        public static async Task<bool> IsSolutionAvailableAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            return await IsSolutionAvailableAsync(solution2);
        }

        public static async Task<bool> IsSolutionAvailableAsync(Solution2 solution2)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return solution2 != null && solution2.IsOpen;
        }
        
        public static void CloseSolution()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await CloseSolutionAsync();
            });
        }

        private static async Task CloseSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            var isSolutionAvailable = await IsSolutionAvailableAsync(solution2);
            if (isSolutionAvailable)
            {
                solution2.Close();
            }
        }

        public static void OpenSolution(string solutionFile)
        {
            Utils.ThrowStringArgException(solutionFile, nameof(solutionFile));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await OpenSolutionAsync(solutionFile);
            });
        }

        private static async Task OpenSolutionAsync(string solutionFile)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            solution2.Open(solutionFile);
        }

        public static void SaveAsSolution(string solutionFile)
        {
            Utils.ThrowStringArgException(solutionFile, nameof(solutionFile));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await SaveAsSolutionAsync(solutionFile);
            });
        }

        private static async Task SaveAsSolutionAsync(string solutionFile)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();
            solution2.SaveAs(solutionFile);
        }

        public static async Task<SolutionBuild> GetSolutionBuildAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await VSSolutionHelper.GetSolution2Async();
            var isSolutionAvailable = await VSSolutionHelper.IsSolutionAvailableAsync(solution2);

            if (!isSolutionAvailable)
            {
                throw new ArgumentException("Solution is not available");
            }

            return solution2.SolutionBuild;
        }

        public static void BuildSolution()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await BuildSolutionAsync();
            });
        }

        private static async Task BuildSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionBuild = await GetSolutionBuildAsync();

            solutionBuild.Build(WaitForBuildToFinish: true);
        }

        public static void CleanSolution()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await CleanSolutionAsync();
            });
        }

        private static async Task CleanSolutionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionBuild = await GetSolutionBuildAsync();

            solutionBuild.Clean(WaitForCleanToFinish: true);
        }

        public static async Task<Project> GetSolutionFolderProjectAsync(Solution2 solution2, string solutionFolderName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solutionFolderParts = solutionFolderName.Split('\\');
            var solutionFolderProject = await GetSolutionFolderProjectAsync(solution2.Projects, solutionFolderParts, 0);

            if (solutionFolderProject == null)
            {
                throw new ArgumentException("No corresponding solution folder exists", nameof(solutionFolderName));
            }

            var solutionFolder = solutionFolderProject.Object as SolutionFolder;
            if (solutionFolder == null)
            {
                throw new ArgumentException("Not a valid solution folder", nameof(solutionFolderName));
            }

            return solutionFolderProject;
        }

        private static async Task<Project> GetSolutionFolderProjectAsync(
            IEnumerable projectItems,
            string[] solutionFolderParts,
            int level)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (solutionFolderParts == null)
            {
                throw new ArgumentNullException(nameof(solutionFolderParts));
            }

            if (solutionFolderParts.Length == 0)
            {
                throw new ArgumentException("solution folder parts cannot be null", nameof(solutionFolderParts));
            }

            if (projectItems == null || level >= solutionFolderParts.Length)
            {
                return null;
            }

            var solutionFolderName = solutionFolderParts[level];
            Project solutionFolderProject = null;

            foreach (var item in projectItems)
            {
                // Item could be a project or a projectItem
                Project project = item as Project;

                if (project == null)
                {
                    var projectItem = item as ProjectItem;
                    if (projectItem != null)
                    {
                        project = projectItem.SubProject;
                    }
                }

                if (project != null)
                {
                    if (project.UniqueName.StartsWith(solutionFolderName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (solutionFolderParts.Length == level + 1)
                        {
                            solutionFolderProject = project;
                            break;
                        }
                        else
                        {
                            solutionFolderProject
                                = await GetSolutionFolderProjectAsync(project.ProjectItems, solutionFolderParts, level + 1);
                        }
                    }
                }
            }

            return solutionFolderProject;
        }

        public static void NewSolutionFolder(string outputPath, string folderPath)
        {
            Utils.ThrowStringArgException(outputPath, nameof(outputPath));
            Utils.ThrowStringArgException(folderPath, nameof(folderPath));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NewSolutionFolderAsync(outputPath, folderPath);
            });
        }

        private static async Task NewSolutionFolderAsync(string outputPath, string folderPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();

            

            var newSolutionFolderIndex = folderPath.LastIndexOf('\\');

            if (newSolutionFolderIndex == -1)
            {
                // Create solution folder at solution level
                await EnsureSolutionAsync(outputPath);
                solution2.AddSolutionFolder(folderPath);
                return;
            }
            else
            {
                // Get solution folder project object for parent
                var parentName = folderPath.Substring(0, newSolutionFolderIndex);
                var solutionFolderName = folderPath.Substring(newSolutionFolderIndex + 1);

                var parentProject = await GetSolutionFolderProjectAsync(solution2, parentName);
                var parentSolutionFolder = (SolutionFolder)parentProject.Object;
                parentSolutionFolder.AddSolutionFolder(solutionFolderName);
            }
        }

        public static void RenameSolutionFolder(string folderPath, string newName)
        {
            Utils.ThrowStringArgException(folderPath, nameof(folderPath));
            Utils.ThrowStringArgException(newName, nameof(newName));

            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await RenameSolutionFolderAsync(folderPath, newName);
            });
        }

        private static async Task RenameSolutionFolderAsync(string folderPath, string newName)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var solution2 = await GetSolution2Async();

            var solutionFolderProject = await GetSolutionFolderProjectAsync(solution2, folderPath);
            solutionFolderProject.Name = newName;
        }
    }
}
