using System;
using System.Windows.Threading;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.UI
{
    public class NuGetUIProjectContext : INuGetProjectContext
    {
        public FileConflictAction FileConflictAction
        {
            get;
            set;
        }

        private readonly Dispatcher _uiDispatcher;
        private readonly INuGetUILogger _logger;
        private readonly ISourceControlManagerProvider _sourceControlManagerProvider;
        private readonly ICommonOperations _commonOperations;

        public NuGetUIProjectContext(INuGetUILogger logger, ISourceControlManagerProvider sourceControlManagerProvider, ICommonOperations commonOperations)
        {
            _logger = logger;
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            _sourceControlManagerProvider = sourceControlManagerProvider;
            _commonOperations = commonOperations;
            if (commonOperations != null)
            {
                ExecutionContext = new IDEExecutionContext(commonOperations);
            }
        }

        public void Log(MessageLevel level, string message, params object[] args)
        {
            _logger.Log(level, message, args);
        }

        public FileConflictAction ShowFileConflictResolution(string message)
        {
            if (!_uiDispatcher.CheckAccess())
            {
                object result = _uiDispatcher.Invoke(
                    new Func<string, FileConflictAction>(ShowFileConflictResolution),
                    message);
                return (FileConflictAction)result;
            }

            var fileConflictDialog = new FileConflictDialog()
            {
                Question = message
            };

            if (fileConflictDialog.ShowModal() == true)
            {
                return fileConflictDialog.UserSelection;
            }
            else
            {
                return FileConflictAction.IgnoreAll;
            }
        }

        public FileConflictAction ResolveFileConflict(string message)
        {
            if (FileConflictAction == FileConflictAction.PromptUser)
            {
                var resolution = ShowFileConflictResolution(message);

                if (resolution == FileConflictAction.IgnoreAll ||
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

        public Packaging.PackageExtractionContext PackageExtractionContext
        {
            get;
            set;
        }

        public ISourceControlManagerProvider SourceControlManagerProvider
        {
            get { return _sourceControlManagerProvider; }
        }

        public ICommonOperations CommonOperations
        {
            get
            {
                return _commonOperations;
            }
        }

        public ExecutionContext ExecutionContext
        {
            get;
            private set;
        }

        public void ReportError(string message)
        {
            _logger.ReportError(message);
        }
    }
}