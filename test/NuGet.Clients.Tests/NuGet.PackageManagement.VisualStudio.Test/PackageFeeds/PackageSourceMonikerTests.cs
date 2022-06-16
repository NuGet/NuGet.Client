// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using Moq;
using NuGet.VisualStudio.Internal.Contracts;
using Xunit;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class PackageSourceMonikerTests
    {
        [Fact]
        public async Task PopulateListAsync_WithCancellationToken_ThrowsAsync()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(async () => await PackageSourceMoniker.PopulateListAsync(It.IsAny<IReadOnlyCollection<PackageSourceContextInfo>>(), cts.Token));
        }

        [Fact]
        public async Task PopulateListAsync_WithPackageSources_AggregateSourceGetsPriorityOrderAsync()
        {
            // Arrange
            var sources = new[]
            {
                new PackageSourceContextInfo("SuourceA", "SourceAName", isEnabled: true),
                new PackageSourceContextInfo("SuourceB", "SourceBName", isEnabled: true),
            };

            // Act
            IReadOnlyCollection<PackageSourceMoniker> result = await PackageSourceMoniker.PopulateListAsync(sources, CancellationToken.None);

            // Assert
            Assert.Equal(sources.Length + 1, result.Count);

            IEnumerable<PackageSourceMoniker> aggSource = result.Where(pkgSource => pkgSource.IsAggregateSource);

            PackageSourceMoniker aggMoniker = aggSource.First();
            Assert.All(result.Except(aggSource), psm => Assert.True(psm.PriorityOrder > aggMoniker.PriorityOrder));
        }

        // This test assumes running on english locale
        [Theory]
        [InlineData(new[] { "Aim", "C", "B" }, new[] { "All", "Aim", "B", "C" })]
        [InlineData(new[] { "Air" }, new[] { "Air" })]
        [InlineData(new[] { "Air", "Aim" }, new[] { "All", "Aim", "Air" })]
        public async Task PopulateListAsync_SimulatingSortingInTopPanel_AggregateSourceAlwaysFirstAsync(string[] sources, string[] expectedOrder)
        {
            // Arrange
            PackageSourceContextInfo[] sourcesContextInfo = sources
                .Select(pkgSrc => new PackageSourceContextInfo(pkgSrc, pkgSrc, isEnabled: true))
                .ToArray();

            IReadOnlyCollection<PackageSourceMoniker> result = await PackageSourceMoniker.PopulateListAsync(sourcesContextInfo, CancellationToken.None);

            // Simulates collectionViewSource found at PackageManagerTopPanel.xaml
            var cvs = new CollectionViewSource();
            cvs.SortDescriptions.Add(new SortDescription(nameof(PackageSourceMoniker.PriorityOrder), ListSortDirection.Ascending));
            cvs.SortDescriptions.Add(new SortDescription(nameof(PackageSourceMoniker.SourceName), ListSortDirection.Ascending));
            cvs.Culture = new System.Globalization.CultureInfo("en-US");
            cvs.Source = result;

            // Act
            IEnumerable<string> resultsSorted = cvs.View.Cast<PackageSourceMoniker>().Select(x => x.SourceName);

            // Assert
            Assert.Equal(expectedOrder, resultsSorted);
        }
    }
}
