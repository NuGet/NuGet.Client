using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using NuGet.PackageManagement;
using NuGet.ProjectManagement;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Management.Automation.Host;
using NuGet.PackageManagement.PowerShellCmdlets;
using System.Diagnostics;
using NuGet.NuGet.PackageManagement.PowerShellCmdlets;

namespace NuGet.PackageManagement.PowerShellCmdlets
{
    /// <summary>
    /// This command process the specified package against the specified project.
    /// </summary>
    public abstract class NuGetPowerShellBaseCommand : PSCmdlet, INuGetProjectContext, IErrorHandler
    {
        private readonly IHttpClientEvents _httpClientEvents;
        //internal const string PSCommandsUserAgentClient = "NuGet VS PowerShell Console";
        //private readonly Lazy<string> _psCommandsUserAgent = new Lazy<string>(
        //    () => HttpUtility.CreateUserAgentString(PSCommandsUserAgentClient, VsVersionHelper.FullVsEdition));
        private ProgressRecordCollection _progressRecordCache;
        private bool _overwriteAll, _ignoreAll;

        internal void Execute()
        {
            BeginProcessing();
            ProcessRecord();
            EndProcessing();
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "We want to display friendly message to the console.")]
        protected sealed override void ProcessRecord()
        {
            try
            {
                ProcessRecordCore();
            }
            catch (Exception ex)
            {
                // unhandled exceptions should be terminating
                ErrorHandler.HandleException(ex, terminating: true);
            }
            finally
            {
                UnsubscribeEvents();
            }
        }

        /// <summary>
        /// Derived classess must implement this method instead of ProcessRecord(), which is sealed by NuGetBaseCmdlet.
        /// </summary>
        protected abstract void ProcessRecordCore();

        protected IErrorHandler ErrorHandler
        {
            get
            {
                return this;
            }
        }

        internal bool IsSyncMode
        {
            get
            {
                if (Host == null || Host.PrivateData == null)
                {
                    return false;
                }

                PSObject privateData = Host.PrivateData;
                var syncModeProp = privateData.Properties["IsSyncMode"];
                return syncModeProp != null && (bool)syncModeProp.Value;
            }
        }

        protected override void BeginProcessing()
        {
            if (_httpClientEvents != null)
            {
                _httpClientEvents.SendingRequest += OnSendingRequest;
            }
        }

        protected override void StopProcessing()
        {
            UnsubscribeEvents();
            base.StopProcessing();
        }

        protected void UnsubscribeEvents()
        {
            if (_httpClientEvents != null)
            {
                _httpClientEvents.SendingRequest -= OnSendingRequest;
            }
        }

        protected virtual void OnSendingRequest(object sender, WebRequestEventArgs e)
        {
            //HttpUtility.SetUserAgent(e.Request, _psCommandsUserAgent.Value);
        }

        #region Logging
        void IErrorHandler.HandleError(ErrorRecord errorRecord, bool terminating)
        {
            if (terminating)
            {
                ThrowTerminatingError(errorRecord);
            }
            else
            {
                WriteError(errorRecord);
            }
        }

        void IErrorHandler.HandleException(Exception exception, bool terminating,
            string errorId, ErrorCategory category, object target)
        {
            exception = ExceptionUtility.Unwrap(exception);

            var error = new ErrorRecord(exception, errorId, category, target);

            ErrorHandler.HandleError(error, terminating: terminating);
        }

        protected void WriteLine(string message = null)
        {
            if (Host == null)
            {
                // Host is null when running unit tests. Simply return in this case
                return;
            }

            if (message == null)
            {
                Host.UI.WriteLine();
            }
            else
            {
                Host.UI.WriteLine(message);
            }
        }

        protected void WriteProgress(int activityId, string operation, int percentComplete)
        {
            if (IsSyncMode)
            {
                // don't bother to show progress if we are in synchronous mode
                return;
            }

            ProgressRecord progressRecord;

            // retrieve the ProgressRecord object for this particular activity id from the cache.
            if (ProgressRecordCache.Contains(activityId))
            {
                progressRecord = ProgressRecordCache[activityId];
            }
            else
            {
                progressRecord = new ProgressRecord(activityId, operation, operation);
                ProgressRecordCache.Add(progressRecord);
            }

            progressRecord.CurrentOperation = operation;
            progressRecord.PercentComplete = percentComplete;

            WriteProgress(progressRecord);
        }

        private void OnProgressAvailable(object sender, ProgressEventArgs e)
        {
            WriteProgress(ProgressActivityIds.DownloadPackageId, e.Operation, e.PercentComplete);
        }

