// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.PackageManagement.UI.Test
{
    /// <summary>
    /// Regression coverage for the
    /// <c>ArgumentOutOfRangeException</c> Watson crash that originated in
    /// <c>InfiniteScrollList.RepopulatePackageListAsync</c>. The production
    /// fix wraps every background-thread mutation of <c>Items</c> in
    /// <c>_list.ItemsLock.ExecuteAsync(...)</c> so the registered
    /// <see cref="BindingOperations.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>
    /// lock is held whenever WPF's cross-thread change log is updated.
    /// </summary>
    /// <remarks>
    /// These tests don't construct <c>InfiniteScrollList</c> directly because
    /// loading its XAML inside the test process is unreliable (see
    /// https://github.com/NuGet/Home/issues/10938). Instead they reproduce the
    /// exact WPF collection-binding configuration used by the control:
    /// an <see cref="ObservableCollection{T}"/> registered with
    /// <see cref="BindingOperations.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>,
    /// projected through a <see cref="ListCollectionView"/> with live
    /// filtering and live grouping enabled, and then drive the same race
    /// (background <c>Add</c> + UI-thread <c>Clear</c> + <c>Refresh</c>) that
    /// surfaced the crash in production.
    /// </remarks>
    public class InfiniteScrollListCollectionSyncTests
    {
        private const int StressIterations = 200;

        private readonly ITestOutputHelper _output;

        public InfiniteScrollListCollectionSyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WpfFact]
        public async Task BackgroundMutation_UnderRegisteredLock_DoesNotThrowOnDispatcherPump()
        {
            ListCollectionView view = CreateView(out ObservableCollection<object> items, out object lockObject);

            int dispatcherFailures = await StressAsync(items, lockObject, view, takeLock: true);

            Assert.Equal(0, dispatcherFailures);
        }

        [WpfFact]
        public async Task BackgroundMutation_WithoutRegisteredLock_CanThrowOnDispatcherPump()
        {
            // This is the bug we fixed: when background threads mutate a
            // collection registered with EnableCollectionSynchronization
            // without holding the registered lock, the underlying
            // ObservableCollection's CollectionChanged events can interleave
            // with each other and with the WPF binding engine's processing of
            // the cross-thread change log. The dispatcher then rethrows from
            // ListCollectionView.ProcessCollectionChanged - typically as
            // ArgumentOutOfRangeException out of ArrayList.Insert when the
            // shadow copy is smaller than the captured NewStartingIndex, but
            // we accept any exception thrown out of WPF's binding-engine
            // change-log processing as evidence of the bug.
            //
            // The race is timing dependent, so we only require it to surface
            // at least once across many iterations to prove the unsafe pattern
            // is genuinely unsafe and that the locked variant in the test
            // above is doing the work.

            ListCollectionView view = CreateView(out ObservableCollection<object> items, out object lockObject);

            int dispatcherFailures = await StressAsync(items, lockObject, view, takeLock: false);

            Assert.True(
                dispatcherFailures > 0,
                $"Expected the unsynchronized pattern to surface a dispatcher exception at least once across {StressIterations} iterations, but observed none. " +
                "If this assertion ever fails, it likely means WPF changed its cross-thread collection behavior; revisit the regression test design and the production fix together.");
        }

        private async Task<int> StressAsync(
            ObservableCollection<object> items,
            object lockObject,
            ListCollectionView view,
            bool takeLock)
        {
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
                for (int i = 0; i < StressIterations; i++)
                {
                    // Each iteration simulates the production race that
                    // surfaced the Watson crash:
                    //   * A prior load cleared the collection under the
                    //     registered lock (ClearPackageList).
                    //   * Multiple background threads then race to add
                    //     loading indicators. In the buggy version the Add
                    //     calls happen on Task.Run without the lock and the
                    //     ObservableCollection's CollectionChanged events get
                    //     queued to the WPF cross-thread change log with
                    //     indices that don't match each other when the
                    //     dispatcher later processes them.
                    lock (lockObject)
                    {
                        items.Clear();
                    }

                    Task[] bgTasks = new Task[4];
                    for (int t = 0; t < bgTasks.Length; t++)
                    {
                        bgTasks[t] = Task.Run(() =>
                        {
                            if (takeLock)
                            {
                                lock (lockObject)
                                {
                                    items.Add(new object());
                                    items.Add(new object());
                                }
                            }
                            else
                            {
                                items.Add(new object());
                                items.Add(new object());
                            }
                        });
                    }

                    await Task.WhenAll(bgTasks);

                    // Pump the dispatcher so any queued cross-thread change-log
                    // entries are drained before the next iteration. This is
                    // the point at which the buggy ordering surfaces as an
                    // exception out of ListCollectionView.ProcessCollectionChanged.
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

                    // Force shadow rebuild via Refresh once items are stable,
                    // mirroring AddVulnerabilitiesFiltering / RemoveVulnerabilitiesFiltering /
                    // AddPackageLevelGrouping in the production code.
                    view.Refresh();
                    await dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                }
            }
            finally
            {
                dispatcher.UnhandledException -= OnUnhandled;
            }

            return dispatcherFailures;
        }

        private static ListCollectionView CreateView(
            out ObservableCollection<object> items,
            out object lockObject)
        {
            items = new ObservableCollection<object>();
            lockObject = new object();

            BindingOperations.EnableCollectionSynchronization(items, lockObject);

            ListCollectionView view = (ListCollectionView)CollectionViewSource.GetDefaultView(items);
            view.IsLiveFiltering = true;
            view.LiveFilteringProperties.Add(nameof(NotifyCollectionChangedAction)); // any property name works for the registration
            view.LiveGroupingProperties.Add(nameof(NotifyCollectionChangedAction));

            return view;
        }
    }
}
