// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Sdk.TestFramework;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    [Collection(MockedVS.Collection)]
    public class SharedServiceStateTests : MockedVSCollectionTests
    {
        public SharedServiceStateTests(GlobalServiceProvider globalServiceProvider)
            : base(globalServiceProvider)
        {
            var solutionManager = new Mock<IVsSolutionManager>();

            solutionManager.SetupGet(x => x.SolutionDirectory)
                .Returns(@"C:\a");

            SourceRepositoryProvider sourceRepositoryProvider = TestSourceRepositoryUtility.CreateV3OnlySourceRepositoryProvider();

            AddService<IDeleteOnRestartManager>(Task.FromResult<object>(Mock.Of<IDeleteOnRestartManager>()));
            AddService<ISettings>(Task.FromResult<object>(Mock.Of<ISettings>()));
            AddService<ISourceRepositoryProvider>(Task.FromResult<object>(sourceRepositoryProvider));
            AddService<IVsSolutionManager>(Task.FromResult<object>(solutionManager.Object));
        }

        [Fact]
        public async Task GetPackageManagerAsync_Always_ReturnsNewInstance()
        {
            using (SharedServiceState state = await SharedServiceState.CreateAsync(CancellationToken.None))
            {
                NuGetPackageManager packageManager0 = await state.GetPackageManagerAsync(CancellationToken.None);
                NuGetPackageManager packageManager1 = await state.GetPackageManagerAsync(CancellationToken.None);
                NuGetPackageManager packageManager2 = await state.GetPackageManagerAsync(CancellationToken.None);

                Assert.NotNull(packageManager0);
                Assert.NotNull(packageManager1);
                Assert.NotNull(packageManager2);

                Assert.NotSame(packageManager0, packageManager1);
                Assert.NotSame(packageManager0, packageManager2);
            }
        }
    }
}
