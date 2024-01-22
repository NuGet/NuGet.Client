using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace NuGet.CommandLine.XPlat
{
    internal class PackageSearchResultJsonRenderer : IPackageSearchResultRenderer
    {
        private ILoggerWithColor _logger;
        private PackageSearchVerbosity _verbosity;
        private bool _exactMatch;
        private PackageSearchMainOutput _packageSearchMainOutput;

        public PackageSearchResultJsonRenderer(ILoggerWithColor loggerWithColor, PackageSearchVerbosity verbosity, bool exactMatch)
        {
            _logger = loggerWithColor;
            _verbosity = verbosity;
            _exactMatch = exactMatch;
        }
        public async void Add(PackageSource source, IEnumerable<IPackageSearchMetadata> completedSearch)
        {
            PackageSearchResult packageSearchResult = new PackageSearchResult(source.Name);

            foreach (IPackageSearchMetadata metadata in completedSearch)
            {
                string deprecation = "";

                if (_verbosity == PackageSearchVerbosity.Detailed)
                {
                    PackageDeprecationMetadata packageDeprecationMetadata = await metadata.GetDeprecationMetadataAsync();
                    deprecation = packageDeprecationMetadata.Message;
                }

                ISearchResultPackage package = JsonFormatFactorySearchResultPackage.GetPackage(metadata, _verbosity, _exactMatch, deprecation);
                packageSearchResult.Packages.Add(package);
            }

            _packageSearchMainOutput.SearchResult.Add(packageSearchResult);
        }

        public void Add(PackageSource source, PackageSearchProblem packageSearchProblem)
        {
            PackageSearchResult packageSearchResult = new PackageSearchResult(source.Name)
            {
                Problems = new List<PackageSearchProblem>() { packageSearchProblem }
            };
            _packageSearchMainOutput.SearchResult.Add(packageSearchResult);
        }

        public void Finish()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new SearchResultPackageConverter() }
            };
            var json = JsonSerializer.Serialize(_packageSearchMainOutput, options);
            _logger.LogMinimal(json);
        }

        public void RenderProblem(PackageSearchProblem packageSearchProblem)
        {
            _packageSearchMainOutput.Problems.Add(packageSearchProblem);
        }

        public void Start()
        {
            _packageSearchMainOutput = new PackageSearchMainOutput();
        }
    }

    class SearchResultPackageConverter : JsonConverter<ISearchResultPackage>
    {
        public override ISearchResultPackage Read(ref Utf8JsonReader reader, Type typeToSerialize, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, ISearchResultPackage value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, value.GetType(), options);
        }
    }
}
