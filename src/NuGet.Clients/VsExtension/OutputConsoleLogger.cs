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
        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private BuildEvents _buildEvents;

        private SolutionEvents _solutionEvents;

        private const string LogEntrySource = "NuGet Package Manager";

        private int _verbosityLevel;

        private readonly DTE _dte;

        private const string DTEProjectPage = "ProjectsAndSolution";

        private const string DTEEnvironmentCategory = "Environment";

        private const string MSBuildVerbosityKey = "MSBuildOutputVerbosity";

        private const int DefaultVerbosityLevel = 2;

        public IConsole OutputConsole { get; private set; }

        public ErrorListProvider ErrorListProvider { get; private set; }

        public OutputConsoleLogger(IServiceProvider serviceProvider)
        {
            ErrorListProvider = new ErrorListProvider(serviceProvider);
            var outputConsoleProvider = ServiceLocator.GetInstance<IOutputConsoleProvider>();

            _dte = ServiceLocator.GetInstance<DTE>();

            _buildEvents = _dte.Events.BuildEvents;
            _buildEvents.OnBuildBegin += (obj, ev) => { ErrorListProvider.Tasks.Clear(); };
            _solutionEvents = _dte.Events.SolutionEvents;
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
                || level == MessageLevel.Warning
                || _verbosityLevel > DefaultVerbosityLevel)
            {
                if (args.Length > 0)
                {
                    message = string.Format(CultureInfo.CurrentCulture, message, args);
                }
                
                OutputConsole.WriteLine(message);
            }
        }

        private void ActivateOutputWindow()
        {
            var uiShell = ServiceLocator.GetGlobalService<SVsUIShell, IVsUIShell>();
            if (uiShell != null)
            {
                IVsWindowFrame toolWindow = null;
                uiShell.FindToolWindow(0, ref GuidList.guidVsWindowKindOutput, out toolWindow);
                toolWindow?.Show();
            }
        }

        private int GetMSBuildVerbosityLevel()
        {
            var properties = _dte.get_Properties(DTEEnvironmentCategory, DTEProjectPage);
            var value = properties.Item(MSBuildVerbosityKey).Value;
            if (value is int)
            {
                return (int)value;
            }

            return DefaultVerbosityLevel;
        }

        public void Start()
        {
            ActivateOutputWindow();
            _verbosityLevel = GetMSBuildVerbosityLevel();
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
