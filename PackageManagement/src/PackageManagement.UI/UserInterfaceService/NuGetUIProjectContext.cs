// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Windows.Threading;
using System.Xml.Linq;
using NuGet.Packaging;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUIProjectContext : INuGetProjectContext
    {
        public FileConflictAction FileConflictAction { get; set; }

        private readonly Dispatcher _uiDispatcher;
        private readonly INuGetUILogger _logger;

        public NuGetUIProjectContext(INuGetUILogger logger, ISourceControlManagerProvider sourceControlManagerProvider, ICommonOperations commonOperations)
        {
            _logger = logger;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            SourceControlManagerProvider = sourceControlManagerProvider;
            CommonOperations = commonOperations;
            if (commonOperations != null)
            {
                ExecutionContext = new IDEExecutionContext(commonOperations);
            }
        }

        public void Log(ProjectManagement.MessageLevel level, string message, params object[] args)
        {
            _logger.Log(level, message, args);
        }

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

        // called when user clicks the action button
        public void Start()
        {
            _logger.Start();
        }

        internal void End()
        {
            _logger.End();
        }

        public PackageExtractionContext PackageExtractionContext { get; set; }

        public ISourceControlManagerProvider SourceControlManagerProvider { get; }

        public ICommonOperations CommonOperations { get; }

        public ExecutionContext ExecutionContext { get; }

        public XDocument OriginalPackagesConfig { get; set; }

        public void ReportError(string message)
        {
            _logger.ReportError(message);
        }
    }
}
