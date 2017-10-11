// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Threading;
using Moq;
using NuGet.PackageManagement.VisualStudio;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace NuGet.PackageManagement.UI.Test
{
    public class InfiniteScrollListTests
    {
        [WpfFact]
        public void Constructor_JoinableTaskFactoryIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(
                () => new InfiniteScrollList(joinableTaskFactory: null));

            Assert.Equal("joinableTaskFactory", exception.ParamName);
        }

        [WpfFact]
        public void CheckBoxesEnabled_Initialized_DefaultIsFalse()
        {
            var list = new InfiniteScrollList();

            Assert.False(list.CheckBoxesEnabled);
        }

        [WpfFact]
        public void DataContext_Initialized_DefaultIsItems()
        {
            var list = new InfiniteScrollList();

            Assert.Same(list.DataContext, list.Items);
        }

        [WpfFact]
        public void IsSolution_Initialized_DefaultIsFalse()
        {
            var list = new InfiniteScrollList();

            Assert.False(list.IsSolution);
        }

        [WpfFact]
        public void Items_Initialized_DefaultIsEmpty()
        {
            var list = new InfiniteScrollList();

            Assert.Empty(list.Items);
        }

        [WpfFact]
        public void PackageItems_Initialized_DefaultIsEmpty()
        {
            var list = new InfiniteScrollList();

            Assert.Empty(list.PackageItems);
        }

        [WpfFact]
        public void SelectedPackageItem_Initialized_DefaultIsNull()
        {
            var list = new InfiniteScrollList();

            Assert.Null(list.SelectedPackageItem);
        }

        [WpfFact]
        public void LoadItems_LoaderIsNull_Throws()
        {
            var list = new InfiniteScrollList();

            var exception = Assert.Throws<ArgumentNullException>(
                () => list.LoadItems(
                    loader: null,
                    loadingMessage: "a",
                    logger: null,
                    searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                    token: CancellationToken.None));

            Assert.Equal("loader", exception.ParamName);
        }

        [WpfTheory]
        [InlineData(null)]
        [InlineData("")]
        public void LoadItems_LoadingMessageIsNullOrEmpty_Throws(string loadingMessage)
        {
            var list = new InfiniteScrollList();

            var exception = Assert.Throws<ArgumentException>(
                () => list.LoadItems(
                    Mock.Of<IPackageItemLoader>(),
                    loadingMessage,
                    logger: null,
                    searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                    token: CancellationToken.None));

            Assert.Equal("loadingMessage", exception.ParamName);
        }

        [WpfFact]
        public void LoadItems_SearchResultTaskIsNull_Throws()
        {
            var list = new InfiniteScrollList();

            var exception = Assert.Throws<ArgumentNullException>(
                () => list.LoadItems(
                    Mock.Of<IPackageItemLoader>(),
                    loadingMessage: "a",
                    logger: null,
                    searchResultTask: null,
                    token: CancellationToken.None));

            Assert.Equal("searchResultTask", exception.ParamName);
        }

        [WpfFact]
        public void LoadItems_IfCancelled_Throws()
        {
            var list = new InfiniteScrollList();

            Assert.Throws<OperationCanceledException>(
                () => list.LoadItems(
                    Mock.Of<IPackageItemLoader>(),
                    loadingMessage: "a",
                    logger: null,
                    searchResultTask: Task.FromResult<SearchResult<IPackageSearchMetadata>>(null),
                    token: new CancellationToken(canceled: true)));
        }

        [WpfFact]
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

            using (var joinableTaskContext = new JoinableTaskContext(Thread.CurrentThread, SynchronizationContext.Current))
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

                list.LoadItems(
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
    }
}