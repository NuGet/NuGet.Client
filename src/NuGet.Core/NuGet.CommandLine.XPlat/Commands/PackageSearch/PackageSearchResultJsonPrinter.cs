// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.CommandLine.XPlat.Commands.PackageSearch;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultJsonPrinter : IPackageSearchResultRenderer
    {
        private ILoggerWithColor _logger;
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
            PackageSearchResult packageSearchResult = new PackageSearchResult(source.Name);
            packageSearchResult.Errors = new List<string>() { error };
            _packageSearchResults.Add(packageSearchResult);
        }

        public void Finish()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new JsonPolymorphicConverter<IPackageSearchResultPackage>() }
            };
            var json = JsonSerializer.Serialize(_packageSearchResults, options);
            _logger.LogMinimal(json);
        }

        public void Start()
        {
            _packageSearchResults = new List<PackageSearchResult>();
        }
    }

    class JsonPolymorphicConverter<T> : JsonConverter<T>
    {
        public override bool CanConvert(Type typeToConvert) => typeof(T).IsAssignableFrom(typeToConvert);

        public override T Read(ref Utf8JsonReader reader, Type typeToSerialize, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            Type type = value.GetType();

            JsonSerializer.Serialize(writer, value, type, new JsonSerializerOptions());
        }
    }
}
