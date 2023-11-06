// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultRendererTable : IPackageSearchResultRenderer
    {
        private PackageSearchArgs _args;
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);

        public PackageSearchResultRendererTable(PackageSearchArgs packageSearchArgs)
        {
            _args = packageSearchArgs;
        }

        public async Task Add(PackageSource source, Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask)
        {
            _args.Logger.LogMinimal(SourceSeparator);

            if (completedSearchTask == null)
            {
                _args.Logger.LogMinimal($"Source: {source.Name} ({source.SourceUri})");
                _args.Logger.LogMinimal(Strings.Error_CannotObtainSearchSource);
                return;
            }

            _args.Logger.LogMinimal($"Source: {source.Name} ({source.SourceUri})");
            IEnumerable<IPackageSearchMetadata> searchResult = await completedSearchTask;

            if (searchResult == null)
            {
                _args.Logger.LogMinimal(Strings.Error_NoResource);
                return;
            }

            var table = new Table(new[] { 0, 2 }, "Package ID", "Latest Version", "Authors", "Downloads");

            if (_args.ExactMatch)
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

            table.PrintResult(_args.SearchTerm, _args.Logger);
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
