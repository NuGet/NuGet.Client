// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Newtonsoft.Json;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultJsonPrinter : IPackageSearchResultRenderer
    {
        private ILoggerWithColor _logger;
        private const int LineSeparatorLength = 40;
        private static readonly string SourceSeparator = new('*', LineSeparatorLength);
        private PackageSearchVerbosity _verbosity;
        private List<PackageSearchResult> _packageSearchResults;

        public PackageSearchResultJsonPrinter(ILoggerWithColor loggerWithColor, PackageSearchVerbosity verbosity)
        {
            _logger = loggerWithColor;
            _verbosity = verbosity;
        }
        public async void Add(PackageSource source, IEnumerable<IPackageSearchMetadata> completedSearch)
        {
            PackageSearchResult packageSearchResult = new PackageSearchResult(source.Name);

            foreach (IPackageSearchMetadata metadata in completedSearch)
            {
                if (_verbosity == PackageSearchVerbosity.Minimal)
                {
                    packageSearchResult.AddPackage(new PackageSearchResultPackageMinimal(metadata));
                }
                else if (_verbosity == PackageSearchVerbosity.Detailed)
                {
                    PackageDeprecationMetadata packageDeprecationMetadata = await metadata.GetDeprecationMetadataAsync();
                    packageSearchResult.AddPackage(new PackageSearchResultPackageDetailed(metadata, packageDeprecationMetadata?.Message));
                }
                else
                {
                    packageSearchResult.AddPackage(new PackageSearchResultPackageNormal(metadata));
                }
            }

            _packageSearchResults.Add(packageSearchResult);
        }

        public void Add(PackageSource source, string error)
        {
            _logger.LogMinimal(SourceSeparator);
            _logger.LogMinimal($"Source: {source.Name} ({source.SourceUri})");
            _logger.LogError(error);
        }

        public void Finish()
        {
            var json = JsonConvert.SerializeObject(_packageSearchResults, Formatting.Indented);
            _logger.LogMinimal(SourceSeparator);
            _logger.LogMinimal("Search Result");
            _logger.LogMinimal(json);
        }

        public void Start()
        {
            _packageSearchResults = new List<PackageSearchResult>();
        }
    }
}
