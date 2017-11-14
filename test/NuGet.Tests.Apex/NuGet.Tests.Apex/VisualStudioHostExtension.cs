using System;
using Microsoft.Test.Apex.VisualStudio;

namespace NuGet.Tests.Apex
{
    public static class VisualStudioHostExtension
    {
        static private readonly Guid _nugetOutputWindowGuid = new Guid("CEC55EC8-CC51-40E7-9243-57B87A6F6BEB");

        public static bool HasNoErrorsInErrorList(this VisualStudioHost host)
        {
            return host.ObjectModel.Shell.ToolWindows.ErrorList.Verify.HasNoErrors();
        }

        public static bool HasNoErrorsInOutputWindows(this VisualStudioHost host)
        {
            try
            {
                var outputPane = host.ObjectModel.Shell.ToolWindows.OutputWindow.GetOutputPane(_nugetOutputWindowGuid);

                return !outputPane.Text.ToLowerInvariant().Contains("failed");
            }
            catch (ArgumentException)
            {
                // If no output has been printed into the nuget output window then the output pane will not be initialized
                // If no output has been printed we know there is no error in the output window.
                return true;
            }
        }

        public static void SelectProjectInSolutionExplorer(this VisualStudioHost host, string project)
        {
            var item = host.ObjectModel.Shell.ToolWindows.SolutionExplorer.FindItemRecursive(project);

            item.Select();
        }

        public static void ClearOutputWindow(this VisualStudioHost host)
        {
            try
            {
                var outputPane = host.ObjectModel.Shell.ToolWindows.OutputWindow.GetOutputPane(_nugetOutputWindowGuid);
                outputPane.Clear();
            }
            catch (ArgumentException)
            {
                //if outputPane doesn't exist, ignore it
            }
        }
    }
}
