// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio
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

        public RestoreOperationSource RestoreSource { get; }

        public Guid OperationId => Guid.NewGuid();

        public string OriginalProject { get; }

        public SolutionRestoreRequest(
            bool forceRestore,
            RestoreOperationSource restoreSource)
            : this(forceRestore, restoreSource, null)
        {
        }

        internal SolutionRestoreRequest(
            bool forceRestore,
            RestoreOperationSource restoreSource,
            string originalProject)
        {
            ForceRestore = forceRestore;
            RestoreSource = restoreSource;
            OriginalProject = originalProject;
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
                forceRestore: false,
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
                restoreSource: RestoreOperationSource.Implicit);
        }

        public static SolutionRestoreRequest OnUpdate(string project)
        {
            return new SolutionRestoreRequest(
                forceRestore: false,
                restoreSource: RestoreOperationSource.Implicit,
                originalProject: project);
        }
    }
}
