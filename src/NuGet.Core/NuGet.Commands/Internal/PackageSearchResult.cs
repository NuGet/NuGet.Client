// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.Commands.Internal
{
    internal class PackageSearchResult
    {
        private readonly List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)> _taskList;
        private readonly ILogger _logger;
        private readonly string _searchTerm;
        private readonly bool _isExactMatch;
        const int LineSeparatorLength = 40;
        static readonly string SourceSeparator = new string('=', LineSeparatorLength);

        public PackageSearchResult(List<(Task<IEnumerable<IPackageSearchMetadata>>, PackageSource)> taskList, ILogger logger, string searchTerm, bool isExactMatch = false)
        {
            _taskList = taskList;
            _logger = logger;
            _searchTerm = searchTerm;
            _isExactMatch = isExactMatch;
        }

        public async Task PrintResultTablesAsync()
        {
            while (_taskList.Count > 0)
            {
                var completedSearchTask = await Task.WhenAny(_taskList.Select(t => t.Item1));
                int taskIndex = _taskList.FindIndex(t => t.Item1 == completedSearchTask);
                PackageSource source = _taskList[taskIndex].Item2;

                // search task at taskIndex is done
                if (completedSearchTask == null)
                {
                    _logger.LogMinimal(SourceSeparator);
                    _logger.LogMinimal($"Source: {source.Name}");
                    continue;
                }

                _logger.LogMinimal(SourceSeparator);
                _logger.LogMinimal($"Source: {source.Name}");
                IEnumerable<IPackageSearchMetadata> searchResult = completedSearchTask.Result;
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
                _taskList.RemoveAt(taskIndex);
            }
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
