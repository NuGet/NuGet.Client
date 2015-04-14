using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class PackageRestoreContext
    {
        public const int DefaultMaxNumberOfParellelTasks = 4;

        public NuGetPackageManager PackageManager { get; }
        public MissingPackagesInfo MissingPackagesInfo { get; }
        public CancellationToken Token { get; }
        public EventHandler<PackageRestoredEventArgs> PackageRestoredEvent { get; }
        public EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent { get; }
        public IEnumerable<SourceRepository> SourceRepositories { get; }
        public int MaxNumberOfParallelTasks { get; }
        /// <summary>
        /// WasRestored is set to true in this class, if one or more packages were restored or that satellite files were copied
        /// Note that this property is not read-only unlike other properties
        /// </summary>
        public bool WasRestored { get; private set; }

        public PackageRestoreContext(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            CancellationToken token,
            EventHandler<PackageRestoredEventArgs> packageRestoredEvent,
            EventHandler<PackageRestoreFailedEventArgs> packageRestoreFailedEvent,
            IEnumerable<SourceRepository> sourceRepositories,
            int maxNumberOfParallelTasks)
        {
            if (nuGetPackageManager == null)
            {
                throw new ArgumentNullException(nameof(nuGetPackageManager));
            }

            if (missingPackagesInfo == null)
            {
                throw new ArgumentNullException(nameof(missingPackagesInfo));
            }

            if (maxNumberOfParallelTasks <= 0)
            {
                throw new ArgumentException(Strings.ParameterCannotBeZeroOrNegative, nameof(maxNumberOfParallelTasks));
            }

            PackageManager = nuGetPackageManager;
            MissingPackagesInfo = missingPackagesInfo;
            Token = token;
            PackageRestoredEvent = packageRestoredEvent;
            PackageRestoreFailedEvent = packageRestoreFailedEvent;
            SourceRepositories = sourceRepositories;
            MaxNumberOfParallelTasks = maxNumberOfParallelTasks;
        }

        public PackageRestoreContext(NuGetPackageManager nuGetPackageManager,
            MissingPackagesInfo missingPackagesInfo,
            CancellationToken token)
            : this(nuGetPackageManager,
                  missingPackagesInfo,
                  token,
                  packageRestoredEvent: null,
                  packageRestoreFailedEvent: null,
                  sourceRepositories: null,
                  maxNumberOfParallelTasks: DefaultMaxNumberOfParellelTasks)
        {

        }

        /// <summary>
        /// Sets that one or more packages were restored or that satellite files were copied
        /// If this has been called at least once, WasRestored returns true. Otherwise, it returns false
        /// </summary>
        public void SetRestored()
        {
            WasRestored = true;
        }
    }
}
