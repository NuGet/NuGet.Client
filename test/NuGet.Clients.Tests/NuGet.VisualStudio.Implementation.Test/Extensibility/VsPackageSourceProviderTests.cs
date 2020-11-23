// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Moq;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.VisualStudio.Telemetry;
using Xunit;

namespace NuGet.VisualStudio.Implementation.Test
{
    public class VsPackageSourceProviderTests
    {
        // known/expected errors should not be reported to telemetry, hence use MockBehavior.Strict
        private Mock<INuGetTelemetryProvider> _telemetryProvider = new Mock<INuGetTelemetryProvider>(MockBehavior.Strict);

        [Fact]
        public void Constructor_WhenSourceRepositoryProviderIsNull_Throws()
        {
            var exception = Assert.Throws<ArgumentNullException>(() => new VsPackageSourceProvider(sourceRepositoryProvider: null, _telemetryProvider.Object));

            Assert.Equal("sourceRepositoryProvider", exception.ParamName);
        }

        [Theory]
        [InlineData(typeof(ArgumentException))]
        [InlineData(typeof(ArgumentNullException))]
        [InlineData(typeof(InvalidDataException))]
        [InlineData(typeof(InvalidOperationException))]
        public void GetSources_WhenKnownExceptionIsThrown_ThrowsThatException(Type exceptionType)
        {
            var sourceRepositoryProvider = new Mock<ISourceRepositoryProvider>();
            var packageSourceProvider = new Mock<IPackageSourceProvider>();

            sourceRepositoryProvider.SetupGet(x => x.PackageSourceProvider)
                .Returns(packageSourceProvider.Object);

            var expectedException = (Exception)Activator.CreateInstance(exceptionType);

            packageSourceProvider.Setup(x => x.LoadPackageSources())
                .Throws(expectedException);

            var vsPackageSourceProvider = new VsPackageSourceProvider(sourceRepositoryProvider.Object, _telemetryProvider.Object);

            Assert.Throws(exceptionType, () => vsPackageSourceProvider.GetSources(includeUnOfficial: true, includeDisabled: true));
        }

        [Fact]
        public void GetSources_WhenUnknownExceptionIsThrown_ThrowsKnownException()
        {
            var sourceRepositoryProvider = new Mock<ISourceRepositoryProvider>();
            var packageSourceProvider = new Mock<IPackageSourceProvider>();
            var telemetryProvider = new Mock<INuGetTelemetryProvider>();

            sourceRepositoryProvider.SetupGet(x => x.PackageSourceProvider)
                .Returns(packageSourceProvider.Object);

            var originalException = new NuGetConfigurationException("a");

            packageSourceProvider.Setup(x => x.LoadPackageSources())
                .Throws(originalException);

            var vsPackageSourceProvider = new VsPackageSourceProvider(sourceRepositoryProvider.Object, telemetryProvider.Object);

            var actualException = Assert.Throws<InvalidOperationException>(() => vsPackageSourceProvider.GetSources(includeUnOfficial: true, includeDisabled: true));

            Assert.Equal(originalException.Message, actualException.Message);
            Assert.Same(originalException, actualException.InnerException);

            telemetryProvider.Verify(p => p.PostFault(originalException, typeof(VsPackageSourceProvider).FullName, nameof(VsPackageSourceProvider.GetSources), It.IsAny<IDictionary<string, object>>()));
        }
    }
}
