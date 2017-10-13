// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Threading;
using System.Xml.Linq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public sealed class NuGetUIProjectContext : INuGetProjectContext
    {
        private readonly Dispatcher _uiDispatcher;
        private readonly INuGetUILogger _logger;

        public FileConflictAction FileConflictAction { get; set; }

        public NuGetUIProjectContext(
            ICommonOperations commonOperations,
            INuGetUILogger logger,
            ISourceControlManagerProvider sourceControlManagerProvider)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (sourceControlManagerProvider == null)
            {
                throw new ArgumentNullException(nameof(sourceControlManagerProvider));
            }

            _logger = logger;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            SourceControlManagerProvider = sourceControlManagerProvider;

            if (commonOperations != null)
            {
                ExecutionContext = new IDEExecutionContext(commonOperations);
            }
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            _logger.Log(level, message, args);
        }


        [SuppressMessage("Microsoft.VisualStudio.Threading.Analyzers", "VSTHRD001", Justification = "NuGet/Home#4833 Baseline")]
        public FileConflictAction ShowFileConflictResolution(string message)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                var result = _uiDispatcher.Invoke(
                    new Func<string, FileConflictAction>(ShowFileConflictResolution),
                    message);
                return (FileConflictAction)result;
            }

            var fileConflictDialog = new FileConflictDialog
                {
                    Question = message
                };

            if (fileConflictDialog.ShowModal() == true)
            {
                return fileConflictDialog.UserSelection;
            }
            return FileConflictAction.IgnoreAll;
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            if (FileConflictAction == FileConflictAction.PromptUser)
            {
                var resolution = ShowFileConflictResolution(message);

                if (resolution == FileConflictAction.IgnoreAll
                    ||
                    resolution == FileConflictAction.OverwriteAll)
                {
                    FileConflictAction = resolution;
                }
                return resolution;
            }

            return FileConflictAction;
        }

        public PackageExtractionV2Context PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
            _logger.ReportError(message);
        }

        public NuGetActionType ActionType { get; set; }

        public TelemetryServiceHelper TelemetryService { get; set; }
    }
}
