// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test.Utility
{
    public sealed class NonPumpingSynchronizationContext : SynchronizationContext
    {
        private readonly List<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private readonly Action? _onWait;
        private bool _allowContinuations;

        private NonPumpingSynchronizationContext(Action? onWait)
        {
            _onWait = onWait;
            SetWaitNotificationRequired();
        }

        public override int Wait(nint[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            _onWait?.Invoke();
            return WaitHelper(waitHandles, waitAll, millisecondsTimeout);
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_callbacks)
            {
                if (!_allowContinuations)
                {
                    _callbacks.Add((d, state));
                    return;
                }
            }

            ThreadPool.QueueUserWorkItem(_ => d(state));
        }

        public static async Task RunAsync(Action action, Action? onWait = null)
        {
            var context = new NonPumpingSynchronizationContext(onWait);
            var work = Task.Run(() =>
            {
                SynchronizationContext? previous = Current;
                SetSynchronizationContext(context);
                try
                {
                    action();
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            });

            Task completed = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30)));

            // Release captured continuations only for cleanup, so a regression cannot strand the test runner.
            lock (context._callbacks)
            {
                context._allowContinuations = true;
                foreach ((SendOrPostCallback callback, object? state) in context._callbacks)
                {
                    ThreadPool.QueueUserWorkItem(_ => callback(state));
                }
                context._callbacks.Clear();
            }

            Assert.Same(work, await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(30))));
            await work;
            Assert.True(completed == work, "The operation required pumping the caller's synchronization context.");
        }
    }
}
