// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.VisualStudio.Threading;
using NuGet.PackageManagement.UI.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.PackageManagement.UI.Test
{
    /// <summary>
    /// Regression coverage for the Watson <see cref="System.ArgumentOutOfRangeException"/> in
    /// <see cref="System.Windows.Data.ListCollectionView.ProcessCollectionChanged"/> that originated in
    /// <c>InfiniteScrollListViewModel.RepopulatePackageListAsync</c>.
    ///
    /// The collection backing <c>InfiniteScrollListViewModel.Items</c> is registered with
    /// <see cref="BindingOperations.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>
    /// using <c>InfiniteScrollListViewModel.ItemsLock</c>, so every cross-thread mutation must hold that lock.
    /// The test runs <see cref="InfiniteScrollListViewModel.AddLoadingIndicatorsAsync"/> on a background thread the way
    /// production does, and asserts the dispatcher never observes an unhandled exception from WPF's binding engine.
    ///
    /// Note: this test deliberately does <b>not</b> construct an <c>InfiniteScrollList</c> instance — XAML loading
    /// inside the test process is unreliable, which is why every existing
    /// <c>InfiniteScrollListTests</c> entry is skipped via <c>https://github.com/NuGet/Home/issues/10938</c>.
    /// Calling the extracted helper directly is enough to cover the unsynchronized-mutation bug.
    /// </summary>
    public class InfiniteScrollListCollectionSyncTests
    {
        // Each iteration races four parallel callers of AddLoadingIndicatorsAsync against a view Refresh,
        // mirroring the production sequence where a second load or a filter/grouping change rebuilds the
        // ListCollectionView shadow copy concurrently with the background-thread Adds. A low iteration count
        // is enough because each iteration runs four concurrent producers, so the unsynchronized Adds reliably
        // surface the race on the buggy code path.
        private const int Iterations = 10;
        private const int ParallelProducers = 4;

        private readonly ITestOutputHelper _output;

        public InfiniteScrollListCollectionSyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WpfFact]
        public async Task AddLoadingIndicatorsAsync_FromBackgroundThreads_DoesNotCrashDispatcher()
        {
            using JoinableTaskContext joinableTaskContext = NewJoinableTaskContext();
            using ReentrantSemaphore itemsLock = ReentrantSemaphore.Create(
                initialCount: 1,
                joinableTaskContext: joinableTaskContext,
                mode: ReentrantSemaphore.ReentrancyMode.Stack);

            ObservableCollection<object> items = new ObservableCollection<object>();
            BindingOperations.EnableCollectionSynchronization(items, itemsLock);

            // Match the live-shaping configuration set up by InfiniteScrollList's constructor.
            ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(items);
            view.IsLiveFiltering = true;
            view.IsLiveGrouping = true;

            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            int dispatcherFailures = 0;

            void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
            {
                Interlocked.Increment(ref dispatcherFailures);
                _output.WriteLine($"Caught {e.Exception.GetType().Name}: {e.Exception.Message}");
                e.Handled = true;
            }

            dispatcher.UnhandledException += OnUnhandled;
            try
            {
                for (int i = 0; i < Iterations; i++)
                {
                    await itemsLock.ExecuteAsync(() =>
                    {
                        items.Clear();
                        return Task.CompletedTask;
                    });

                    Task[] producers = new Task[ParallelProducers];
                    for (int t = 0; t < producers.Length; t++)
                    {
                        producers[t] = Task.Run(async () =>
                        {
                            await InfiniteScrollListViewModel.AddLoadingIndicatorsAsync(
                                items,
                                loadingStatusIndicator: new object(),
                                loadingVulnerabilitiesStatusIndicator: new object(),
                                itemsLock);
                        });
                    }

                    await Task.WhenAll(producers);
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

                    // Mirrors AddVulnerabilitiesFiltering / AddPackageLevelGrouping in production, which call
                    // ItemsView.Refresh() and force WPF to rebuild its shadow copy.
                    view.Refresh();
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                }
            }
            finally
            {
                dispatcher.UnhandledException -= OnUnhandled;
            }

            Assert.Equal(0, dispatcherFailures);
        }

#pragma warning disable VSSDK005 // Allow constructing a JoinableTaskContext in a test that doesn't run inside a VS host.
        private static JoinableTaskContext NewJoinableTaskContext() => new JoinableTaskContext();
#pragma warning restore VSSDK005
    }
}
