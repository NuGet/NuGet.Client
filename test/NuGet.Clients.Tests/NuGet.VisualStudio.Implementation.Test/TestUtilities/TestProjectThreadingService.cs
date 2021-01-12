// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.VisualStudio;

namespace NuGet.VisualStudio.Implementation.Test.TestUtilities
{
    internal class TestProjectThreadingService : IVsProjectThreadingService
    {
        private readonly JoinableTaskContext _context;

        public TestProjectThreadingService()
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            _context = new JoinableTaskContext();
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
        }

        public JoinableTaskFactory JoinableTaskFactory => _context.Factory;

        public void ExecuteSynchronously(Func<Task> asyncAction)
        {
            JoinableTaskFactory.Run(asyncAction);
        }

        public T ExecuteSynchronously<T>(Func<Task<T>> asyncAction)
        {
            return JoinableTaskFactory.Run(asyncAction);
        }

        public void ThrowIfNotOnUIThread([CallerMemberName] string callerMemberName = "")
        {
            if (!_context.IsOnMainThread)
            {
                throw new Exception();
            }
        }
    }
}
