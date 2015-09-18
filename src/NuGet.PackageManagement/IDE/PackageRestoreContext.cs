// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using NuGet.Protocol.Core.Types;

namespace NuGet.PackageManagement
{
    public class PackageRestoreContext
    {
        public NuGetPackageManager PackageManager { get; }
        public IEnumerable<PackageRestoreData> Packages { get; }
        public CancellationToken Token { get; }
        public EventHandler<PackageRestoredEventArgs> PackageRestoredEvent { get; }
        public EventHandler<PackageRestoreFailedEventArgs> PackageRestoreFailedEvent { get; }
        public IEnumerable<SourceRepository> SourceRepositories { get; }
        public int MaxNumberOfParallelTasks { get; }

        /// <summary>
        /// WasRestored is set to true in this class, if one or more packages were restored or that satellite files
        /// were copied
        /// Note that this property is not read-only unlike other properties
        /// </summary>
        public bool WasRestored { get; private set; }

        public PackageRestoreContext(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageRestoreData> packages,
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

            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            if (maxNumberOfParallelTasks <= 0)
            {
                throw new ArgumentException(Strings.ParameterCannotBeZeroOrNegative, nameof(maxNumberOfParallelTasks));
            }

            PackageManager = nuGetPackageManager;
            Packages = packages;
            Token = token;
            PackageRestoredEvent = packageRestoredEvent;
            PackageRestoreFailedEvent = packageRestoreFailedEvent;
            SourceRepositories = sourceRepositories;
            MaxNumberOfParallelTasks = maxNumberOfParallelTasks;
        }

        public PackageRestoreContext(NuGetPackageManager nuGetPackageManager,
            IEnumerable<PackageRestoreData> packages,
            CancellationToken token)
            : this(nuGetPackageManager,
                packages,
                token,
                packageRestoredEvent: null,
                packageRestoreFailedEvent: null,
                sourceRepositories: null,
                maxNumberOfParallelTasks: PackageManagementConstants.DefaultMaxDegreeOfParallelism)
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
