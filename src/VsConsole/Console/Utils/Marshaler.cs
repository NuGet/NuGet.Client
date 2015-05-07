// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Shell;

namespace NuGetConsole
{
    internal class Marshaler<T>
    {
        protected readonly T _impl;

        protected Marshaler(T impl)
        {
            _impl = impl;
        }

        /// <summary>
        /// Invoke an action on the main UI thread.
        /// </summary>
        protected static void Invoke(Action action)
        {
            ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    action();
                });
        }

        /// <summary>
        /// Invoke a function on the main UI thread.
        /// </summary>
        protected static TResult Invoke<TResult>(Func<TResult> func)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    return func();
                });
        }
    }
}