        protected void SubscribeToProgressEvents()
        {
            if (!IsSyncMode && _httpClientEvents != null)
            {
                _httpClientEvents.ProgressAvailable += OnProgressAvailable;
            }
        }

        protected void UnsubscribeFromProgressEvents()
        {
            if (_httpClientEvents != null)
            {
                _httpClientEvents.ProgressAvailable -= OnProgressAvailable;
            }
        }

        protected virtual void LogCore(MessageLevel level, string formattedMessage)
        {
            switch (level)
            {
                case MessageLevel.Debug:
                    WriteVerbose(formattedMessage);
                    break;

                case MessageLevel.Warning:
                    WriteWarning(formattedMessage);
                    break;

                case MessageLevel.Info:
                    WriteLine(formattedMessage);
                    break;

                case MessageLevel.Error:
                    WriteError(formattedMessage);
                    break;
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes", Justification = "This exception is passed to PowerShell. We really don't care about the type of exception here.")]
        protected void WriteError(string message)
        {
            if (!String.IsNullOrEmpty(message))
            {
                WriteError(new Exception(message));
            }
        }

        protected void WriteError(Exception exception)
        {
            ErrorHandler.HandleException(exception, terminating: false);
        }

        private ProgressRecordCollection ProgressRecordCache
        {
            get
            {
                if (_progressRecordCache == null)
                {
                    _progressRecordCache = new ProgressRecordCollection();
                }

                return _progressRecordCache;
            }
        }

        void IErrorHandler.WriteProjectNotFoundError(string projectName, bool terminating)
        {
            var notFoundException =
                new ItemNotFoundException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        Resources.Cmdlet_ProjectNotFound, projectName));

            ErrorHandler.HandleError(
                new ErrorRecord(
                    notFoundException,
                    NuGetErrorId.ProjectNotFound, // This is your locale-agnostic error id.
                    ErrorCategory.ObjectNotFound,
                    projectName),
                    terminating: terminating);
        }

        void IErrorHandler.ThrowSolutionNotOpenTerminatingError()
        {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoSolution),
                terminating: true,
                errorId: NuGetErrorId.NoActiveSolution,
                category: ErrorCategory.InvalidOperation);
        }

        void IErrorHandler.ThrowNoCompatibleProjectsTerminatingError()
        {
            ErrorHandler.HandleException(
                new InvalidOperationException(Resources.Cmdlet_NoCompatibleProjects),
                terminating: true,
                errorId: NuGetErrorId.NoCompatibleProjects,
                category: ErrorCategory.InvalidOperation);
        }

        #endregion Logging

        void INuGetProjectContext.Log(MessageLevel level, string message, params object[] args)
        {
            string formattedMessage = String.Format(CultureInfo.CurrentCulture, message, args);
            LogCore(level, formattedMessage);
        }

        FileConflictAction INuGetProjectContext.ResolveFileConflict(string message)
        {
            if (_overwriteAll)
            {
                return FileConflictAction.OverwriteAll;
            }

            if (_ignoreAll)
            {
                return FileConflictAction.IgnoreAll;
            }

            var choices = new Collection<ChoiceDescription>
            {
                new ChoiceDescription(Resources.Cmdlet_Yes, Resources.Cmdlet_FileConflictYesHelp),
                new ChoiceDescription(Resources.Cmdlet_YesAll, Resources.Cmdlet_FileConflictYesAllHelp),
                new ChoiceDescription(Resources.Cmdlet_No, Resources.Cmdlet_FileConflictNoHelp),
                new ChoiceDescription(Resources.Cmdlet_NoAll, Resources.Cmdlet_FileConflictNoAllHelp)
            };

            int choice = Host.UI.PromptForChoice(Resources.FileConflictTitle, message, choices, defaultChoice: 2);

            Debug.Assert(choice >= 0 && choice < 4);
            switch (choice)
            {
                case 0:
                    return FileConflictAction.Overwrite;

                case 1:
                    _overwriteAll = true;
                    return FileConflictAction.OverwriteAll;

                case 2:
                    return FileConflictAction.Ignore;

                case 3:
                    _ignoreAll = true;
                    return FileConflictAction.IgnoreAll;
            }

            return FileConflictAction.Ignore;
        }
    }

    public class ProgressRecordCollection : KeyedCollection<int, ProgressRecord>
    {
        protected override int GetKeyForItem(ProgressRecord item)
        {
            return item.ActivityId;
        }
    }
}
