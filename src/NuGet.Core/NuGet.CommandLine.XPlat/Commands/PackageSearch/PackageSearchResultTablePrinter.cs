// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultTablePrinter : IPackageSearchResultRenderer
    {
        private string _searchTerm;
        private ILoggerWithColor _loggerWithColor;
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);

        public PackageSearchResultTablePrinter(string searchTerm, ILoggerWithColor loggerWithColor)
        {
            _searchTerm = searchTerm;
            _loggerWithColor = loggerWithColor;
        }

        public void Add(PackageSource source, IEnumerable<IPackageSearchMetadata> completedSearch)
        {
            _loggerWithColor.LogMinimal(SourceSeparator);
            _loggerWithColor.LogMinimal($"Source: {source.Name} ({source.SourceUri})");
            var table = new Table(new[] { 0, 2 }, "Package ID", "Latest Version", "Authors", "Downloads");
            PopulateTableWithResults(completedSearch, table);
            table.PrintResult(_searchTerm, _loggerWithColor);
        }

        public void Add(PackageSource source, string error)
        {
            _loggerWithColor.LogMinimal(SourceSeparator);
            _loggerWithColor.LogMinimal($"Source: {source.Name} ({source.SourceUri})");
            _loggerWithColor.LogError(error);
        }

        public void Finish()
        {
            // We don't need to write anything at the end of the rendering for a tabular format
        }

        public void Start()
        {
            // We don't need to write anything at the beginning of the rendering for a tabular format
        }

        /// <summary>
        /// Populates the given table with package metadata results.
        /// </summary>
        /// <param name="results">An enumerable of package search metadata to be processed and added to the table.</param>
        /// <param name="table">The table where the results will be added as rows.</param>
        private static void PopulateTableWithResults(IEnumerable<IPackageSearchMetadata> results, Table table)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            NumberFormatInfo nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
            nfi.NumberDecimalDigits = 0;

            foreach (IPackageSearchMetadata result in results)
            {
                string packageId = result.Identity.Id;
                string version = result.Identity.Version.ToNormalizedString();
                string authors = result.Authors;
                string downloads = "N/A";

                if (result.DownloadCount != null)
                {
                    downloads = string.Format(nfi, "{0:N}", result.DownloadCount);
                }

                table.AddRow(packageId, version, authors, downloads);
            }
        }
    }
}
