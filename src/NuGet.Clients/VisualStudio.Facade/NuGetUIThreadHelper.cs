// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace NuGet.PackageManagement.UI
{
    public static class NuGetUIThreadHelper
    {
        private static JoinableTaskFactory _joinableTaskFactory;

        /// <summary>
        /// Returns the static instance of JoinableTaskFactory set by SetJoinableTaskFactoryFromService.
        /// </summary>
        public static JoinableTaskFactory JoinableTaskFactory
        {
            get
            {
                return _joinableTaskFactory;
            }
        }

        /// <summary>
        /// Retrieve the CPS enabled JoinableTaskFactory for the current version of Visual Studio.
        /// This overrides the default VsTaskLibraryHelper.ServiceInstance JTF.
        /// </summary>
        public static void SetJoinableTaskFactoryFromService(IServiceProvider serviceProvider)
        {
            var componentModel = serviceProvider.GetService(typeof(SComponentModel)) as IComponentModel;
            var projectServiceAccessor = componentModel.GetService<IProjectServiceAccessor>();

#if VS14
            // Use IThreadHandling.AsyncPump for Visual Studio 2015
            ProjectService projectService = projectServiceAccessor.GetProjectService();
            IThreadHandling threadHandling = projectService.Services.ThreadingPolicy;
            _joinableTaskFactory = service.AsyncPump;
#else
            // Use IProjectService for Visual Studio 2017
            IProjectService projectService = projectServiceAccessor.GetProjectService();
            _joinableTaskFactory = projectService.Services.ThreadingPolicy.JoinableTaskFactory;
#endif
        }

        /// <summary>
        /// Set a non-Visual Studio JTF. This is used for standalone mode.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public static void SetCustomJoinableTaskFactory(Thread mainThread, SynchronizationContext synchronizationContext)
        {
            if (mainThread == null)
            {
                throw new ArgumentNullException(nameof(mainThread));
            }

            if (synchronizationContext == null)
            {
                throw new ArgumentNullException(nameof(synchronizationContext));
            }

            // This method is not thread-safe and does not have it to be
            // This is really just a test-hook to be used by test standalone UI and only 1 thread will call into this
            // And, note that this method throws, when running inside VS, and ThreadHelper.JoinableTaskContext is not null
            var joinableTaskContext = new JoinableTaskContext(mainThread, synchronizationContext);
            _joinableTaskFactory = joinableTaskContext.Factory;
        }
    }
}
