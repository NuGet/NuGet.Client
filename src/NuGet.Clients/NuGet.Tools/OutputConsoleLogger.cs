// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.SolutionRestoreManager;
using NuGet.VisualStudio;

namespace NuGetVSExtension
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
        private int _verbosityLevel;

        private EnvDTE.DTE _dte;

        public IOutputConsole OutputConsole { get; private set; }

        public Lazy<ErrorListTableDataSource> ErrorListTableDataSource { get; private set; }

        [ImportingConstructor]
        public OutputConsoleLogger(
            [Import(typeof(SVsServiceProvider))]
            IServiceProvider serviceProvider,
            IOutputConsoleProvider consoleProvider,
            Lazy<ErrorListTableDataSource> errorListDataSource)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException(nameof(serviceProvider));
            }

            if (consoleProvider == null)
            {
                throw new ArgumentNullException(nameof(consoleProvider));
            }

            ErrorListTableDataSource = errorListDataSource;

            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _dte = serviceProvider.GetDTE();

                OutputConsole = consoleProvider.CreatePackageManagerConsole();
            });
        }

        public void Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ErrorListTableDataSource.Value.Dispose();
            });
        }

        public void End()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                OutputConsole.WriteLine(Resources.Finished);
                OutputConsole.WriteLine(string.Empty);

                // Give the error list focus
                ErrorListTableDataSource.Value.BringToFront();
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
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                _verbosityLevel = GetMSBuildVerbosityLevel();

                OutputConsole.Activate();
                OutputConsole.Clear();

                ErrorListTableDataSource.Value.ClearNuGetEntries();
            });
        }

        public void ReportError(string message)
        {
            var errorListEntry = new ErrorListTableEntry(message, LogLevel.Error);
            RunTaskOnUI(() => ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry));
        }

        public void ReportError(ILogMessage message)
        {
            var errorListEntry = new ErrorListTableEntry(message);
            RunTaskOnUI(() => ErrorListTableDataSource.Value.AddNuGetEntries(errorListEntry));
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
                NuGetUIThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    action();
                });
            }
        }
    }
}
