using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using NuGet.PackageManagement.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace API.Test
{
    public static class VSHelper
    {
        public static string GetVSVersion()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var version = await GetVSVersionAsync();
                return version;
            });
        }

        private static async Task<string> GetVSVersionAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var version = dte.Version;

            return version;
        }

        public static string GetBuildOutput()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var text = await GetBuildOutputAsync();
                return text;
            });
        }

        private static string BuildOutputPaneName = "Build";
        private static async Task<string> GetBuildOutputAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            var dte2 = (DTE2)dte;
            var buildPane = dte2.ToolWindows.OutputWindow.OutputWindowPanes.Item(BuildOutputPaneName);
            var doc = buildPane.TextDocument;
            var sel = doc.Selection;
            sel.StartOfDocument(Extend: false);
            sel.EndOfDocument(Extend: true);
            var text = sel.Text;
            return text;
        }

        public static void NewTextFile()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NewTextFileAsync();
            });
        }

        private static async Task NewTextFileAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            dte.ItemOperations.NewFile("General\\Text File");
            dte.ActiveDocument.Object("TextDocument");
        }

        private static string ErrorListWindowCaption = "Error List";
        private static async Task<string[]> GetErrorTasksAsync(vsBuildErrorLevel errorLevel)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = ServiceLocator.GetInstance<DTE>();
            dte.ExecuteCommand("View.ErrorList", " ");

            Window errorListWindow = null;
            foreach (Window window in dte.Windows)
            {
                if (window.Caption.StartsWith(ErrorListWindowCaption, System.StringComparison.OrdinalIgnoreCase))
                {
                    errorListWindow = window;
                    break;
                }
            }

            if (errorListWindow == null)
            {
                throw new InvalidOperationException("Unable to locate the error list");
            }

            errorListWindow.Object.ShowErrors = true;
            errorListWindow.Object.ShowWarnings = true;
            errorListWindow.Object.ShowMessages = true;

            var errorItems = errorListWindow.Object.ErrorItems as ErrorItems;
            if (errorItems == null)
            {
                throw new InvalidOperationException("Unable to retrieve the error list");
            }

            var errorTasks = new List<ErrorItem>();

            for (int i = 1; i <= errorItems.Count; i++)
            {
                var errorItem = errorItems.Item(i);
                var currentErrorLevel = (vsBuildErrorLevel)errorItem.ErrorLevel;
                if (currentErrorLevel == errorLevel)
                {
                    errorTasks.Add(errorItem);
                }
            }

            var items = errorTasks.Select(e => e.Description as string).ToArray();
            return items;
        }

        public static string[] GetErrors()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var errors = await GetErrorsAsync();
                return errors;
            });
        }

        private static async Task<string[]> GetErrorsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var errorLevel = vsBuildErrorLevel.vsBuildErrorLevelHigh;
            var errors = await GetErrorTasksAsync(errorLevel);
            return errors;
        }

        public static string[] GetWarnings()
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                var warnings = await GetWarningsAsync();
                return warnings;
            });
        }

        private static async Task<string[]> GetWarningsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var errorLevel = vsBuildErrorLevel.vsBuildErrorLevelMedium;
            var warnings = await GetErrorTasksAsync(errorLevel);
            return warnings;
        }
    }
}
