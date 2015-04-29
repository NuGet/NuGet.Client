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
        static protected void Invoke(Action action)
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
        static protected TResult Invoke<TResult>(Func<TResult> func)
        {
            return ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                return func();
            });
        }
    }
}
