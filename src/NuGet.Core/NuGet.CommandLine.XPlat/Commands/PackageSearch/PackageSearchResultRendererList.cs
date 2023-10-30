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
    internal class PackageSearchResultRendererList : IPackageSearchResultRenderer
    {
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);
        private PackageSearchArgs _args;

        public PackageSearchResultRendererList(PackageSearchArgs args)
        {
            _args = args;
        }

        public async Task Add(PackageSource source, Task<IEnumerable<IPackageSearchMetadata>> completedSearchTask)
        {
            _args.Logger.LogMinimal(SourceSeparator);

            if (completedSearchTask == null)
            {
                _args.Logger.LogMinimal($"Source: {source.Name}");
                _args.Logger.LogMinimal("Failed to obtain a search resource.");
                return;
            }

            _args.Logger.LogMinimal($"Source: {source.Name}");
            IEnumerable<IPackageSearchMetadata> searchResult = await completedSearchTask;

            if (_args.ExactMatch)
            {
                var firstResult = searchResult.FirstOrDefault();
                if (firstResult != null)
                {
                    PrintPackages(new[] { firstResult });
                }
            }
            else
            {
                PrintPackages(searchResult);
            }
        }

        private void PrintPackages(IEnumerable<IPackageSearchMetadata> packages)
        {
            CultureInfo culture = CultureInfo.CurrentCulture;

            if (packages == null || !packages.Any())
            {
                Console.WriteLine("No results found.");
                return;
            }

            foreach (var result in packages)
            {
                string packageId = result.Identity.Id;
                string version = result.Identity.Version.ToNormalizedString();
                string downloads = "N/A";

                if (result.DownloadCount != null)
                {
                    NumberFormatInfo nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
                    nfi.NumberDecimalDigits = 0;
                    downloads = string.Format(nfi, "{0:N}", result.DownloadCount);
                }

                _args.Logger.LogMinimal($">{packageId} | Latest Version: {version} | Downloads: {downloads}");
            }
        }

        public void Finish()
        {
            // We don' need to write anything at the end of the rendering for a tabular format
        }

        public void Start()
        {
            // We don' need to write anything at the beginning of the rendering for a tabular format
        }
    }
}
