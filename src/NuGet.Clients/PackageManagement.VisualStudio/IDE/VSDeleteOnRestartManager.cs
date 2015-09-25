// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Shell;
using NuGet.Configuration;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;

namespace NuGet.PackageManagement.VisualStudio
{
    /// <summary>
    /// An <see cref="DeleteOnRestartManager"/> manger which is used for surfacing errors and UI in VS.
    /// </summary>
    [Export(typeof(IDeleteOnRestartManager))]
    public class VsDeleteOnRestartManager : IDeleteOnRestartManager
    {
        // The file extension to add to the empty files which will be placed adjacent to partially uninstalled package
        // directories marking them for removal the next time the solution is opened.
        private const string DeletionMarkerSuffix = ".deleteme";
        private const string DeletionMarkerFilter = "*" + DeletionMarkerSuffix;

        private string _packagesFolderPath = null;

        public event EventHandler<PackagesMarkedForDeletionEventArgs> PackagesMarkedForDeletionFound;

        /// <summary>
        /// Creates a new instance of <see cref="VsDeleteOnRestartManager"/>.
        /// </summary>
        public VsDeleteOnRestartManager() : this(
            ServiceLocator.GetInstance<Configuration.ISettings>(),
            ServiceLocator.GetInstance<ISolutionManager>())
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="DeleteOnRestartManager"/>.
        /// </summary>
        /// <param name="settings">The <see cref="ISettings"/> associated with the current solution.</param>
        /// <param name="solutionManager">The <see cref="ISolutionManager"/> associated with the current solution.
        /// </param>
        public VsDeleteOnRestartManager(
            Configuration.ISettings settings,
            ISolutionManager solutionManager)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (solutionManager == null)
            {
                throw new ArgumentNullException(nameof(solutionManager));
            }

            Settings = settings;
            SolutionManager = solutionManager;
            SolutionManager.SolutionOpened += OnSolutionOpenedOrClosed;
            SolutionManager.SolutionClosed += OnSolutionOpenedOrClosed;
        }

        public ISolutionManager SolutionManager { get; }

        public Configuration.ISettings Settings { get; }

        public string PackagesFolderPath
        {
            get
            {
                if (SolutionManager.SolutionDirectory != null)
                {
                    _packagesFolderPath =
                        PackagesFolderPathUtility.GetPackagesFolderPath(SolutionManager, Settings);
                }

                return _packagesFolderPath;
            }

            set
            {
                _packagesFolderPath = value;
            }
        }

        /// <summary>
        /// Gets the directories marked for deletion.
        /// </summary>
        public IReadOnlyList<string> GetPackageDirectoriesMarkedForDeletion()
        {
            if (PackagesFolderPath == null)
            {
                return new List<string>();
            }

            var candidates = FileSystemUtility
                .GetFiles(PackagesFolderPath, path: "", filter: DeletionMarkerFilter, recursive: false)
                // strip the DeletionMarkerFilter at the end of the path to get the package path.
                .Select(path => Path.Combine(PackagesFolderPath, Path.ChangeExtension(path, null)))
                .ToList();

            var filesWithoutFolders = candidates.Where(path => !Directory.Exists(path));
            foreach (var directory in filesWithoutFolders)
            {
                File.Delete(directory + DeletionMarkerSuffix);
            }

            return candidates.Where(path => Directory.Exists(path)).ToList();
        }

        /// <summary>
        /// Checks for any pacakge directories that are pending to be deleted and raises the
        /// <see cref="PackagesMarkedForDeletionFound"/> event.
        /// </summary>
        public virtual void CheckAndRaisePackageDirectoriesMarkedForDeletion()
        {
            var packages = GetPackageDirectoriesMarkedForDeletion();
            if (packages.Any() && PackagesMarkedForDeletionFound != null)
            {
                var eventArgs = new PackagesMarkedForDeletionEventArgs(packages);
                PackagesMarkedForDeletionFound(this, eventArgs);
            }
        }

        /// <summary>
        /// Marks package directory for future removal if it was not fully deleted during the normal uninstall process
        /// if the directory does not contain any added or modified files.
        /// The package directory will be marked by an adjacent *directory name*.deleteme file.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We want to log an exception as a warning and move on")]
        public void MarkPackageDirectoryForDeletion(
            PackageIdentity package,
            string packageDirectory,
            INuGetProjectContext projectContext)
        {
            if (PackagesFolderPath == null)
            {
                return;
            }

            try
            {
                using (FileSystemUtility.CreateFile(packageDirectory + DeletionMarkerSuffix, projectContext))
                {
                }
            }
            catch (Exception e)
            {
                projectContext.Log(
                    ProjectManagement.MessageLevel.Warning,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.Warning_FailedToMarkPackageDirectoryForDeletion,
                        packageDirectory,
                        e.Message));
            }
        }

        /// <summary>
        /// Attempts to remove package directories that were unable to be fully deleted during the original uninstall.
        /// These directories will be marked by an adjacent *directory name*.deleteme files in the local package
        /// repository.
        /// If the directory removal is successful, the .deleteme file will also be removed.
        /// </summary>
        [SuppressMessage(
            "Microsoft.Design",
            "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "We want to log an exception as a warning and move on")]
        public void DeleteMarkedPackageDirectories(INuGetProjectContext projectContext)
        {
            if (PackagesFolderPath == null)
            {
                return;
            }

            try
            {
                var packages = GetPackageDirectoriesMarkedForDeletion();
                foreach (var package in packages)
                {
                    try
                    {
                        FileSystemUtility.DeleteDirectorySafe(package, true, projectContext);
                    }
                    finally
                    {
                        if (!Directory.Exists(package))
                        {
                            var deleteMeFilePath = package.TrimEnd('\\') + DeletionMarkerSuffix;
                            FileSystemUtility.DeleteFile(deleteMeFilePath, projectContext);
                        }
                        else
                        {
                            projectContext.Log(
                                ProjectManagement.MessageLevel.Warning,
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Strings.Warning_FailedToDeleteMarkedPackageDirectory,
                                    package));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                projectContext.Log(
                               ProjectManagement.MessageLevel.Warning,
                               string.Format(
                                   CultureInfo.CurrentCulture,
                                   Strings.Warning_FailedToDeleteMarkedPackageDirectories,
                                   e.Message));
            }
        }

        private void OnSolutionOpenedOrClosed(object sender, EventArgs e)
        {
            // This is a solution event. Should be on the UI thread
            ThreadHelper.ThrowIfNotOnUIThread();

            // We need to do the check even on Solution Closed because, let's say if the yellow Update bar
            // is showing and the user closes the solution; in that case, we want to hide the Update bar.
            DeleteMarkedPackageDirectories(SolutionManager.NuGetProjectContext);
        }
    }
}
