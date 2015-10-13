using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NuGet.Commands;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class BuildIntegratedProjectAction : NuGetProjectAction
    {
        /// <summary>
        /// Before the update
        /// </summary>
        public LockFile OriginalLockFile { get; }

        /// <summary>
        /// After applying the changes
        /// </summary>
        public RestoreResult RestoreResult { get; }

        /// <summary>
        /// project.json after applying the changes
        /// </summary>
        public JObject UpdatedProjectJson { get; }

        /// <summary>
        /// Sources used for package restore.
        /// </summary>
        public IReadOnlyList<SourceRepository> Sources { get; }

        public BuildIntegratedProjectAction(PackageIdentity packageIdentity,
            NuGetProjectActionType nuGetProjectActionType,
            LockFile originalLockFile,
            JObject updatedProjectJson,
            RestoreResult restoreResult,
            IReadOnlyList<SourceRepository> sources)
            : base(packageIdentity, nuGetProjectActionType)
        {
            if (packageIdentity == null)
            {
                throw new ArgumentNullException(nameof(packageIdentity));
            }

            if (originalLockFile == null)
            {
                throw new ArgumentNullException(nameof(originalLockFile));
            }

            if (updatedProjectJson == null)
            {
                throw new ArgumentNullException(nameof(updatedProjectJson));
            }

            if (restoreResult == null)
            {
                throw new ArgumentNullException(nameof(restoreResult));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            OriginalLockFile = originalLockFile;
            RestoreResult = restoreResult;
            UpdatedProjectJson = updatedProjectJson;
            Sources = sources;
        }

        public IReadOnlyList<NuGetProjectAction> GetProjectActions()
        {
            var actions = new List<NuGetProjectAction>();

            if (RestoreResult.Success)
            {
                var added = BuildIntegratedRestoreUtility.GetAddedPackages(OriginalLockFile, RestoreResult.LockFile);
                var removed = BuildIntegratedRestoreUtility.GetAddedPackages(RestoreResult.LockFile, OriginalLockFile);

                foreach (var package in added)
                {
                    actions.Add(NuGetProjectAction.CreateInstallProjectAction(package, sourceRepository: null));
                }

                foreach (var package in removed)
                {
                    actions.Add(NuGetProjectAction.CreateUninstallProjectAction(package));
                }
            }

            return actions;
        }
    }
}
