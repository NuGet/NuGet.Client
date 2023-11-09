// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultTableRenderer : IPackageSearchResultRenderer
    {
        private string _searchTerm;
        private ILoggerWithColor _loggerWithColor;
        private bool _exactMatch;
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);

        public PackageSearchResultTableRenderer(string searchTerm, ILoggerWithColor loggerWithColor, bool exactMatch)
        {
            _searchTerm = searchTerm;
            _loggerWithColor = loggerWithColor;
            _exactMatch = exactMatch;
        }

        public async Task Add(PackageSource source, Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask)
        {
            IEnumerable<IPackageSearchMetadata> searchResult;

            _loggerWithColor.LogMinimal(SourceSeparator);
            _loggerWithColor.LogMinimal($"Source: {source.Name} ({source.SourceUri})");

            if (completedSearchTask == null)
            {
                _loggerWithColor.LogMinimal(Strings.Error_CannotObtainSearchSource);
                return;
            }

            try
            {
                searchResult = await completedSearchTask;
            }
            catch (FatalProtocolException ex)
            {
                // search
                // Throws FatalProtocolException for JSON parsing errors as fatal metadata issues.
                // Throws FatalProtocolException for HTTP request issues indicating critical source(v2/v3) problems.
                _loggerWithColor.LogError(ex.Message);
                return;
            }
            catch (OperationCanceledException ex)
            {
                _loggerWithColor.LogError(ex.Message);
                return;
            }
            catch (InvalidOperationException ex)
            {
                // Thrown for a local package with an invalid source destination.
                _loggerWithColor.LogError(ex.Message);
                return;
            }

            if (searchResult == null)
            {
                _loggerWithColor.LogMinimal(Strings.Error_NoResource);
                return;
            }

            var table = new Table(new[] { 0, 2 }, "Package ID", "Latest Version", "Authors", "Downloads");

            if (_exactMatch)
            {
                var lastResult = searchResult.LastOrDefault();
                if (lastResult != null)
                {
                    PopulateTableWithResults(new[] { lastResult }, table);
                }
            }
            else
            {
                PopulateTableWithResults(searchResult, table);
            }

            table.PrintResult(_searchTerm, _loggerWithColor);
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
