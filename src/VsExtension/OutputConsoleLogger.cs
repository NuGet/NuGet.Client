// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGetConsole;

namespace NuGetVSExtension
{
    internal class OutputConsoleLogger : INuGetUILogger
    {
        // Copied from EnvDTE interop
        private const string vsWindowKindOutput = "{34E76E81-EE4A-11D0-AE2E-00A0C90FFFC3}";

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private BuildEvents _buildEvents;

        private SolutionEvents _solutionEvents;

        private const string LogEntrySource = "NuGet Package Manager";

        public IConsole OutputConsole { get; private set; }

        public ErrorListProvider ErrorListProvider { get; private set; }

        public OutputConsoleLogger(IServiceProvider serviceProvider)
        {
            ErrorListProvider = new ErrorListProvider(serviceProvider);
            var outputConsoleProvider = ServiceLocator.GetInstance<IOutputConsoleProvider>();

            var dte = ServiceLocator.GetInstance<DTE>();
            _buildEvents = dte.Events.BuildEvents;
            _buildEvents.OnBuildBegin += (obj, ev) => { ErrorListProvider.Tasks.Clear(); };
            _solutionEvents = dte.Events.SolutionEvents;
            _solutionEvents.AfterClosing += () => { ErrorListProvider.Tasks.Clear(); };

            OutputConsole = outputConsoleProvider.CreateOutputConsole(requirePowerShellHost: false);
        }

        public void End()
        {
            OutputConsole.WriteLine(Resources.Finished);

            if (ErrorListProvider.Tasks.Count > 0)
            {
                ErrorListProvider.BringToFront();
                ErrorListProvider.ForceShowErrors();
            }
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            if (level == MessageLevel.Info
                || level == MessageLevel.Error
                || level == MessageLevel.Warning)
            {
                var s = string.Format(CultureInfo.CurrentCulture, message, args);
                OutputConsole.WriteLine(s);
            }
        }

        private void ActivateOutputWindow()
        {
            var uiShell = ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>();
            if (uiShell == null)
            {
                return;
            }

            var guid = new Guid(vsWindowKindOutput);
            IVsWindowFrame f = null;
            uiShell.FindToolWindow(0, ref guid, out f);
            if (f == null)
            {
                return;
            }

            f.Show();
        }

        public void Start()
        {
            ActivateOutputWindow();
            ErrorListProvider.Tasks.Clear();
            OutputConsole.Clear();
        }

        public void ReportError(string message)
        {
            ErrorTask retargetErrorTask = new ErrorTask();
            retargetErrorTask.Text = message;
            retargetErrorTask.ErrorCategory = TaskErrorCategory.Error;
            retargetErrorTask.Category = TaskCategory.User;
            retargetErrorTask.Priority = TaskPriority.High;
            retargetErrorTask.HierarchyItem = null;
            ErrorListProvider.Tasks.Add(retargetErrorTask);
        }
    }
}
