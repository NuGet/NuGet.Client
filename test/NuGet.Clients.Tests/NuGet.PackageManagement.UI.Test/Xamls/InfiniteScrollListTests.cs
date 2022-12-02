// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio;
using Xunit;
using Xunit.Abstractions;

namespace NuGet.PackageManagement.UI.Test
{
    [Collection(MockedVS.Collection)]
    public class InfiniteScrollListTests : IDisposable
    {
        private JoinableTaskContext _joinableTaskContext;
        private readonly ITestOutputHelper _output;

        public InfiniteScrollListTests(ITestOutputHelper output)
        {
#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            _joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current);
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(_joinableTaskContext.Factory);

            _output = output;
        }

        public void Dispose()
        {
            _joinableTaskContext?.Dispose();
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void Constructor_JoinableTaskFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new InfiniteScrollList(joinableTaskFactory: null));

            Assert.Equal("joinableTaskFactory", exception.ParamName);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void CheckBoxesEnabled_Initialized_DefaultIsFalse()
        {
            var list = new InfiniteScrollList();

            Assert.False(list.CheckBoxesEnabled);
        }

        public void DataContext_Initialized_DefaultIsItems()
        {
            var list = new InfiniteScrollList();

            Assert.Same(list.DataContext, list.Items);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void IsSolution_Initialized_DefaultIsFalse()
        {
            var list = new InfiniteScrollList();

            Assert.False(list.IsSolution);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void Items_Initialized_DefaultIsEmpty()
        {
            var list = new InfiniteScrollList();

            Assert.Empty(list.Items);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void PackageItems_Initialized_DefaultIsEmpty()
        {
            var list = new InfiniteScrollList();

            Assert.Empty(list.PackageItems);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public void SelectedPackageItem_Initialized_DefaultIsNull()
        {
            var list = new InfiniteScrollList();

            Assert.Null(list.SelectedPackageItem);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public async Task LoadItems_LoaderIsNull_Throws()
        {
            var list = new InfiniteScrollList();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    await list.LoadItemsAsync(
                        loader: null,
                        loadingMessage: "a",
                        logger: null,
                        searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                        token: CancellationToken.None);
                });

            Assert.Equal("loader", exception.ParamName);
        }

        [WpfTheory]
        [InlineData(null)]
        [InlineData("")]
        public async Task LoadItems_LoadingMessageIsNullOrEmpty_Throws(string loadingMessage)
        {
            var list = new InfiniteScrollList();

            var exception = await Assert.ThrowsAsync<ArgumentException>(
                async () =>
                {
                    await list.LoadItemsAsync(
                        Mock.Of<IPackageItemLoader>(),
                        loadingMessage,
                        logger: null,
                        searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                        token: CancellationToken.None);
                });

            Assert.Equal("loadingMessage", exception.ParamName);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public async Task LoadItems_SearchResultTaskIsNull_Throws()
        {
            var list = new InfiniteScrollList();

            var exception = await Assert.ThrowsAsync<ArgumentNullException>(
                async () =>
                {
                    await list.LoadItemsAsync(
                        Mock.Of<IPackageItemLoader>(),
                        loadingMessage: "a",
                        logger: null,
                        searchResultTask: null,
                        token: CancellationToken.None);
                });

            Assert.Equal("searchResultTask", exception.ParamName);
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public async Task LoadItems_IfCancelled_Throws()
        {
            var list = new InfiniteScrollList();

            await Assert.ThrowsAsync<OperationCanceledException>(
                async () =>
                {
                    await list.LoadItemsAsync(
                        Mock.Of<IPackageItemLoader>(),
                        loadingMessage: "a",
                        logger: null,
                        searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                        token: new CancellationToken(canceled: true));
                });
        }

        [WpfFact(Skip = "https://github.com/NuGet/Home/issues/10938")]
        public async Task LoadItems_BeforeGettingCurrent_WaitsForInitialResults()
        {
            var loader = new Mock<IPackageItemLoader>(MockBehavior.Strict);
            var state = new Mock<IItemLoaderState>();
            var hasWaited = false;

            loader.SetupGet(x => x.IsMultiSource)
                .Returns(true);
            loader.SetupGet(x => x.State)
                .Returns(state.Object);
            loader.Setup(x => x.UpdateStateAndReportAsync(
                    It.IsNotNull<SearchResult<IPackageSearchMetadata>>(),
                    It.IsNotNull<IProgress<IItemLoaderState>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0));

            var loadingStatus = LoadingStatus.Loading;
            var loadingStatusCallCount = 0;

            state.Setup(x => x.LoadingStatus)
                .Returns(() => loadingStatus)
                .Callback(() =>
                    {
                        ++loadingStatusCallCount;

                        if (loadingStatusCallCount >= 2)
                        {
                            loadingStatus = LoadingStatus.NoItemsFound;
                            hasWaited = true;
                        }
                    });

            var itemsCount = 0;
            var itemsCountCallCount = 0;

            state.Setup(x => x.ItemsCount)
                .Returns(() => itemsCount)
                .Callback(() =>
                    {
                        ++itemsCountCallCount;

                        if (itemsCountCallCount >= 2)
                        {
                            itemsCount = 1;
                            hasWaited = true;
                        }
                    });

            loader.Setup(x => x.UpdateStateAsync(
                    It.IsNotNull<IProgress<IItemLoaderState>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(0));

            var logger = new Mock<INuGetUILogger>();
            var searchResultTask = Task.FromResult(new SearchResult<IPackageSearchMetadata>());

#pragma warning disable VSSDK005 // Avoid instantiating JoinableTaskContext
            using (var joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current))
#pragma warning restore VSSDK005 // Avoid instantiating JoinableTaskContext
            {
                var list = new InfiniteScrollList(new Lazy<JoinableTaskFactory>(() => joinableTaskContext.Factory));
                var taskCompletionSource = new TaskCompletionSource<string>();

                // Despite LoadItems(...) being a synchronous method, the method internally fires an asynchronous task.
                // We'll know when that task completes successfully when the LoadItemsCompleted event fires,
                // and to avoid infinite waits in exceptional cases, we'll interpret a call to reset as a failure.
                list.LoadItemsCompleted += (sender, args) => taskCompletionSource.TrySetResult(null);

                loader.Setup(x => x.Reset());
                logger.Setup(x => x.Log(
                        It.Is<MessageLevel>(m => m == MessageLevel.Error),
                        It.IsNotNull<string>(),
                        It.IsAny<object[]>()))
                    .Callback<MessageLevel, string, object[]>(
                        (messageLevel, message, args) =>
                            {
                                taskCompletionSource.TrySetResult(message);
                            });
                loader.Setup(x => x.GetCurrent())
                    .Returns(() =>
                    {
                        if (!hasWaited)
                        {
                            taskCompletionSource.TrySetResult("GetCurrent() was called before waiting for initial results.");
                        }

                        return Enumerable.Empty<PackageItemListViewModel>();
                    });

                await list.LoadItemsAsync(
                    loader.Object,
                    loadingMessage: "a",
                    logger: logger.Object,
                    searchResultTask: searchResultTask,
                    token: CancellationToken.None);

                var errorMessage = await taskCompletionSource.Task;

                Assert.Null(errorMessage);

                loader.Verify();
            }
        }

        [WpfTheory]
        [MemberData(nameof(TestSearchMetadata))]
        public async Task LoadItemsAsync_LoadingStatusIndicator_InItemsCollectionIfEmptySearch(
            IPackageSearchMetadata[] searchItems,
            int expectedItems)
        {
            var loaderMock = new Mock<IPackageItemLoader>(MockBehavior.Strict);
            var stateMock = new Mock<IItemLoaderState>();
            var searchTask = Task.FromResult(SearchResult.FromItems( searchItems ));
            var testLogger = new TestNuGetUILogger(_output);
            var tcs = new TaskCompletionSource<int>();
            var list = new InfiniteScrollList(new Lazy<JoinableTaskFactory>(() => _joinableTaskContext.Factory));

            var currentStatus = LoadingStatus.Loading;

            stateMock.Setup(x => x.LoadingStatus)
                .Returns(() => currentStatus);
            stateMock.Setup(x => x.ItemsCount)
                .Returns(() => searchItems.Length);
            loaderMock.SetupGet(x => x.State)
                .Returns(stateMock.Object);
            loaderMock.SetupGet(x => x.IsMultiSource)
                .Returns(false);
            loaderMock.Setup(x => x.UpdateStateAndReportAsync(
                    It.IsNotNull<SearchResult<IPackageSearchMetadata>>(),
                    It.IsNotNull<IProgress<IItemLoaderState>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(0))
                .Callback(() => {
                    currentStatus = searchItems.Length > 0 ? LoadingStatus.Ready : LoadingStatus.NoItemsFound;
                });
            loaderMock.Setup(x => x.UpdateStateAsync(
                    It.IsNotNull<IProgress<IItemLoaderState>>(),
                    It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(0));
            loaderMock.Setup(x => x.GetCurrent())
                .Returns(() => searchItems.Select(x => new PackageItemListViewModel()));

            list.LoadItemsCompleted += (sender, args) =>
            {
                var lst = (InfiniteScrollList)sender;
                tcs.TrySetResult(lst.Items.Count);
                _output.WriteLine("3. After assert");
            };

            _output.WriteLine("1. Init act");
            await list.LoadItemsAsync(
                loader: loaderMock.Object,
                loadingMessage: "Test loading",
                logger: testLogger,
                searchResultTask: searchTask,
                token: CancellationToken.None);
            _output.WriteLine("2. End act");

            var finished = await tcs.Task;

            Assert.Equal(expectedItems, finished);
            _output.WriteLine("4. End of test");
        }

        public static IEnumerable<object[]> TestSearchMetadata()
        {
            var allData = new List<object[]>
            {
                new object[]{ new IPackageSearchMetadata[] {
                }, 1 }, // only loading indicator
                new object[]{ new IPackageSearchMetadata[] {
                    new TestPackageSearchMetadata(),
                    new TestPackageSearchMetadata(),
                    new TestPackageSearchMetadata(),
                }, 3 }, // only search elements
            };

            return allData;
        }
    }
}
