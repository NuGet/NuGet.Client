// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using NuGet.PackageManagement.UI;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;

namespace NuGetConsole
{
    [Export(typeof(INuGetUILogger))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class OutputConsoleLogger : INuGetUILogger, IDisposable
    {
        private const string LogEntrySource = "NuGet Package Manager";
        private const string DTEProjectPage = "ProjectsAndSolution";
        private const string DTEEnvironmentCategory = "Environment";
        private const string MSBuildVerbosityKey = "MSBuildOutputVerbosity";
        private const int DefaultVerbosityLevel = 2;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private EnvDTE.BuildEvents _buildEvents;
        private EnvDTE.SolutionEvents _solutionEvents;

        private int _verbosityLevel;

        private EnvDTE.DTE _dte;

        public IOutputConsole OutputConsole { get; private set; }

        public ErrorListProvider ErrorListProvider { get; private set; }

        [ImportingConstructor]
        public OutputConsoleLogger(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IOutputConsoleProvider consoleProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (consoleProvider == null)
            {
                throw new ArgumentNullException(nameof(consoleProvider));
            }

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ErrorListProvider = new ErrorListProvider(serviceProvider);

                _dte = serviceProvider.GetDTE();

                _buildEvents = _dte.Events.BuildEvents;
                _buildEvents.OnBuildBegin += (_, __) => { ErrorListProvider.Tasks.Clear(); };

                _solutionEvents = _dte.Events.SolutionEvents;
                _solutionEvents.AfterClosing += () => { ErrorListProvider.Tasks.Clear(); };

                OutputConsole = consoleProvider.CreatePackageManagerConsole();
            });
        }

        public void Dispose()
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ErrorListProvider.Dispose();
            });
        }

        public void End()
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OutputConsole.WriteLine(Resources.Finished);

                if (ErrorListProvider.Tasks.Count > 0)
                {
                    ErrorListProvider.BringToFront();
                    ErrorListProvider.ForceShowErrors();
                }
            });
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

                RunTaskOnUI(() => OutputConsole.WriteLine(message));
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
            ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ActivateOutputWindow();
                _verbosityLevel = GetMSBuildVerbosityLevel();
                ErrorListProvider.Tasks.Clear();
                OutputConsole.Clear();
            });
        }

        public void ReportError(string message)
        {
            var errorTask = new ErrorTask
            {
                Text = message,
                ErrorCategory = TaskErrorCategory.Error,
                Category = TaskCategory.User,
                Priority = TaskPriority.High,
                HierarchyItem = null
            };
            RunTaskOnUI(() => ErrorListProvider.Tasks.Add(errorTask));
        }

        private static void RunTaskOnUI(Action action)
        {
            // Optimization for when this is already on the UI thread since
            // RunAsync cannot be used.
            if (ThreadHelper.CheckAccess())
            {
                // Run directly
                action();
            }
            else
            {
                // Run in JTF
                ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    action();
                });
            }
        }
    }
}
