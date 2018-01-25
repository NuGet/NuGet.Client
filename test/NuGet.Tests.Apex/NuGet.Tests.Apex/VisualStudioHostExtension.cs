using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio;
using NuGet.VisualStudio;

namespace NuGet.Tests.Apex
{
    public static class VisualStudioHostExtension
    {
        static private readonly Guid _nugetOutputWindowGuid = new Guid("CEC55EC8-CC51-40E7-9243-57B87A6F6BEB");

        /// <summary>
        /// Assert no errors in the error list or output window
        /// </summary>
        internal static void AssertNoErrors(this VisualStudioHost host)
        {
            host.AssertNuGetOutputDoesNotHaveErrors();
            host.GetErrorListErrors().Should().BeEmpty();
        }

        /// <summary>
        /// Assert no errors in the error list
        /// </summary>
        internal static List<string> GetErrorListErrors(this VisualStudioHost host)
        {
            var errors = new List<string>();

            Utils.UIInvoke(() =>
            {
                errors.AddRange(host.ObjectModel.Shell.ToolWindows.ErrorList.Messages.Select(e => e.Description));
            });

            return errors;
        }

        /// <summary>
        /// Assert no errors in nuget output window
        /// </summary>
        internal static void AssertNuGetOutputDoesNotHaveErrors(this VisualStudioHost host)
        {
            host.GetErrorsInOutputWindows().Should().BeEmpty();
        }

        public static bool HasNoErrorsInOutputWindows(this VisualStudioHost host)
        {
            return host.GetErrorsInOutputWindows().Count == 0;
        }

        public static List<string> GetErrorsInOutputWindows(this VisualStudioHost host)
        {
            return host.GetOutputWindowsLines().Where(e => e.IndexOf("failed", StringComparison.OrdinalIgnoreCase) > -1).ToList();
        }

        public static List<string> GetOutputWindowsLines(this VisualStudioHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            var lines = new List<string>();

            try
            {

                Utils.UIInvoke(() =>
                {
                    var outputPane = host.ObjectModel.Shell.ToolWindows.OutputWindow.GetOutputPane(_nugetOutputWindowGuid);
                    lines.AddRange(outputPane.Text.Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e)));
                });
            }
            catch (ArgumentException)
            {
                // If no output has been printed into the nuget output window then the output pane will not be initialized
                // If no output has been printed we know there is no error in the output window.
            }

            return lines;
        }

        public static void SelectProjectInSolutionExplorer(this VisualStudioHost host, string project)
        {
            Utils.UIInvoke(() =>
            {
                var item = host.ObjectModel.Shell.ToolWindows.SolutionExplorer.FindItemRecursive(project);
                item.Select();
            });
        }

        public static void ClearOutputWindow(this VisualStudioHost host)
        {
            try
            {
                Utils.UIInvoke(() =>
                {
                    var outputPane = host.ObjectModel.Shell.ToolWindows.OutputWindow.GetOutputPane(_nugetOutputWindowGuid);
                    outputPane.Clear();
                });
            }
            catch (ArgumentException)
            {
                // if outputPane doesn't exist, ignore it
            }
        }

        public static void ClearErrorWindow(this VisualStudioHost host)
        {
            try
            {
                Utils.UIInvoke(() => host.ObjectModel.Shell.ToolWindows.ErrorList.HideAllItems());
            }
            catch (ArgumentException)
            {
                // ignore errors
            }
        }

        /// <summary>
        /// Clear the error list and output window
        /// </summary>
        public static void ClearWindows(this VisualStudioHost host)
        {
            host.ClearOutputWindow();
            host.ClearErrorWindow();
        }
    }
}
