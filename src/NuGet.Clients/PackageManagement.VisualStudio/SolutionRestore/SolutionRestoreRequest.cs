// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.PackageManagement.UI;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// Restore request flags and parameters.
    /// These parameters are used to alter UI, logging, and scheduling aspects of the operation.
    /// The restore functionsl behavior and result should be the same regardless actual values of
    /// the request.
    /// </summary>
    public sealed class SolutionRestoreRequest
    {
        /// <summary>
        /// Should the restore discard previous assets and clean the cache.
        /// </summary>
        public bool ForceRestore { get; }

        /// <summary>
        /// Should opt-out message be logged.
        /// </summary>
        public bool ShowOptOutMessage { get; }

        /// <summary>
        /// Should log the exception to the console and activity log
        /// </summary>
        public bool LogError { get; }

        /// <summary>
        /// Should the operation summary be logged even in case of no-op restore.
        /// </summary>
        public bool ForceStatusWrite { get; }

        public RestoreOperationSource RestoreSource { get; }

        public SolutionRestoreRequest(
            bool forceRestore, 
            bool showOptOutMessage,
            bool logError,
            bool forceStatusWrite,
            RestoreOperationSource restoreSource)
        {
            ForceRestore = forceRestore;
            ShowOptOutMessage = showOptOutMessage;
            LogError = logError;
            ForceStatusWrite = forceStatusWrite;
            RestoreSource = restoreSource;
        }

        /// <summary>
        /// Creates an instance of <see cref="SolutionRestoreRequest"/> with flags typical to 
        /// on-build restore.
        /// </summary>
        /// <param name="forceRestore">Force restore if re-build is requested.</param>
        /// <returns>New instance of <see cref="SolutionRestoreRequest"/></returns>
        public static SolutionRestoreRequest OnBuild(bool forceRestore)
        {
            return new SolutionRestoreRequest(
                forceRestore: forceRestore, 
                showOptOutMessage: true,
                logError: false,
                forceStatusWrite: false,
                restoreSource: RestoreOperationSource.OnBuild);
        }

        /// <summary>
        /// Creates an instance of <see cref="SolutionRestoreRequest"/> with flags typical to 
        /// on-demand restore as requested by an user via Visual Studio UI.
        /// </summary>
        /// <returns>New instance of <see cref="SolutionRestoreRequest"/></returns>
        public static SolutionRestoreRequest ByMenu()
        {
            return new SolutionRestoreRequest(
                forceRestore: true, 
                showOptOutMessage: false,
                logError: true,
                forceStatusWrite: true,
                restoreSource: RestoreOperationSource.Explicit);
        }

        /// <summary>
        /// Creates an instance of <see cref="SolutionRestoreRequest"/> with flags typical to 
        /// background restore.
        /// </summary>
        /// <returns>New instance of <see cref="SolutionRestoreRequest"/></returns>
        public static SolutionRestoreRequest OnUpdate()
        {
            return new SolutionRestoreRequest(
                forceRestore: false, 
                showOptOutMessage: false,
                logError: false,
                forceStatusWrite: false,
                restoreSource: RestoreOperationSource.Implicit);
        }
    }
}
