// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResult
    {
        private readonly Task<IEnumerable<IPackageSearchMetadata>> _completedSearchTask;
        private readonly PackageSource _source;
        private readonly ILogger _logger;
        private readonly string _searchTerm;
        private readonly bool _isExactMatch;
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);

        public PackageSearchResult(Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask, PackageSource source, ILogger logger, string searchTerm, bool isExactMatch = false)
        {
            _completedSearchTask = completedSearchTask;
            _source = source;
            _logger = logger;
            _searchTerm = searchTerm;
            _isExactMatch = isExactMatch;
        }

        public async Task PrintResultTablesAsync()
        {
            _logger.LogMinimal(SourceSeparator);

            if (_completedSearchTask == null)
            {
                _logger.LogMinimal($"Source: {_source.Name}");
                _logger.LogMinimal("Failed to obtain a search resource.");
                return;
            }

            _logger.LogMinimal($"Source: {_source.Name}");
            IEnumerable<IPackageSearchMetadata> searchResult = await _completedSearchTask;
            var table = new PackageSearchResultTable(new[] { 0, 2 }, "Package ID", "Latest Version", "Authors", "Downloads");

            if (_isExactMatch)
            {
                var firstResult = searchResult.FirstOrDefault();
                if (firstResult != null)
                {
                    PopulateTableWithResults(new[] { firstResult }, table);
                }
            }
            else
            {
                PopulateTableWithResults(searchResult, table);
            }

            table.PrintResult(_searchTerm);
        }

        /// <summary>
        /// Populates the given table with package metadata results.
        /// </summary>
        /// <param name="results">An enumerable of package search metadata to be processed and added to the table.</param>
        /// <param name="table">The table where the results will be added as rows.</param>
        private static void PopulateTableWithResults(IEnumerable<IPackageSearchMetadata> results, PackageSearchResultTable table)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;

            foreach (IPackageSearchMetadata result in results)
            {
                string packageId = result.Identity.Id;
                string version = result.Identity.Version.ToNormalizedString();
                string authors = result.Authors;
                string downloads = "N/A";

                if (result.DownloadCount != null)
                {
                    NumberFormatInfo nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
                    nfi.NumberDecimalDigits = 0;
                    downloads = string.Format(nfi, "{0:N}", result.DownloadCount);
                }

                table.AddRow(packageId, version, authors, downloads);
            }
        }

    }
}
