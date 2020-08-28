// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using NuGet.VisualStudio.Internal.Contracts;

namespace NuGet.PackageManagement.UI
{
    internal sealed class NuGetSolutionManagerServiceWrapper : INuGetSolutionManagerService
    {
        private INuGetSolutionManagerService _service = NullNuGetSolutionManagerService.Instance;
        private readonly object _syncObject = new object();

        public event EventHandler<string>? AfterNuGetCacheUpdated;
        public event EventHandler<IProjectContextInfo>? AfterProjectRenamed;
        public event EventHandler<IProjectContextInfo>? ProjectAdded;
        public event EventHandler<IProjectContextInfo>? ProjectRemoved;
        public event EventHandler<IProjectContextInfo>? ProjectRenamed;
        public event EventHandler<IProjectContextInfo>? ProjectUpdated;

        internal INuGetSolutionManagerService Service
        {
            get
            {
                lock (_syncObject)
                {
                    return _service;
                }
            }
            set
            {
                lock (_syncObject)
                {
                    UnregisterEventHandlers();

                    _service = value ?? NullNuGetSolutionManagerService.Instance;

                    RegisterEventHandlers();
                }
            }
        }

        public void Dispose()
        {
            using (_service)
            {
                Service = NullNuGetSolutionManagerService.Instance;
            }

            GC.SuppressFinalize(this);
        }

        public ValueTask<string> GetSolutionDirectoryAsync(CancellationToken cancellationToken)
        {
            return Service.GetSolutionDirectoryAsync(cancellationToken);
        }

        private void RegisterEventHandlers()
        {
            _service.AfterNuGetCacheUpdated += AfterNuGetCacheUpdated;
            _service.AfterProjectRenamed += AfterProjectRenamed;
            _service.ProjectAdded += ProjectAdded;
            _service.ProjectRemoved += ProjectRemoved;
            _service.ProjectRenamed += ProjectRenamed;
            _service.ProjectUpdated += ProjectUpdated;
        }

        private void UnregisterEventHandlers()
        {
            _service.AfterNuGetCacheUpdated -= AfterNuGetCacheUpdated;
            _service.AfterProjectRenamed -= AfterProjectRenamed;
            _service.ProjectAdded -= ProjectAdded;
            _service.ProjectRemoved -= ProjectRemoved;
            _service.ProjectRenamed -= ProjectRenamed;
            _service.ProjectUpdated -= ProjectUpdated;
        }

        private sealed class NullNuGetSolutionManagerService : INuGetSolutionManagerService
        {
            public event EventHandler<string> AfterNuGetCacheUpdated { add { } remove { } }
            public event EventHandler<IProjectContextInfo> AfterProjectRenamed { add { } remove { } }
            public event EventHandler<IProjectContextInfo> ProjectAdded { add { } remove { } }
            public event EventHandler<IProjectContextInfo> ProjectRemoved { add { } remove { } }
            public event EventHandler<IProjectContextInfo> ProjectRenamed { add { } remove { } }
            public event EventHandler<IProjectContextInfo> ProjectUpdated { add { } remove { } }

            internal static NullNuGetSolutionManagerService Instance { get; } = new NullNuGetSolutionManagerService();

            public void Dispose() { }

            public ValueTask<string> GetSolutionDirectoryAsync(CancellationToken cancellationToken) => new ValueTask<string>();
        }
    }
}
