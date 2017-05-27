// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace NuGet.PackageManagement.VisualStudio
{
    public interface IVsProjectThreadingService
    {
        JoinableTaskFactory JoinableTaskFactory { get; }

        void ExecuteSynchronously(Func<Task> asyncAction);

        T ExecuteSynchronously<T>(Func<Task<T>> asyncAction);

        void ThrowIfNotOnUIThread([CallerMemberName] string callerMemberName = "");
    }
}
