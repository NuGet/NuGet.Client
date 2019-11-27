// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class DedicatedAsynchronousProcessingThreadTests
    {
        [Fact]
        public void Dispose_IsIdempotent()
        {
            using (var thread = new DedicatedAsynchronousProcessingThread())
            {
                thread.Dispose();
                thread.Dispose();
            }
        }

        [Fact]
        public void Start_ThrowsForDisposesObject()
        {
            var thread = new DedicatedAsynchronousProcessingThread();
            thread.Dispose();
            Assert.Throws<ObjectDisposedException>(() => thread.Start());
        }

        [Fact]
        public void Push_ThrowsForDisposesObject()
        {
            var thread = new DedicatedAsynchronousProcessingThread();
            thread.Start();
            thread.Dispose();

            Assert.Throws<ObjectDisposedException>(() => thread.Push(() => Task.CompletedTask));
        }

        [Fact]
        public void Start_ThrowsForAlreadyStartedObject()
        {
            using (var thread = new DedicatedAsynchronousProcessingThread())
            {
                thread.Start();
                var exception = Assert.Throws<InvalidOperationException>(() => thread.Start());
                exception.Message.Should().Be("The processing thread is already started.");
            }
        }

        [Fact]
        public void Push_ThrowsForNotStartedObject()
        {
            using (var thread = new DedicatedAsynchronousProcessingThread())
            {
                var exception = Assert.Throws<InvalidOperationException>(() => thread.Push(() => Task.CompletedTask));
                exception.Message.Should().Be("The processing thread is not started yet.");
            }
        }

        [Fact]
        public void Push_ExecutesTaskAsynchronously()
        {
            using (var thread = new DedicatedAsynchronousProcessingThread())
            using (var handledEvent = new ManualResetEventSlim(initialState: false))
            {
                thread.Start();
                var executed = false;
                Func<Task> task = () => { executed = true; handledEvent.Set(); return Task.CompletedTask; };
                thread.Push(task);
                handledEvent.Wait();
                Assert.True(executed);
            }
        }

        [Fact]
        public void Push_ExecutesTaskAsynchronouslyInSameOrderAsPush()
        {
            using (var countdownEvent = new CountdownEvent(3))
            using (var thread = new DedicatedAsynchronousProcessingThread())
            {
                thread.Start();
                var queue = new Queue<int>();

                Func<Task> task1 = () => { queue.Enqueue(1); countdownEvent.Signal(); return Task.CompletedTask; };
                Func<Task> task2 = () => { queue.Enqueue(2); countdownEvent.Signal(); return Task.CompletedTask; };
                Func<Task> task3 = () => { queue.Enqueue(3); countdownEvent.Signal(); return Task.CompletedTask; };

                thread.Push(task1);
                thread.Push(task2);
                thread.Push(task3);

                countdownEvent.Wait();
                Assert.Equal(1, queue.Dequeue());
                Assert.Equal(2, queue.Dequeue());
                Assert.Equal(3, queue.Dequeue());
                Assert.Empty(queue);
            }
        }

        [Fact]
        public void Push_ExecutesNoLaterThanDelay()
        {
            var delay = 100;
            var tolerance = 5;
            using (var handledEvent = new ManualResetEventSlim(initialState: false))
            using (var thread = new DedicatedAsynchronousProcessingThread(delay))
            {
                thread.Start();
                var stopwatch = new Stopwatch();
                Func<Task> task1 = () => { stopwatch.Stop(); handledEvent.Set(); return Task.CompletedTask; };
                stopwatch.Start();
                thread.Push(task1);
                handledEvent.Wait();
                Assert.True(delay + tolerance > stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
