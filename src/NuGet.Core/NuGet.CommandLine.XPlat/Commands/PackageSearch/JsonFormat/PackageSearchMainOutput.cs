// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NuGet.CommandLine.XPlat
{
    internal class SearchMainOutput
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("problems")]
        public List<PackageSearchProblem> Problems { get; set; }

        [JsonPropertyName("searchResult")]
        public List<PackageSearchResult> SearchResult { get; set; }

        public SearchMainOutput()
        {
            Version = 2;
            Problems = new List<PackageSearchProblem>();
            SearchResult = new List<PackageSearchResult>();
        }
    }
}
