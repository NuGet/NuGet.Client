// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using NuGet.Common;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio.Common
{
    [Export(typeof(INuGetUILoggerFactory))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class OutputConsoleLoggerFactory : INuGetUILoggerFactory, IDisposable
    {
        private const string DTEProjectPage = "ProjectsAndSolution";
        private const string DTEEnvironmentCategory = "Environment";
        private const string MSBuildVerbosityKey = "MSBuildOutputVerbosity";

        private const int DefaultVerbosityLevel = 2;

        private int _verbosityLevel;

        private AsyncLazy<EnvDTE.DTE> _dte;
        private AsyncLazy<IOutputConsole> _outputConsole;
        private AsyncLazy<ErrorListTableDataSource> _errorListTableDataSource;

        // keeps a reference to BuildEvents so that our event handler
        // won't get disconnected because of GC.
        private EnvDTE.BuildEvents _buildEvents;
        private EnvDTE.SolutionEvents _solutionEvents;

        [ImportingConstructor]
        public OutputConsoleLoggerFactory(IOutputConsoleProvider consoleProvider, Lazy<ErrorListTableDataSource> errorListTableDataSource)
        {
            if (consoleProvider == null)
            {
                throw new ArgumentNullException(nameof(consoleProvider));
            }

            _dte = AsyncLazy.New(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await AsyncServiceProvider.GlobalProvider.GetDTEAsync();
            });

            _outputConsole = AsyncLazy.New(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return await consoleProvider.CreatePackageManagerConsoleAsync();
            });

            _errorListTableDataSource = AsyncLazy.New(async () =>
            {
                var errorListTableDataSourceValue = errorListTableDataSource.Value;

                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await _dte;
                _buildEvents = dte.Events.BuildEvents;
                _buildEvents.OnBuildBegin += (_, __) => { errorListTableDataSourceValue.ClearNuGetEntries(); };
                _solutionEvents = dte.Events.SolutionEvents;
                _solutionEvents.AfterClosing += () => { errorListTableDataSourceValue.ClearNuGetEntries(); };

                return errorListTableDataSourceValue;
            });
        }

        void IDisposable.Dispose()
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var errorListTableDataSource = await _errorListTableDataSource;
                errorListTableDataSource.Dispose();
                GC.SuppressFinalize(this);
            });
        }

        INuGetUILogger INuGetUILoggerFactory.Create()
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(() => CreateAsync());
        }

        private async Task<INuGetUILogger> CreateAsync()
        {
            var outputConsole = await _outputConsole;
            await outputConsole.ActivateAsync();
            await outputConsole.ClearAsync();
            _verbosityLevel = await GetMSBuildVerbosityLevelAsync();

            var errorListTableDataSource = await _errorListTableDataSource;
            errorListTableDataSource.ClearNuGetEntries();

            return new OutputConsoleLogger(outputConsole, errorListTableDataSource, _verbosityLevel);
       }

        private async Task<int> GetMSBuildVerbosityLevelAsync()
        {
            await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await _dte;
            var properties = dte.get_Properties(DTEEnvironmentCategory, DTEProjectPage);
            var value = properties.Item(MSBuildVerbosityKey).Value;
            if (value is int)
            {
                return (int)value;
            }

            return DefaultVerbosityLevel;
        }
    }
}
