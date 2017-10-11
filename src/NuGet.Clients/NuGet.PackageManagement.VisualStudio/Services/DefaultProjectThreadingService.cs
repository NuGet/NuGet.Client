// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    [Export(typeof(IVsProjectThreadingService))]
    internal class DefaultProjectThreadingService : IVsProjectThreadingService
    {
        public JoinableTaskFactory JoinableTaskFactory => NuGetUIThreadHelper.JoinableTaskFactory;

        public void ExecuteSynchronously(Func<Task> asyncAction)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(asyncAction);
        }

        public T ExecuteSynchronously<T>(Func<Task<T>> asyncMethod)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(asyncMethod);
        }

        public void ThrowIfNotOnUIThread(string callerMemberName)
        {
            ThreadHelper.ThrowIfNotOnUIThread(callerMemberName);
        }
    }
}
