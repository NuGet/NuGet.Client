using System;

namespace PowerShellGet.Tests
{
    public static class JsonData
    {
        #region index.json

        public const string IndexJson = @"{
 ""version"": ""3.0.0-beta.1"",
 ""resources"": [
  {
   ""@id"": ""https://api-v3search-0.nuget.org/query"",
   ""@type"": ""SearchQueryService""
  },
  {
   ""@id"": ""https://api-v3search-0.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService""
  },
  {
   ""@id"": ""https://api-v3search-1.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService""
  },
  {
   ""@id"": ""https://api-search.nuget.org/"",
   ""@type"": ""SearchGalleryQueryService""
  },  
  {
   ""@id"": ""https://api-metrics.nuget.org/DownloadEvent"",
   ""@type"": ""MetricsService""
  },
  {
   ""@id"": ""https://api.nuget.org/v3/registration0/"",
   ""@type"": ""RegistrationsBaseUrl""
  },
  {
   ""@id"": ""https://api.nuget.org/v2"",
   ""@type"": ""LegacyGallery""
  },
  {
   ""@id"": ""https://api.nuget.org/v2"",
   ""@type"": ""LegacyGallery/2.0.0""
  },
  {
   ""@id"": ""https://api-v3search-0.nuget.org/query"",
   ""@type"": ""SearchQueryService/3.0.0-rc""
  },
  {
   ""@id"": ""https://api-v3search-1.nuget.org/query"",
   ""@type"": ""SearchQueryService/3.0.0-rc""
  },
  {
   ""@id"": ""https://api-v3search-0.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService/3.0.0-rc""
  },
  {
   ""@id"": ""https://api-v3search-1.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService/3.0.0-rc""
  },
  {
   ""@id"": ""https://api-search.nuget.org/"",
   ""@type"": ""SearchGalleryQueryService/3.0.0-rc""
  },  
  {
   ""@id"": ""https://api-metrics.nuget.org/DownloadEvent"",
   ""@type"": ""MetricsService/3.0.0-rc""
  },
  {
   ""@id"": ""https://api.nuget.org/v3/registration0/"",
   ""@type"": ""RegistrationsBaseUrl/3.0.0-rc""
  },
  {
   ""@id"": ""https://www.nuget.org/packages/{id}/{version}/ReportAbuse"",
   ""@type"": ""ReportAbuseUriTemplate/3.0.0-rc""
  },
  {
   ""@id"": ""https://api.nuget.org/v3/registration0/{id-lower}/index.json"",
   ""@type"": ""PackageDisplayMetadataUriTemplate/3.0.0-rc""
  },
  {
   ""@id"": ""https://api.nuget.org/v3/registration0/{id-lower}/{version-lower}.json"",
   ""@type"": ""PackageVersionDisplayMetadataUriTemplate/3.0.0-rc""
  },
  {
   ""@id"": ""https://api-v3search-0.nuget.org/query"",
   ""@type"": ""SearchQueryService/3.0.0-beta""
  },
  {
   ""@id"": ""https://api-v3search-1.nuget.org/query"",
   ""@type"": ""SearchQueryService/3.0.0-beta""
  },
  {
   ""@id"": ""https://api-v3search-0.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService/3.0.0-beta""
  },
  {
   ""@id"": ""https://api-v3search-1.nuget.org/autocomplete"",
   ""@type"": ""SearchAutocompleteService/3.0.0-beta""
  },
  {
   ""@id"": ""https://api-search.nuget.org/"",
   ""@type"": ""SearchGalleryQueryService/3.0.0-beta""
  },  
  {
   ""@id"": ""https://api-metrics.nuget.org/DownloadEvent"",
   ""@type"": ""MetricsService/3.0.0-beta""
  },
  {
   ""@id"": ""https://api.nuget.org/v3/registration0/"",
   ""@type"": ""RegistrationsBaseUrl/3.0.0-beta""
  },
  {
   ""@id"": ""https://www.nuget.org/packages/{id}/{version}/ReportAbuse"",
   ""@type"": ""ReportAbuseUriTemplate/3.0.0-beta""
  },
  {
    ""@id"": ""https://api.nuget.org/v3/stats0/totals.json"",
    ""@type"": ""TotalStats/3.0.0-rc""
  }
 ],
 ""@context"": {
  ""@vocab"": ""https://schema.nuget.org/services#""
 }
}";
        #endregion

        #region Non-PSMetadata search page

        public const string NonPSMetadata = @"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 31003,
  ""lastReopen"": ""2015-03-04T22:25:44.3797399Z"",
  ""index"": ""v3-lucene0"",
  ""data"": [
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.2.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/entityframework/index.json"",
      ""id"": ""EntityFramework"",
      ""description"": ""Entity Framework is Microsoft's recommended data access technology for new applications."",
      ""summary"": ""Entity Framework is Microsoft's recommended data access technology for new applications."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=386613"",
      ""tags"": [
        ""Microsoft"",
        ""EF"",
        ""Database"",
        ""Data"",
        ""O/RM"",
        ""ADO.NET""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""6.1.2"",
      ""versions"": [
        {
          ""version"": ""6.1.3-beta1"",
          ""downloads"": 8155,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.3-beta1.json""
        },
        {
          ""version"": ""6.1.2"",
          ""downloads"": 412702,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.2.json""
        },
        {
          ""version"": ""6.1.2-beta2"",
          ""downloads"": 24520,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.2-beta2.json""
        },
        {
          ""version"": ""6.1.2-beta1"",
          ""downloads"": 38498,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.2-beta1.json""
        },
        {
          ""version"": ""6.1.1"",
          ""downloads"": 1013076,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.1.json""
        },
        {
          ""version"": ""6.1.1-beta1"",
          ""downloads"": 27420,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.1-beta1.json""
        },
        {
          ""version"": ""6.1.0"",
          ""downloads"": 752732,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.0.json""
        },
        {
          ""version"": ""6.1.0-beta1"",
          ""downloads"": 66051,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.0-beta1.json""
        },
        {
          ""version"": ""6.1.0-alpha1"",
          ""downloads"": 36121,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.1.0-alpha1.json""
        },
        {
          ""version"": ""6.0.2"",
          ""downloads"": 1250514,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.2.json""
        },
        {
          ""version"": ""6.0.2-beta1"",
          ""downloads"": 34970,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.2-beta1.json""
        },
        {
          ""version"": ""6.0.1"",
          ""downloads"": 736781,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.1.json""
        },
        {
          ""version"": ""6.0.0"",
          ""downloads"": 596498,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0.json""
        },
        {
          ""version"": ""6.0.0-rc1"",
          ""downloads"": 60234,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0-rc1.json""
        },
        {
          ""version"": ""6.0.0-beta1"",
          ""downloads"": 60277,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0-beta1.json""
        },
        {
          ""version"": ""6.0.0-alpha3"",
          ""downloads"": 47266,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha3.json""
        },
        {
          ""version"": ""6.0.0-alpha2"",
          ""downloads"": 40644,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha2.json""
        },
        {
          ""version"": ""6.0.0-alpha1"",
          ""downloads"": 26314,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 2249757,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc"",
          ""downloads"": 97963,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/5.0.0-rc.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 14370,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 4634,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/5.0.0-beta1.json""
        },
        {
          ""version"": ""4.3.1"",
          ""downloads"": 391006,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.3.1.json""
        },
        {
          ""version"": ""4.3.0"",
          ""downloads"": 67561,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.3.0.json""
        },
        {
          ""version"": ""4.3.0-beta1"",
          ""downloads"": 3519,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.3.0-beta1.json""
        },
        {
          ""version"": ""4.2.0"",
          ""downloads"": 331293,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.2.0.json""
        },
        {
          ""version"": ""4.1.10715"",
          ""downloads"": 606226,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.1.10715.json""
        },
        {
          ""version"": ""4.1.10331"",
          ""downloads"": 341607,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.1.10331.json""
        },
        {
          ""version"": ""4.1.10311"",
          ""downloads"": 31291,
          ""@id"": ""https://api.nuget.org/v3/registration0/entityframework/4.1.10311.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.8.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/index.json"",
      ""id"": ""Newtonsoft.Json"",
      ""description"": ""Json.NET is a popular high-performance JSON framework for .NET"",
      ""tags"": [
        ""json""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""6.0.8"",
      ""versions"": [
        {
          ""version"": ""7.0.1-beta1"",
          ""downloads"": 6082,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/7.0.1-beta1.json""
        },
        {
          ""version"": ""6.0.8"",
          ""downloads"": 329683,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.8.json""
        },
        {
          ""version"": ""6.0.7"",
          ""downloads"": 182745,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.7.json""
        },
        {
          ""version"": ""6.0.6"",
          ""downloads"": 566067,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.6.json""
        },
        {
          ""version"": ""6.0.5"",
          ""downloads"": 545022,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.5.json""
        },
        {
          ""version"": ""6.0.4"",
          ""downloads"": 656641,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.4.json""
        },
        {
          ""version"": ""6.0.3"",
          ""downloads"": 782040,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.3.json""
        },
        {
          ""version"": ""6.0.2"",
          ""downloads"": 386345,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.2.json""
        },
        {
          ""version"": ""6.0.1"",
          ""downloads"": 644076,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/6.0.1.json""
        },
        {
          ""version"": ""5.0.8"",
          ""downloads"": 1171639,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.8.json""
        },
        {
          ""version"": ""5.0.7"",
          ""downloads"": 265537,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.7.json""
        },
        {
          ""version"": ""5.0.6"",
          ""downloads"": 1163950,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.6.json""
        },
        {
          ""version"": ""5.0.5"",
          ""downloads"": 317640,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.5.json""
        },
        {
          ""version"": ""5.0.4"",
          ""downloads"": 342685,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.4.json""
        },
        {
          ""version"": ""5.0.3"",
          ""downloads"": 180526,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.3.json""
        },
        {
          ""version"": ""5.0.2"",
          ""downloads"": 164655,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.2.json""
        },
        {
          ""version"": ""5.0.1"",
          ""downloads"": 180945,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/5.0.1.json""
        },
        {
          ""version"": ""4.5.11"",
          ""downloads"": 1582739,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.11.json""
        },
        {
          ""version"": ""4.5.10"",
          ""downloads"": 229290,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.10.json""
        },
        {
          ""version"": ""4.5.9"",
          ""downloads"": 135499,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.9.json""
        },
        {
          ""version"": ""4.5.8"",
          ""downloads"": 176784,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.8.json""
        },
        {
          ""version"": ""4.5.7"",
          ""downloads"": 184111,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.7.json""
        },
        {
          ""version"": ""4.5.6"",
          ""downloads"": 523505,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.6.json""
        },
        {
          ""version"": ""4.5.5"",
          ""downloads"": 55013,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.5.json""
        },
        {
          ""version"": ""4.5.4"",
          ""downloads"": 31402,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.4.json""
        },
        {
          ""version"": ""4.5.3"",
          ""downloads"": 19832,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.3.json""
        },
        {
          ""version"": ""4.5.2"",
          ""downloads"": 8543,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.2.json""
        },
        {
          ""version"": ""4.5.1"",
          ""downloads"": 116767,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.5.1.json""
        },
        {
          ""version"": ""4.0.8"",
          ""downloads"": 197258,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.8.json""
        },
        {
          ""version"": ""4.0.7"",
          ""downloads"": 252247,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.7.json""
        },
        {
          ""version"": ""4.0.6"",
          ""downloads"": 10688,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.6.json""
        },
        {
          ""version"": ""4.0.5"",
          ""downloads"": 54614,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.5.json""
        },
        {
          ""version"": ""4.0.4"",
          ""downloads"": 24293,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.4.json""
        },
        {
          ""version"": ""4.0.3"",
          ""downloads"": 25216,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.3.json""
        },
        {
          ""version"": ""4.0.2"",
          ""downloads"": 45546,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.2.json""
        },
        {
          ""version"": ""4.0.1"",
          ""downloads"": 9664,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/4.0.1.json""
        },
        {
          ""version"": ""3.5.8"",
          ""downloads"": 10539,
          ""@id"": ""https://api.nuget.org/v3/registration0/newtonsoft.json/3.5.8.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.1.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/jquery/index.json"",
      ""id"": ""jQuery"",
      ""description"": ""jQuery is a new kind of JavaScript Library.\n        jQuery is a fast and concise JavaScript Library that simplifies HTML document traversing, event handling, animating, and Ajax interactions for rapid web development. jQuery is designed to change the way that you write JavaScript.\n        NOTE: This package is maintained on behalf of the library owners by the NuGet Community Packages project at http://nugetpackages.codeplex.com/"",
      ""tags"": [
        ""jQuery""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""2.1.3"",
      ""versions"": [
        {
          ""version"": ""2.1.3"",
          ""downloads"": 189280,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.1.3.json""
        },
        {
          ""version"": ""2.1.2"",
          ""downloads"": 60852,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.1.2.json""
        },
        {
          ""version"": ""2.1.1"",
          ""downloads"": 669638,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.1.1.json""
        },
        {
          ""version"": ""2.1.0"",
          ""downloads"": 530216,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.1.0.json""
        },
        {
          ""version"": ""2.0.3"",
          ""downloads"": 769507,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.0.3.json""
        },
        {
          ""version"": ""2.0.2"",
          ""downloads"": 344117,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.0.2.json""
        },
        {
          ""version"": ""2.0.1.1"",
          ""downloads"": 280753,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.0.1.1.json""
        },
        {
          ""version"": ""2.0.1"",
          ""downloads"": 249174,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.0.1.json""
        },
        {
          ""version"": ""2.0.0"",
          ""downloads"": 344464,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/2.0.0.json""
        },
        {
          ""version"": ""1.11.2"",
          ""downloads"": 52198,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.11.2.json""
        },
        {
          ""version"": ""1.11.1"",
          ""downloads"": 198755,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.11.1.json""
        },
        {
          ""version"": ""1.11.0"",
          ""downloads"": 235109,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.11.0.json""
        },
        {
          ""version"": ""1.10.2"",
          ""downloads"": 684594,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.10.2.json""
        },
        {
          ""version"": ""1.10.1"",
          ""downloads"": 175738,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.10.1.json""
        },
        {
          ""version"": ""1.10.0.1"",
          ""downloads"": 153194,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.10.0.1.json""
        },
        {
          ""version"": ""1.10.0"",
          ""downloads"": 238329,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.10.0.json""
        },
        {
          ""version"": ""1.9.1"",
          ""downloads"": 875708,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.9.1.json""
        },
        {
          ""version"": ""1.9.0"",
          ""downloads"": 346556,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.9.0.json""
        },
        {
          ""version"": ""1.8.3"",
          ""downloads"": 401859,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.8.3.json""
        },
        {
          ""version"": ""1.8.2"",
          ""downloads"": 628369,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.8.2.json""
        },
        {
          ""version"": ""1.8.1"",
          ""downloads"": 95615,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.8.1.json""
        },
        {
          ""version"": ""1.8.0"",
          ""downloads"": 147460,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.8.0.json""
        },
        {
          ""version"": ""1.7.2"",
          ""downloads"": 289635,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.7.2.json""
        },
        {
          ""version"": ""1.7.1.1"",
          ""downloads"": 635424,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.7.1.1.json""
        },
        {
          ""version"": ""1.7.1"",
          ""downloads"": 187451,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.7.1.json""
        },
        {
          ""version"": ""1.7.0"",
          ""downloads"": 323711,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.7.0.json""
        },
        {
          ""version"": ""1.6.4"",
          ""downloads"": 415267,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.6.4.json""
        },
        {
          ""version"": ""1.6.3"",
          ""downloads"": 24087,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.6.3.json""
        },
        {
          ""version"": ""1.6.2"",
          ""downloads"": 753216,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.6.2.json""
        },
        {
          ""version"": ""1.6.1"",
          ""downloads"": 25053,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.6.1.json""
        },
        {
          ""version"": ""1.6.0"",
          ""downloads"": 18138,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.6.0.json""
        },
        {
          ""version"": ""1.5.2"",
          ""downloads"": 39923,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.5.2.json""
        },
        {
          ""version"": ""1.5.1"",
          ""downloads"": 247051,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.5.1.json""
        },
        {
          ""version"": ""1.5.0"",
          ""downloads"": 5499,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.5.0.json""
        },
        {
          ""version"": ""1.4.4"",
          ""downloads"": 210121,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.4.4.json""
        },
        {
          ""version"": ""1.4.3"",
          ""downloads"": 1476,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.4.3.json""
        },
        {
          ""version"": ""1.4.2"",
          ""downloads"": 2889,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.4.2.json""
        },
        {
          ""version"": ""1.4.1"",
          ""downloads"": 35242,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery/1.4.1.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/index.json"",
      ""id"": ""Microsoft.AspNet.Mvc"",
      ""description"": ""This package contains the runtime assemblies for ASP.NET MVC. ASP.NET MVC gives you a powerful, patterns-based way to build dynamic websites that enables a clean separation of concerns and that gives you full control over markup."",
      ""summary"": ""This package contains the runtime assemblies for ASP.NET MVC."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""Mvc"",
        ""AspNetMvc""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""5.2.3"",
      ""versions"": [
        {
          ""version"": ""6.0.0-beta3"",
          ""downloads"": 3693,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/6.0.0-beta3.json""
        },
        {
          ""version"": ""6.0.0-beta2"",
          ""downloads"": 14665,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/6.0.0-beta2.json""
        },
        {
          ""version"": ""6.0.0-beta1"",
          ""downloads"": 39688,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/6.0.0-beta1.json""
        },
        {
          ""version"": ""5.2.3"",
          ""downloads"": 86292,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.3.json""
        },
        {
          ""version"": ""5.2.3-beta1"",
          ""downloads"": 10341,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.3-beta1.json""
        },
        {
          ""version"": ""5.2.2"",
          ""downloads"": 724782,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.2.json""
        },
        {
          ""version"": ""5.2.2-rc"",
          ""downloads"": 11694,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.2-rc.json""
        },
        {
          ""version"": ""5.2.0"",
          ""downloads"": 488847,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.0.json""
        },
        {
          ""version"": ""5.2.0-rc"",
          ""downloads"": 24527,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.2.0-rc.json""
        },
        {
          ""version"": ""5.1.3"",
          ""downloads"": 80487,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.1.3.json""
        },
        {
          ""version"": ""5.1.2"",
          ""downloads"": 753563,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.1.2.json""
        },
        {
          ""version"": ""5.1.1"",
          ""downloads"": 554190,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.1.1.json""
        },
        {
          ""version"": ""5.1.0"",
          ""downloads"": 328342,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.1.0.json""
        },
        {
          ""version"": ""5.1.0-rc1"",
          ""downloads"": 20190,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.1.0-rc1.json""
        },
        {
          ""version"": ""5.0.2"",
          ""downloads"": 64398,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.2.json""
        },
        {
          ""version"": ""5.0.1"",
          ""downloads"": 165651,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 927999,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc1"",
          ""downloads"": 27692,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.0-rc1.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 26100,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 19675,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/5.0.0-beta1.json""
        },
        {
          ""version"": ""4.0.40804"",
          ""downloads"": 121753,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/4.0.40804.json""
        },
        {
          ""version"": ""4.0.30506"",
          ""downloads"": 786958,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/4.0.30506.json""
        },
        {
          ""version"": ""4.0.20710"",
          ""downloads"": 1261588,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/4.0.20710.json""
        },
        {
          ""version"": ""4.0.20505"",
          ""downloads"": 122440,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/4.0.20505.json""
        },
        {
          ""version"": ""3.0.50813.1"",
          ""downloads"": 45734,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/3.0.50813.1.json""
        },
        {
          ""version"": ""3.0.20105.1"",
          ""downloads"": 84838,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.mvc/3.0.20105.1.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.3.2.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/bootstrap/index.json"",
      ""id"": ""bootstrap"",
      ""description"": ""Sleek, intuitive, and powerful mobile first front-end framework for faster and easier web development."",
      ""iconUrl"": ""https://github.com/twbs/bootstrap/blob/master/docs-assets/ico/apple-touch-icon-144-precomposed.png"",
      ""tags"": [
        ""bootstrap"",
        ""html"",
        ""css"",
        ""javascript"",
        ""web""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.3.2"",
      ""versions"": [
        {
          ""version"": ""3.3.2"",
          ""downloads"": 45429,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.3.2.json""
        },
        {
          ""version"": ""3.3.1"",
          ""downloads"": 142662,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.3.1.json""
        },
        {
          ""version"": ""3.3.0"",
          ""downloads"": 47793,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.3.0.json""
        },
        {
          ""version"": ""3.2.0"",
          ""downloads"": 262461,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.2.0.json""
        },
        {
          ""version"": ""3.1.1"",
          ""downloads"": 267636,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.1.1.json""
        },
        {
          ""version"": ""3.1.0"",
          ""downloads"": 43076,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.1.0.json""
        },
        {
          ""version"": ""3.0.3"",
          ""downloads"": 149307,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.0.3.json""
        },
        {
          ""version"": ""3.0.2"",
          ""downloads"": 54527,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.0.2.json""
        },
        {
          ""version"": ""3.0.1"",
          ""downloads"": 72605,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.0.1.json""
        },
        {
          ""version"": ""3.0.0"",
          ""downloads"": 491141,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/3.0.0.json""
        },
        {
          ""version"": ""2.3.2"",
          ""downloads"": 17261,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/2.3.2.json""
        },
        {
          ""version"": ""2.3.1"",
          ""downloads"": 30142,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/2.3.1.json""
        },
        {
          ""version"": ""1.0.0"",
          ""downloads"": 4116,
          ""@id"": ""https://api.nuget.org/v3/registration0/bootstrap/1.0.0.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.6.0.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/webgrease/index.json"",
      ""id"": ""WebGrease"",
      ""description"": ""Web Grease is a suite of tools for optimizing javascript, css files and images."",
      ""tags"": [
        """"
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""1.6.0"",
      ""versions"": [
        {
          ""version"": ""1.6.0"",
          ""downloads"": 1477173,
          ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.6.0.json""
        },
        {
          ""version"": ""1.5.2"",
          ""downloads"": 1165047,
          ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.5.2.json""
        },
        {
          ""version"": ""1.3.0"",
          ""downloads"": 803567,
          ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.3.0.json""
        },
        {
          ""version"": ""1.1.0"",
          ""downloads"": 794100,
          ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.1.0.json""
        },
        {
          ""version"": ""1.0.0"",
          ""downloads"": 76195,
          ""@id"": ""https://api.nuget.org/v3/registration0/webgrease/1.0.0.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/index.json"",
      ""id"": ""Microsoft.AspNet.WebApi"",
      ""description"": ""This package contains everything you need to host ASP.NET Web API on IIS. ASP.NET Web API is a framework that makes it easy to build HTTP services that reach a broad range of clients, including browsers and mobile devices. ASP.NET Web API is an ideal platform for building RESTful applications on the .NET Framework."",
      ""summary"": ""This package contains everything you need to host ASP.NET Web API on IIS."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""WebApi"",
        ""AspNetWebApi""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""5.2.3"",
      ""versions"": [
        {
          ""version"": ""5.2.3"",
          ""downloads"": 55594,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.3.json""
        },
        {
          ""version"": ""5.2.3-beta1"",
          ""downloads"": 7367,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.3-beta1.json""
        },
        {
          ""version"": ""5.2.2"",
          ""downloads"": 404671,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.2.json""
        },
        {
          ""version"": ""5.2.2-rc"",
          ""downloads"": 8498,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.2-rc.json""
        },
        {
          ""version"": ""5.2.0"",
          ""downloads"": 283777,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.0.json""
        },
        {
          ""version"": ""5.2.0-rc"",
          ""downloads"": 15597,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.2.0-rc.json""
        },
        {
          ""version"": ""5.1.2"",
          ""downloads"": 362276,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.1.2.json""
        },
        {
          ""version"": ""5.1.1"",
          ""downloads"": 275146,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.1.1.json""
        },
        {
          ""version"": ""5.1.0"",
          ""downloads"": 175877,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.1.0.json""
        },
        {
          ""version"": ""5.1.0-rc1"",
          ""downloads"": 18079,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.1.0-rc1.json""
        },
        {
          ""version"": ""5.0.1"",
          ""downloads"": 128872,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.0.1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 671013,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc1"",
          ""downloads"": 26482,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.0.0-rc1.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 21842,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 13379,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/5.0.0-beta1.json""
        },
        {
          ""version"": ""4.0.30506"",
          ""downloads"": 578484,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/4.0.30506.json""
        },
        {
          ""version"": ""4.0.20710"",
          ""downloads"": 1159046,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/4.0.20710.json""
        },
        {
          ""version"": ""4.0.20505"",
          ""downloads"": 107481,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi/4.0.20505.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.4.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/nunit/index.json"",
      ""id"": ""NUnit"",
      ""description"": ""NUnit features a fluent assert syntax, parameterized, generic and theory tests and is user-extensible. A number of runners, both from the NUnit project and by third parties, are able to execute NUnit tests.\n\nVersion 2.6 is the seventh major release of this well-known and well-tested programming tool.\n\nThis package includes only the framework assembly. You will need to install the NUnit.Runners package unless you are using a third-party runner."",
      ""summary"": ""NUnit is a unit-testing framework for all .Net languages with a strong TDD focus."",
      ""iconUrl"": ""http://nunit.org/nuget/nunit_32x32.png"",
      ""tags"": [
        ""nunit"",
        ""test"",
        ""testing"",
        ""tdd"",
        ""framework"",
        ""fluent"",
        ""assert"",
        ""theory"",
        ""plugin"",
        ""addin""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""2.6.4"",
      ""versions"": [
        {
          ""version"": ""3.0.0-alpha-5"",
          ""downloads"": 1797,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/3.0.0-alpha-5.json""
        },
        {
          ""version"": ""3.0.0-alpha-4"",
          ""downloads"": 1693,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/3.0.0-alpha-4.json""
        },
        {
          ""version"": ""3.0.0-alpha-3"",
          ""downloads"": 1425,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/3.0.0-alpha-3.json""
        },
        {
          ""version"": ""3.0.0-alpha-2"",
          ""downloads"": 1664,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/3.0.0-alpha-2.json""
        },
        {
          ""version"": ""3.0.0-alpha"",
          ""downloads"": 3160,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/3.0.0-alpha.json""
        },
        {
          ""version"": ""2.6.4"",
          ""downloads"": 104186,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.4.json""
        },
        {
          ""version"": ""2.6.3"",
          ""downloads"": 702268,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.3.json""
        },
        {
          ""version"": ""2.6.2"",
          ""downloads"": 539108,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.2.json""
        },
        {
          ""version"": ""2.6.1"",
          ""downloads"": 130822,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.1.json""
        },
        {
          ""version"": ""2.6.0.12054"",
          ""downloads"": 184913,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.6.0.12054.json""
        },
        {
          ""version"": ""2.5.10.11092"",
          ""downloads"": 186128,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.5.10.11092.json""
        },
        {
          ""version"": ""2.5.9.10348"",
          ""downloads"": 19993,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.5.9.10348.json""
        },
        {
          ""version"": ""2.5.7.10213"",
          ""downloads"": 14817,
          ""@id"": ""https://api.nuget.org/v3/registration0/nunit/2.5.7.10213.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.29.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/index.json"",
      ""id"": ""Microsoft.Net.Http"",
      ""description"": ""This package includes HttpClient for sending requests over HTTP, as well as HttpRequestMessage and HttpResponseMessage for processing HTTP messages.\n\nThis package is not supported in Visual Studio 2010, and is only required for projects targeting .NET Framework 4.5, Windows 8, or Windows Phone 8.1 when consuming a library that uses this package.\n\nSupported Platforms:\n- .NET Framework 4\n- Windows 8\n- Windows Phone 8.1\n- Windows Phone Silverlight 7.5\n- Silverlight 4\n- Portable Class Libraries"",
      ""summary"": ""This package provides a programming interface for modern HTTP/REST based applications."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""BCL"",
        ""HTTP"",
        ""HttpClient"",
        ""REST"",
        ""Microsoft"",
        ""System"",
        ""Networking""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""2.2.29"",
      ""versions"": [
        {
          ""version"": ""2.2.29"",
          ""downloads"": 13943,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.29.json""
        },
        {
          ""version"": ""2.2.28"",
          ""downloads"": 404506,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.28.json""
        },
        {
          ""version"": ""2.2.27-beta"",
          ""downloads"": 18098,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.27-beta.json""
        },
        {
          ""version"": ""2.2.23-beta"",
          ""downloads"": 11578,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.23-beta.json""
        },
        {
          ""version"": ""2.2.22"",
          ""downloads"": 458710,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.22.json""
        },
        {
          ""version"": ""2.2.20"",
          ""downloads"": 193598,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.20.json""
        },
        {
          ""version"": ""2.2.19"",
          ""downloads"": 160758,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.19.json""
        },
        {
          ""version"": ""2.2.18"",
          ""downloads"": 920006,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.18.json""
        },
        {
          ""version"": ""2.2.15"",
          ""downloads"": 320596,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.15.json""
        },
        {
          ""version"": ""2.2.13"",
          ""downloads"": 323289,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.13.json""
        },
        {
          ""version"": ""2.2.10-rc"",
          ""downloads"": 13448,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.10-rc.json""
        },
        {
          ""version"": ""2.2.7-beta"",
          ""downloads"": 14956,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.7-beta.json""
        },
        {
          ""version"": ""2.2.3-beta"",
          ""downloads"": 15740,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.2.3-beta.json""
        },
        {
          ""version"": ""2.1.10"",
          ""downloads"": 286546,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.1.10.json""
        },
        {
          ""version"": ""2.1.6-rc"",
          ""downloads"": 16877,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.1.6-rc.json""
        },
        {
          ""version"": ""2.1.3-beta"",
          ""downloads"": 30206,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.1.3-beta.json""
        },
        {
          ""version"": ""2.0.20710"",
          ""downloads"": 1582320,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.0.20710.json""
        },
        {
          ""version"": ""2.0.20505"",
          ""downloads"": 115073,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.net.http/2.0.20505.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.14.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/angularjs/index.json"",
      ""id"": ""angularjs"",
      ""description"": ""AngularJS. HTML enhanced for web apps!"",
      ""iconUrl"": ""https://secure.gravatar.com/avatar/6e1b5ab3ef1593413f1bee4e5a6e6ae7?s=140&d=https://a248.e.akamai.net/assets.github.com%2Fimages%2Fgravatars%2Fgravatar-140.png"",
      ""tags"": [
        ""angular"",
        ""angularjs"",
        ""SPA""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""1.3.14"",
      ""versions"": [
        {
          ""version"": ""1.4.0-beta5"",
          ""downloads"": 139,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.4.0-beta5.json""
        },
        {
          ""version"": ""1.4.0-beta4"",
          ""downloads"": 514,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.4.0-beta4.json""
        },
        {
          ""version"": ""1.4.0-beta3"",
          ""downloads"": 168,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.4.0-beta3.json""
        },
        {
          ""version"": ""1.4.0-beta1"",
          ""downloads"": 452,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.4.0-beta1.json""
        },
        {
          ""version"": ""1.3.14"",
          ""downloads"": 2795,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.14.json""
        },
        {
          ""version"": ""1.3.13"",
          ""downloads"": 10267,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.13.json""
        },
        {
          ""version"": ""1.3.12"",
          ""downloads"": 3869,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.12.json""
        },
        {
          ""version"": ""1.3.11"",
          ""downloads"": 50,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.11.json""
        },
        {
          ""version"": ""1.3.10"",
          ""downloads"": 8834,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.10.json""
        },
        {
          ""version"": ""1.3.9"",
          ""downloads"": 3322,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.9.json""
        },
        {
          ""version"": ""1.3.8"",
          ""downloads"": 15314,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.8.json""
        },
        {
          ""version"": ""1.3.7"",
          ""downloads"": 3053,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.7.json""
        },
        {
          ""version"": ""1.3.6"",
          ""downloads"": 4826,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.6.json""
        },
        {
          ""version"": ""1.3.5"",
          ""downloads"": 4676,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.5.json""
        },
        {
          ""version"": ""1.3.4"",
          ""downloads"": 5012,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.4.json""
        },
        {
          ""version"": ""1.3.3"",
          ""downloads"": 5119,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.3.json""
        },
        {
          ""version"": ""1.3.2"",
          ""downloads"": 7876,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.2.json""
        },
        {
          ""version"": ""1.3.1"",
          ""downloads"": 7053,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.1.json""
        },
        {
          ""version"": ""1.3.0"",
          ""downloads"": 11289,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0.json""
        },
        {
          ""version"": ""1.3.0-rc5"",
          ""downloads"": 45,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc5.json""
        },
        {
          ""version"": ""1.3.0-rc4"",
          ""downloads"": 663,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc4.json""
        },
        {
          ""version"": ""1.3.0-rc3"",
          ""downloads"": 241,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc3.json""
        },
        {
          ""version"": ""1.3.0-rc2"",
          ""downloads"": 392,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc2.json""
        },
        {
          ""version"": ""1.3.0-rc1"",
          ""downloads"": 255,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc1.json""
        },
        {
          ""version"": ""1.3.0-rc0"",
          ""downloads"": 905,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-rc0.json""
        },
        {
          ""version"": ""1.3.0-beta9"",
          ""downloads"": 54,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta9.json""
        },
        {
          ""version"": ""1.3.0-beta8"",
          ""downloads"": 379,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta8.json""
        },
        {
          ""version"": ""1.3.0-beta7"",
          ""downloads"": 404,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta7.json""
        },
        {
          ""version"": ""1.3.0-beta6"",
          ""downloads"": 2104,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta6.json""
        },
        {
          ""version"": ""1.3.0-beta5"",
          ""downloads"": 483,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta5.json""
        },
        {
          ""version"": ""1.3.0-beta4"",
          ""downloads"": 228,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta4.json""
        },
        {
          ""version"": ""1.3.0-beta3"",
          ""downloads"": 576,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta3.json""
        },
        {
          ""version"": ""1.3.0-beta2"",
          ""downloads"": 126,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta2.json""
        },
        {
          ""version"": ""1.3.0-beta1"",
          ""downloads"": 209,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.3.0-beta1.json""
        },
        {
          ""version"": ""1.2.28"",
          ""downloads"": 665,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.28.json""
        },
        {
          ""version"": ""1.2.27"",
          ""downloads"": 586,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.27.json""
        },
        {
          ""version"": ""1.2.26"",
          ""downloads"": 10716,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.26.json""
        },
        {
          ""version"": ""1.2.25"",
          ""downloads"": 9624,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.25.json""
        },
        {
          ""version"": ""1.2.24"",
          ""downloads"": 3966,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.24.json""
        },
        {
          ""version"": ""1.2.23"",
          ""downloads"": 14519,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.23.json""
        },
        {
          ""version"": ""1.2.22"",
          ""downloads"": 6683,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.22.json""
        },
        {
          ""version"": ""1.2.21"",
          ""downloads"": 10910,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.21.json""
        },
        {
          ""version"": ""1.2.20"",
          ""downloads"": 7145,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.20.json""
        },
        {
          ""version"": ""1.2.19"",
          ""downloads"": 8624,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.19.json""
        },
        {
          ""version"": ""1.2.18"",
          ""downloads"": 12583,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.18.json""
        },
        {
          ""version"": ""1.2.17"",
          ""downloads"": 7877,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.17.json""
        },
        {
          ""version"": ""1.2.16"",
          ""downloads"": 46625,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.16.json""
        },
        {
          ""version"": ""1.2.15"",
          ""downloads"": 8080,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.15.json""
        },
        {
          ""version"": ""1.2.14"",
          ""downloads"": 19898,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.14.json""
        },
        {
          ""version"": ""1.2.13"",
          ""downloads"": 7342,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.13.json""
        },
        {
          ""version"": ""1.2.12"",
          ""downloads"": 660,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.12.json""
        },
        {
          ""version"": ""1.2.11"",
          ""downloads"": 139,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.11.json""
        },
        {
          ""version"": ""1.2.10"",
          ""downloads"": 182,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.10.json""
        },
        {
          ""version"": ""1.2.9"",
          ""downloads"": 383,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.9.json""
        },
        {
          ""version"": ""1.2.8"",
          ""downloads"": 163,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.8.json""
        },
        {
          ""version"": ""1.2.7"",
          ""downloads"": 152,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.7.json""
        },
        {
          ""version"": ""1.2.6.1"",
          ""downloads"": 162,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.6.1.json""
        },
        {
          ""version"": ""1.2.5"",
          ""downloads"": 163,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.5.json""
        },
        {
          ""version"": ""1.2.4"",
          ""downloads"": 160,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.4.json""
        },
        {
          ""version"": ""1.2.3"",
          ""downloads"": 199,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.3.json""
        },
        {
          ""version"": ""1.2.2"",
          ""downloads"": 56204,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.2.json""
        },
        {
          ""version"": ""1.2.1"",
          ""downloads"": 1979,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.1.json""
        },
        {
          ""version"": ""1.2.0"",
          ""downloads"": 6881,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.0.json""
        },
        {
          ""version"": ""1.2.0-RC3"",
          ""downloads"": 1331,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.0-rc3.json""
        },
        {
          ""version"": ""1.2.0-RC2"",
          ""downloads"": 2736,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.0-rc2.json""
        },
        {
          ""version"": ""1.2.0-RC1"",
          ""downloads"": 3869,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.2.0-rc1.json""
        },
        {
          ""version"": ""1.1.5-Unstable"",
          ""downloads"": 3859,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.5-unstable.json""
        },
        {
          ""version"": ""1.1.4-Unstable"",
          ""downloads"": 804,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.4-unstable.json""
        },
        {
          ""version"": ""1.1.3-Unstable"",
          ""downloads"": 675,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.3-unstable.json""
        },
        {
          ""version"": ""1.1.2-Unstable"",
          ""downloads"": 404,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.2-unstable.json""
        },
        {
          ""version"": ""1.1.1-Unstable"",
          ""downloads"": 195,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.1-unstable.json""
        },
        {
          ""version"": ""1.1.0-Unstable"",
          ""downloads"": 445,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.1.0-unstable.json""
        },
        {
          ""version"": ""1.0.8"",
          ""downloads"": 16869,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.8.json""
        },
        {
          ""version"": ""1.0.7"",
          ""downloads"": 28611,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.7.json""
        },
        {
          ""version"": ""1.0.6"",
          ""downloads"": 9126,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.6.json""
        },
        {
          ""version"": ""1.0.5"",
          ""downloads"": 7302,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.5.json""
        },
        {
          ""version"": ""1.0.4"",
          ""downloads"": 1892,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.4.json""
        },
        {
          ""version"": ""1.0.3"",
          ""downloads"": 2381,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.3.json""
        },
        {
          ""version"": ""1.0.2"",
          ""downloads"": 1573,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.2.json""
        },
        {
          ""version"": ""1.0.2-Unstable"",
          ""downloads"": 146,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.2-unstable.json""
        },
        {
          ""version"": ""1.0.1"",
          ""downloads"": 828,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.1.json""
        },
        {
          ""version"": ""1.0.0"",
          ""downloads"": 170,
          ""@id"": ""https://api.nuget.org/v3/registration0/angularjs/1.0.0.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.11.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/index.json"",
      ""id"": ""jQuery.UI.Combined"",
      ""description"": ""jQuery UI is an open source library of interface components â€” interactions, full-featured widgets, and animation effects â€” based on the stellar jQuery javascript library . Each component is built according to jQuery's event-driven architecture (find something, manipulate it) and is themeable, making it easy for developers of any skill level to integrate and extend into their own code.\n    NOTE: This package is maintained on behalf of the library owners by the NuGet Community Packages project at http://nugetpackages.codeplex.com/"",
      ""summary"": ""The full jQuery UI library as a single combined file. Includes the base theme."",
      ""iconUrl"": ""http://nuget.org/Media/Default/Packages/jQuery.UI.Combined/1.8.9/jQueryUILogo.png"",
      ""tags"": [
        ""jQuery"",
        ""jQueryUI"",
        ""plugins""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""1.11.3"",
      ""versions"": [
        {
          ""version"": ""1.11.3"",
          ""downloads"": 25495,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.11.3.json""
        },
        {
          ""version"": ""1.11.2"",
          ""downloads"": 127546,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.11.2.json""
        },
        {
          ""version"": ""1.11.1"",
          ""downloads"": 158537,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.11.1.json""
        },
        {
          ""version"": ""1.11.0"",
          ""downloads"": 65127,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.11.0.json""
        },
        {
          ""version"": ""1.10.4"",
          ""downloads"": 350651,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.10.4.json""
        },
        {
          ""version"": ""1.10.3"",
          ""downloads"": 485977,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.10.3.json""
        },
        {
          ""version"": ""1.10.2"",
          ""downloads"": 208339,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.10.2.json""
        },
        {
          ""version"": ""1.10.1"",
          ""downloads"": 167197,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.10.1.json""
        },
        {
          ""version"": ""1.10.0"",
          ""downloads"": 218655,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.10.0.json""
        },
        {
          ""version"": ""1.9.2"",
          ""downloads"": 281391,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.9.2.json""
        },
        {
          ""version"": ""1.9.1"",
          ""downloads"": 105372,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.9.1.json""
        },
        {
          ""version"": ""1.9.0"",
          ""downloads"": 197558,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.9.0.json""
        },
        {
          ""version"": ""1.9.0-RC1"",
          ""downloads"": 9703,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.9.0-rc1.json""
        },
        {
          ""version"": ""1.8.24"",
          ""downloads"": 450352,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.24.json""
        },
        {
          ""version"": ""1.8.23"",
          ""downloads"": 93761,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.23.json""
        },
        {
          ""version"": ""1.8.22"",
          ""downloads"": 61202,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.22.json""
        },
        {
          ""version"": ""1.8.21"",
          ""downloads"": 29729,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.21.json""
        },
        {
          ""version"": ""1.8.20.1"",
          ""downloads"": 639997,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.20.1.json""
        },
        {
          ""version"": ""1.8.20"",
          ""downloads"": 84575,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.20.json""
        },
        {
          ""version"": ""1.8.19"",
          ""downloads"": 42345,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.19.json""
        },
        {
          ""version"": ""1.8.18"",
          ""downloads"": 56987,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.18.json""
        },
        {
          ""version"": ""1.8.17"",
          ""downloads"": 71701,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.17.json""
        },
        {
          ""version"": ""1.8.16"",
          ""downloads"": 340172,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.16.json""
        },
        {
          ""version"": ""1.8.15"",
          ""downloads"": 18528,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.15.json""
        },
        {
          ""version"": ""1.8.14"",
          ""downloads"": 22308,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.14.json""
        },
        {
          ""version"": ""1.8.13"",
          ""downloads"": 21666,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.13.json""
        },
        {
          ""version"": ""1.8.12"",
          ""downloads"": 16478,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.12.json""
        },
        {
          ""version"": ""1.8.11"",
          ""downloads"": 363716,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.11.json""
        },
        {
          ""version"": ""1.8.10"",
          ""downloads"": 542,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.10.json""
        },
        {
          ""version"": ""1.8.9"",
          ""downloads"": 1012,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.ui.combined/1.8.9.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/index.json"",
      ""id"": ""Microsoft.AspNet.WebPages"",
      ""description"": ""This package contains core runtime assemblies shared between ASP.NET MVC and ASP.NET Web Pages."",
      ""summary"": ""This package contains core runtime assemblies shared between ASP.NET MVC and ASP.NET Web Pages."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""WebPages"",
        ""AspNetWebPages""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.2.3"",
      ""versions"": [
        {
          ""version"": ""3.2.3"",
          ""downloads"": 92415,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.3.json""
        },
        {
          ""version"": ""3.2.3-beta1"",
          ""downloads"": 12976,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.3-beta1.json""
        },
        {
          ""version"": ""3.2.2"",
          ""downloads"": 772381,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.2.json""
        },
        {
          ""version"": ""3.2.2-rc"",
          ""downloads"": 12525,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.2-rc.json""
        },
        {
          ""version"": ""3.2.1-beta"",
          ""downloads"": 16493,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.1-beta.json""
        },
        {
          ""version"": ""3.2.0"",
          ""downloads"": 498866,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.0.json""
        },
        {
          ""version"": ""3.2.0-rc"",
          ""downloads"": 26266,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.2.0-rc.json""
        },
        {
          ""version"": ""3.1.2"",
          ""downloads"": 788057,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.1.2.json""
        },
        {
          ""version"": ""3.1.1"",
          ""downloads"": 579532,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.1.1.json""
        },
        {
          ""version"": ""3.1.0"",
          ""downloads"": 387359,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.1.0.json""
        },
        {
          ""version"": ""3.1.0-rc1"",
          ""downloads"": 22006,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.1.0-rc1.json""
        },
        {
          ""version"": ""3.0.1"",
          ""downloads"": 199099,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.0.1.json""
        },
        {
          ""version"": ""3.0.0"",
          ""downloads"": 1006110,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.0.0.json""
        },
        {
          ""version"": ""3.0.0-rc1"",
          ""downloads"": 31261,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.0.0-rc1.json""
        },
        {
          ""version"": ""3.0.0-beta2"",
          ""downloads"": 30648,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.0.0-beta2.json""
        },
        {
          ""version"": ""3.0.0-beta1"",
          ""downloads"": 21164,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/3.0.0-beta1.json""
        },
        {
          ""version"": ""2.0.30506"",
          ""downloads"": 874468,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/2.0.30506.json""
        },
        {
          ""version"": ""2.0.20710"",
          ""downloads"": 1513104,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/2.0.20710.json""
        },
        {
          ""version"": ""2.0.20505"",
          ""downloads"": 183892,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/2.0.20505.json""
        },
        {
          ""version"": ""1.0.20105.408"",
          ""downloads"": 119829,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webpages/1.0.20105.408.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/index.json"",
      ""id"": ""Microsoft.AspNet.WebApi.Client"",
      ""description"": ""This package adds support for formatting and content negotiation to System.Net.Http. It includes support for JSON, XML, and form URL encoded data."",
      ""summary"": ""This package adds support for formatting and content negotiation to System.Net.Http."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""WebApi"",
        ""AspNetWebApi"",
        ""HttpClient""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""5.2.3"",
      ""versions"": [
        {
          ""version"": ""5.2.3"",
          ""downloads"": 80140,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.3.json""
        },
        {
          ""version"": ""5.2.3-beta1"",
          ""downloads"": 12196,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.3-beta1.json""
        },
        {
          ""version"": ""5.2.2"",
          ""downloads"": 559274,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.2.json""
        },
        {
          ""version"": ""5.2.2-rc"",
          ""downloads"": 10224,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.2-rc.json""
        },
        {
          ""version"": ""5.2.0"",
          ""downloads"": 330264,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.0.json""
        },
        {
          ""version"": ""5.2.0-rc"",
          ""downloads"": 21074,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.2.0-rc.json""
        },
        {
          ""version"": ""5.1.2"",
          ""downloads"": 467354,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.1.2.json""
        },
        {
          ""version"": ""5.1.1"",
          ""downloads"": 337364,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.1.1.json""
        },
        {
          ""version"": ""5.1.0"",
          ""downloads"": 303986,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.1.0.json""
        },
        {
          ""version"": ""5.1.0-rc1"",
          ""downloads"": 23020,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.1.0-rc1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 807164,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc1"",
          ""downloads"": 33110,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.0.0-rc1.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 31639,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 14320,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.0.0-beta1.json""
        },
        {
          ""version"": ""5.0.0-alpha1"",
          ""downloads"": 16181,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/5.0.0-alpha1.json""
        },
        {
          ""version"": ""4.1.0-alpha-120809"",
          ""downloads"": 49695,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/4.1.0-alpha-120809.json""
        },
        {
          ""version"": ""4.0.30506"",
          ""downloads"": 771504,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/4.0.30506.json""
        },
        {
          ""version"": ""4.0.20710"",
          ""downloads"": 1214992,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/4.0.20710.json""
        },
        {
          ""version"": ""4.0.20505"",
          ""downloads"": 117607,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.client/4.0.20505.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.1.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/automapper/index.json"",
      ""id"": ""AutoMapper"",
      ""description"": ""A convention-based object-object mapper. AutoMapper uses a fluent configuration API to define an object-object mapping strategy. AutoMapper uses a convention-based matching algorithm to match up source to destination values. Currently, AutoMapper is geared towards model projection scenarios to flatten complex object models to DTOs and other simple objects, whose design is better suited for serialization, communication, messaging, or simply an anti-corruption layer between the domain and application layer."",
      ""summary"": ""A convention-based object-object mapper"",
      ""iconUrl"": ""https://s3.amazonaws.com/automapper/icon.png"",
      ""tags"": [
        """"
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.3.1"",
      ""versions"": [
        {
          ""version"": ""4.0.0-ci1032"",
          ""downloads"": 750,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1032.json""
        },
        {
          ""version"": ""4.0.0-ci1026"",
          ""downloads"": 335,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1026.json""
        },
        {
          ""version"": ""4.0.0-ci1021"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1021.json""
        },
        {
          ""version"": ""4.0.0-ci1020"",
          ""downloads"": 25,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1020.json""
        },
        {
          ""version"": ""4.0.0-ci1019"",
          ""downloads"": 100,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1019.json""
        },
        {
          ""version"": ""4.0.0-ci1018"",
          ""downloads"": 31,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1018.json""
        },
        {
          ""version"": ""4.0.0-ci1017"",
          ""downloads"": 123,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1017.json""
        },
        {
          ""version"": ""4.0.0-ci1015"",
          ""downloads"": 194,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1015.json""
        },
        {
          ""version"": ""4.0.0-ci1014"",
          ""downloads"": 24,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1014.json""
        },
        {
          ""version"": ""4.0.0-ci1007"",
          ""downloads"": 1158,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1007.json""
        },
        {
          ""version"": ""4.0.0-ci1006"",
          ""downloads"": 31,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1006.json""
        },
        {
          ""version"": ""4.0.0-ci1004"",
          ""downloads"": 31,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1004.json""
        },
        {
          ""version"": ""4.0.0-ci1002"",
          ""downloads"": 72,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/4.0.0-ci1002.json""
        },
        {
          ""version"": ""3.3.1"",
          ""downloads"": 40832,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.1.json""
        },
        {
          ""version"": ""3.3.0"",
          ""downloads"": 81884,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0.json""
        },
        {
          ""version"": ""3.3.0-ci1033"",
          ""downloads"": 42,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1033.json""
        },
        {
          ""version"": ""3.3.0-ci1032"",
          ""downloads"": 83,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1032.json""
        },
        {
          ""version"": ""3.3.0-ci1031"",
          ""downloads"": 30,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1031.json""
        },
        {
          ""version"": ""3.3.0-ci1030"",
          ""downloads"": 203,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1030.json""
        },
        {
          ""version"": ""3.3.0-ci1029"",
          ""downloads"": 75,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1029.json""
        },
        {
          ""version"": ""3.3.0-ci1028"",
          ""downloads"": 256,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1028.json""
        },
        {
          ""version"": ""3.3.0-ci1027"",
          ""downloads"": 11248,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1027.json""
        },
        {
          ""version"": ""3.3.0-ci1026"",
          ""downloads"": 55,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1026.json""
        },
        {
          ""version"": ""3.3.0-ci1025"",
          ""downloads"": 83,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1025.json""
        },
        {
          ""version"": ""3.3.0-ci1024"",
          ""downloads"": 444,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1024.json""
        },
        {
          ""version"": ""3.3.0-ci1023"",
          ""downloads"": 236,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1023.json""
        },
        {
          ""version"": ""3.3.0-ci1022"",
          ""downloads"": 587,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1022.json""
        },
        {
          ""version"": ""3.3.0-ci1021"",
          ""downloads"": 86,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1021.json""
        },
        {
          ""version"": ""3.3.0-ci1020"",
          ""downloads"": 99,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1020.json""
        },
        {
          ""version"": ""3.3.0-ci1019"",
          ""downloads"": 169,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1019.json""
        },
        {
          ""version"": ""3.3.0-ci1018"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1018.json""
        },
        {
          ""version"": ""3.3.0-ci1017"",
          ""downloads"": 4790,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1017.json""
        },
        {
          ""version"": ""3.3.0-ci1016"",
          ""downloads"": 97,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1016.json""
        },
        {
          ""version"": ""3.3.0-ci1009"",
          ""downloads"": 170,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1009.json""
        },
        {
          ""version"": ""3.3.0-ci1008"",
          ""downloads"": 1119,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1008.json""
        },
        {
          ""version"": ""3.3.0-ci1007"",
          ""downloads"": 121,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1007.json""
        },
        {
          ""version"": ""3.3.0-ci1006"",
          ""downloads"": 361,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1006.json""
        },
        {
          ""version"": ""3.3.0-ci1005"",
          ""downloads"": 59,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1005.json""
        },
        {
          ""version"": ""3.3.0-ci1004"",
          ""downloads"": 60,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1004.json""
        },
        {
          ""version"": ""3.3.0-ci1003"",
          ""downloads"": 95,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1003.json""
        },
        {
          ""version"": ""3.3.0-ci1002"",
          ""downloads"": 149,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1002.json""
        },
        {
          ""version"": ""3.3.0-ci1001"",
          ""downloads"": 91,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1001.json""
        },
        {
          ""version"": ""3.3.0-ci1000"",
          ""downloads"": 62,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.3.0-ci1000.json""
        },
        {
          ""version"": ""3.2.1"",
          ""downloads"": 308550,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.1.json""
        },
        {
          ""version"": ""3.2.1-ci1002"",
          ""downloads"": 74,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.1-ci1002.json""
        },
        {
          ""version"": ""3.2.1-ci1001"",
          ""downloads"": 1647,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.1-ci1001.json""
        },
        {
          ""version"": ""3.2.1-ci1000"",
          ""downloads"": 299,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.1-ci1000.json""
        },
        {
          ""version"": ""3.2.0"",
          ""downloads"": 21513,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0.json""
        },
        {
          ""version"": ""3.2.0-ci1043"",
          ""downloads"": 87,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1043.json""
        },
        {
          ""version"": ""3.2.0-ci1042"",
          ""downloads"": 246,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1042.json""
        },
        {
          ""version"": ""3.2.0-ci1041"",
          ""downloads"": 51,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1041.json""
        },
        {
          ""version"": ""3.2.0-ci1040"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1040.json""
        },
        {
          ""version"": ""3.2.0-ci1039"",
          ""downloads"": 55,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1039.json""
        },
        {
          ""version"": ""3.2.0-ci1038"",
          ""downloads"": 75,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1038.json""
        },
        {
          ""version"": ""3.2.0-ci1037"",
          ""downloads"": 59,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1037.json""
        },
        {
          ""version"": ""3.2.0-ci1036"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1036.json""
        },
        {
          ""version"": ""3.2.0-ci1035"",
          ""downloads"": 62,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1035.json""
        },
        {
          ""version"": ""3.2.0-ci1034"",
          ""downloads"": 51,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1034.json""
        },
        {
          ""version"": ""3.2.0-ci1033"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1033.json""
        },
        {
          ""version"": ""3.2.0-ci1030"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1030.json""
        },
        {
          ""version"": ""3.2.0-ci1029"",
          ""downloads"": 54,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1029.json""
        },
        {
          ""version"": ""3.2.0-ci1028"",
          ""downloads"": 51,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1028.json""
        },
        {
          ""version"": ""3.2.0-ci1027"",
          ""downloads"": 168,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1027.json""
        },
        {
          ""version"": ""3.2.0-ci1026"",
          ""downloads"": 53,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1026.json""
        },
        {
          ""version"": ""3.2.0-ci1025"",
          ""downloads"": 142,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1025.json""
        },
        {
          ""version"": ""3.2.0-ci1024"",
          ""downloads"": 52,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1024.json""
        },
        {
          ""version"": ""3.2.0-ci1023"",
          ""downloads"": 54,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1023.json""
        },
        {
          ""version"": ""3.2.0-ci1022"",
          ""downloads"": 83,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1022.json""
        },
        {
          ""version"": ""3.2.0-ci1021"",
          ""downloads"": 177,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1021.json""
        },
        {
          ""version"": ""3.2.0-ci1017"",
          ""downloads"": 790,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1017.json""
        },
        {
          ""version"": ""3.2.0-ci1016"",
          ""downloads"": 84,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1016.json""
        },
        {
          ""version"": ""3.2.0-ci1015"",
          ""downloads"": 85,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1015.json""
        },
        {
          ""version"": ""3.2.0-ci1014"",
          ""downloads"": 85,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1014.json""
        },
        {
          ""version"": ""3.2.0-ci1011"",
          ""downloads"": 1488,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1011.json""
        },
        {
          ""version"": ""3.2.0-ci1010"",
          ""downloads"": 642,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1010.json""
        },
        {
          ""version"": ""3.2.0-ci1009"",
          ""downloads"": 82,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1009.json""
        },
        {
          ""version"": ""3.2.0-ci1008"",
          ""downloads"": 80,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1008.json""
        },
        {
          ""version"": ""3.2.0-ci1005"",
          ""downloads"": 264,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1005.json""
        },
        {
          ""version"": ""3.2.0-ci1004"",
          ""downloads"": 384,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1004.json""
        },
        {
          ""version"": ""3.2.0-ci1003"",
          ""downloads"": 465,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1003.json""
        },
        {
          ""version"": ""3.2.0-ci1002"",
          ""downloads"": 837,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1002.json""
        },
        {
          ""version"": ""3.2.0-ci1001"",
          ""downloads"": 130,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1001.json""
        },
        {
          ""version"": ""3.2.0-ci1000"",
          ""downloads"": 299,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.2.0-ci1000.json""
        },
        {
          ""version"": ""3.1.1"",
          ""downloads"": 194245,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.1.json""
        },
        {
          ""version"": ""3.1.1-ci1003"",
          ""downloads"": 487,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.1-ci1003.json""
        },
        {
          ""version"": ""3.1.1-ci1000"",
          ""downloads"": 6552,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.1-ci1000.json""
        },
        {
          ""version"": ""3.1.0"",
          ""downloads"": 66726,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0.json""
        },
        {
          ""version"": ""3.1.0-ci1058"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1058.json""
        },
        {
          ""version"": ""3.1.0-ci1056"",
          ""downloads"": 778,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1056.json""
        },
        {
          ""version"": ""3.1.0-ci1053"",
          ""downloads"": 147,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1053.json""
        },
        {
          ""version"": ""3.1.0-ci1051"",
          ""downloads"": 464,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1051.json""
        },
        {
          ""version"": ""3.1.0-ci1050"",
          ""downloads"": 84,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1050.json""
        },
        {
          ""version"": ""3.1.0-ci1049"",
          ""downloads"": 641,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1049.json""
        },
        {
          ""version"": ""3.1.0-ci1048"",
          ""downloads"": 413,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1048.json""
        },
        {
          ""version"": ""3.1.0-ci1047"",
          ""downloads"": 261,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1047.json""
        },
        {
          ""version"": ""3.1.0-ci1046"",
          ""downloads"": 88,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1046.json""
        },
        {
          ""version"": ""3.1.0-ci1045"",
          ""downloads"": 205,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1045.json""
        },
        {
          ""version"": ""3.1.0-ci1044"",
          ""downloads"": 368,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1044.json""
        },
        {
          ""version"": ""3.1.0-ci1043"",
          ""downloads"": 157,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1043.json""
        },
        {
          ""version"": ""3.1.0-ci1038"",
          ""downloads"": 269,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1038.json""
        },
        {
          ""version"": ""3.1.0-ci1037"",
          ""downloads"": 157,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1037.json""
        },
        {
          ""version"": ""3.1.0-ci1036"",
          ""downloads"": 1076,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1036.json""
        },
        {
          ""version"": ""3.1.0-ci1035"",
          ""downloads"": 94,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1035.json""
        },
        {
          ""version"": ""3.1.0-ci1034"",
          ""downloads"": 257,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1034.json""
        },
        {
          ""version"": ""3.1.0-ci1033"",
          ""downloads"": 125,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1033.json""
        },
        {
          ""version"": ""3.1.0-ci1032"",
          ""downloads"": 2223,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1032.json""
        },
        {
          ""version"": ""3.1.0-ci1027"",
          ""downloads"": 479,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1027.json""
        },
        {
          ""version"": ""3.1.0-ci1026"",
          ""downloads"": 638,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1026.json""
        },
        {
          ""version"": ""3.1.0-ci1024"",
          ""downloads"": 641,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1024.json""
        },
        {
          ""version"": ""3.1.0-ci1023"",
          ""downloads"": 538,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1023.json""
        },
        {
          ""version"": ""3.1.0-ci1022"",
          ""downloads"": 101,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1022.json""
        },
        {
          ""version"": ""3.1.0-ci1021"",
          ""downloads"": 649,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1021.json""
        },
        {
          ""version"": ""3.1.0-ci1020"",
          ""downloads"": 1284,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1020.json""
        },
        {
          ""version"": ""3.1.0-ci1019"",
          ""downloads"": 156,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1019.json""
        },
        {
          ""version"": ""3.1.0-ci1018"",
          ""downloads"": 102,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1018.json""
        },
        {
          ""version"": ""3.1.0-ci1017"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1017.json""
        },
        {
          ""version"": ""3.1.0-ci1016"",
          ""downloads"": 272,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1016.json""
        },
        {
          ""version"": ""3.1.0-ci1014"",
          ""downloads"": 109,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.1.0-ci1014.json""
        },
        {
          ""version"": ""3.0.0"",
          ""downloads"": 142407,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0.json""
        },
        {
          ""version"": ""3.0.0-ci1053"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1053.json""
        },
        {
          ""version"": ""3.0.0-ci1043"",
          ""downloads"": 298,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1043.json""
        },
        {
          ""version"": ""3.0.0-ci1042"",
          ""downloads"": 258,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1042.json""
        },
        {
          ""version"": ""3.0.0-ci1041"",
          ""downloads"": 89,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1041.json""
        },
        {
          ""version"": ""3.0.0-ci1040"",
          ""downloads"": 96,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1040.json""
        },
        {
          ""version"": ""3.0.0-ci1039"",
          ""downloads"": 183,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1039.json""
        },
        {
          ""version"": ""3.0.0-ci1038"",
          ""downloads"": 177,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1038.json""
        },
        {
          ""version"": ""3.0.0-ci1037"",
          ""downloads"": 4963,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1037.json""
        },
        {
          ""version"": ""3.0.0-ci1036"",
          ""downloads"": 1919,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1036.json""
        },
        {
          ""version"": ""3.0.0-ci1035"",
          ""downloads"": 2977,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1035.json""
        },
        {
          ""version"": ""3.0.0-ci1034"",
          ""downloads"": 353,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1034.json""
        },
        {
          ""version"": ""3.0.0-ci1033"",
          ""downloads"": 215,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1033.json""
        },
        {
          ""version"": ""3.0.0-ci1032"",
          ""downloads"": 378,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1032.json""
        },
        {
          ""version"": ""3.0.0-ci1031"",
          ""downloads"": 185,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1031.json""
        },
        {
          ""version"": ""3.0.0-ci1029"",
          ""downloads"": 541,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1029.json""
        },
        {
          ""version"": ""3.0.0-ci1028"",
          ""downloads"": 2801,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1028.json""
        },
        {
          ""version"": ""3.0.0-ci1026"",
          ""downloads"": 383,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/3.0.0-ci1026.json""
        },
        {
          ""version"": ""2.2.1"",
          ""downloads"": 283040,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1.json""
        },
        {
          ""version"": ""2.2.1-ci9006"",
          ""downloads"": 138,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9006.json""
        },
        {
          ""version"": ""2.2.1-ci9005"",
          ""downloads"": 269,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9005.json""
        },
        {
          ""version"": ""2.2.1-ci9004"",
          ""downloads"": 1930,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9004.json""
        },
        {
          ""version"": ""2.2.1-ci9003"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9003.json""
        },
        {
          ""version"": ""2.2.1-ci9002"",
          ""downloads"": 275,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9002.json""
        },
        {
          ""version"": ""2.2.1-ci9001"",
          ""downloads"": 3499,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9001.json""
        },
        {
          ""version"": ""2.2.1-ci9000"",
          ""downloads"": 16191,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9000.json""
        },
        {
          ""version"": ""2.2.1-ci9"",
          ""downloads"": 2208,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci9.json""
        },
        {
          ""version"": ""2.2.1-ci8"",
          ""downloads"": 1742,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci8.json""
        },
        {
          ""version"": ""2.2.1-ci7"",
          ""downloads"": 394,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci7.json""
        },
        {
          ""version"": ""2.2.1-ci6"",
          ""downloads"": 291,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci6.json""
        },
        {
          ""version"": ""2.2.1-ci5"",
          ""downloads"": 270,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci5.json""
        },
        {
          ""version"": ""2.2.1-ci4"",
          ""downloads"": 98,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci4.json""
        },
        {
          ""version"": ""2.2.1-ci17"",
          ""downloads"": 275,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci17.json""
        },
        {
          ""version"": ""2.2.1-ci16"",
          ""downloads"": 104,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci16.json""
        },
        {
          ""version"": ""2.2.1-ci15"",
          ""downloads"": 274,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci15.json""
        },
        {
          ""version"": ""2.2.1-ci11"",
          ""downloads"": 105,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci11.json""
        },
        {
          ""version"": ""2.2.1-ci1000"",
          ""downloads"": 101,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci1000.json""
        },
        {
          ""version"": ""2.2.1-ci10"",
          ""downloads"": 279,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.1-ci10.json""
        },
        {
          ""version"": ""2.2.0"",
          ""downloads"": 132495,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.2.0.json""
        },
        {
          ""version"": ""2.1.267"",
          ""downloads"": 133928,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.1.267.json""
        },
        {
          ""version"": ""2.1.266"",
          ""downloads"": 12961,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.1.266.json""
        },
        {
          ""version"": ""2.1.265"",
          ""downloads"": 21737,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.1.265.json""
        },
        {
          ""version"": ""2.1.262"",
          ""downloads"": 254,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.1.262.json""
        },
        {
          ""version"": ""2.1.1"",
          ""downloads"": 1108,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.1.1.json""
        },
        {
          ""version"": ""2.0.0"",
          ""downloads"": 67279,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/2.0.0.json""
        },
        {
          ""version"": ""1.1.0.118"",
          ""downloads"": 44380,
          ""@id"": ""https://api.nuget.org/v3/registration0/automapper/1.1.0.118.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.1.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/index.json"",
      ""id"": ""Microsoft.Owin.Host.SystemWeb"",
      ""description"": ""OWIN server that enables OWIN-based applications to run on IIS using the ASP.NET request pipeline."",
      ""tags"": [
        ""Microsoft"",
        ""OWIN"",
        ""Katana""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.0.1"",
      ""versions"": [
        {
          ""version"": ""3.0.1"",
          ""downloads"": 17127,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.1.json""
        },
        {
          ""version"": ""3.0.0"",
          ""downloads"": 288827,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.0.json""
        },
        {
          ""version"": ""3.0.0-rc2"",
          ""downloads"": 4150,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.0-rc2.json""
        },
        {
          ""version"": ""3.0.0-rc1"",
          ""downloads"": 1049,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.0-rc1.json""
        },
        {
          ""version"": ""3.0.0-beta1"",
          ""downloads"": 14366,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.0-beta1.json""
        },
        {
          ""version"": ""3.0.0-alpha1"",
          ""downloads"": 4496,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/3.0.0-alpha1.json""
        },
        {
          ""version"": ""2.1.0"",
          ""downloads"": 367107,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.1.0.json""
        },
        {
          ""version"": ""2.1.0-rc1"",
          ""downloads"": 1647,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.1.0-rc1.json""
        },
        {
          ""version"": ""2.0.2"",
          ""downloads"": 292515,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.0.2.json""
        },
        {
          ""version"": ""2.0.1"",
          ""downloads"": 112729,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.0.1.json""
        },
        {
          ""version"": ""2.0.0"",
          ""downloads"": 183463,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.0.0.json""
        },
        {
          ""version"": ""2.0.0-rc1"",
          ""downloads"": 12281,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/2.0.0-rc1.json""
        },
        {
          ""version"": ""1.1.0-beta2"",
          ""downloads"": 11381,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/1.1.0-beta2.json""
        },
        {
          ""version"": ""1.1.0-beta1"",
          ""downloads"": 4561,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/1.1.0-beta1.json""
        },
        {
          ""version"": ""1.0.1"",
          ""downloads"": 184245,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/1.0.1.json""
        },
        {
          ""version"": ""1.0.0"",
          ""downloads"": 46352,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.owin.host.systemweb/1.0.0.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1409.1722.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/moq/index.json"",
      ""id"": ""Moq"",
      ""description"": ""Moq is the most popular and friendly mocking framework for .NET"",
      ""tags"": [
        ""moq"",
        ""tdd"",
        ""mocking"",
        ""mocks"",
        ""unittesting"",
        ""agile"",
        ""unittest""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""4.2.1409.1722"",
      ""versions"": [
        {
          ""version"": ""4.2.1409.1722"",
          ""downloads"": 225045,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1409.1722.json""
        },
        {
          ""version"": ""4.2.1408.717"",
          ""downloads"": 172361,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1408.717.json""
        },
        {
          ""version"": ""4.2.1408.619"",
          ""downloads"": 3321,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1408.619.json""
        },
        {
          ""version"": ""4.2.1402.2112"",
          ""downloads"": 453218,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1402.2112.json""
        },
        {
          ""version"": ""4.2.1312.1622"",
          ""downloads"": 152228,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1622.json""
        },
        {
          ""version"": ""4.2.1312.1621"",
          ""downloads"": 578,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1621.json""
        },
        {
          ""version"": ""4.2.1312.1615"",
          ""downloads"": 1242,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1615.json""
        },
        {
          ""version"": ""4.2.1312.1416"",
          ""downloads"": 5269,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1416.json""
        },
        {
          ""version"": ""4.2.1312.1323"",
          ""downloads"": 655,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1323.json""
        },
        {
          ""version"": ""4.2.1312.1319"",
          ""downloads"": 947,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.2.1312.1319.json""
        },
        {
          ""version"": ""4.1.1311.615"",
          ""downloads"": 109187,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1311.615.json""
        },
        {
          ""version"": ""4.1.1309.1617"",
          ""downloads"": 166573,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1309.1617.json""
        },
        {
          ""version"": ""4.1.1309.919"",
          ""downloads"": 152274,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1309.919.json""
        },
        {
          ""version"": ""4.1.1309.801"",
          ""downloads"": 3350,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1309.801.json""
        },
        {
          ""version"": ""4.1.1309.800"",
          ""downloads"": 274,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1309.800.json""
        },
        {
          ""version"": ""4.1.1308.2321"",
          ""downloads"": 38522,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1308.2321.json""
        },
        {
          ""version"": ""4.1.1308.2316"",
          ""downloads"": 707,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1308.2316.json""
        },
        {
          ""version"": ""4.1.1308.2120"",
          ""downloads"": 5017,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.1.1308.2120.json""
        },
        {
          ""version"": ""4.0.10827"",
          ""downloads"": 1015263,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/4.0.10827.json""
        },
        {
          ""version"": ""3.1.416.3"",
          ""downloads"": 20572,
          ""@id"": ""https://api.nuget.org/v3/registration0/moq/3.1.416.3.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/index.json"",
      ""id"": ""Microsoft.AspNet.Razor"",
      ""description"": ""This package contains the runtime assemblies for ASP.NET Web Pages. ASP.NET Web Pages and the new Razor syntax provide a fast, terse, clean and lightweight way to combine server code with HTML to create dynamic web content."",
      ""summary"": ""This package contains the runtime assemblies for ASP.NET Web Pages."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""WebPages"",
        ""AspNetWebPages"",
        ""Razor""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.2.3"",
      ""versions"": [
        {
          ""version"": ""4.0.0-beta3"",
          ""downloads"": 3990,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/4.0.0-beta3.json""
        },
        {
          ""version"": ""4.0.0-beta2"",
          ""downloads"": 15651,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/4.0.0-beta2.json""
        },
        {
          ""version"": ""4.0.0-beta1"",
          ""downloads"": 30431,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/4.0.0-beta1.json""
        },
        {
          ""version"": ""3.2.3"",
          ""downloads"": 86498,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.3.json""
        },
        {
          ""version"": ""3.2.3-beta1"",
          ""downloads"": 10169,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.3-beta1.json""
        },
        {
          ""version"": ""3.2.2"",
          ""downloads"": 578984,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.2.json""
        },
        {
          ""version"": ""3.2.2-rc"",
          ""downloads"": 13039,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.2-rc.json""
        },
        {
          ""version"": ""3.2.0"",
          ""downloads"": 388476,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.0.json""
        },
        {
          ""version"": ""3.2.0-rc"",
          ""downloads"": 20806,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.2.0-rc.json""
        },
        {
          ""version"": ""3.1.2"",
          ""downloads"": 606491,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.1.2.json""
        },
        {
          ""version"": ""3.1.1"",
          ""downloads"": 395415,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.1.1.json""
        },
        {
          ""version"": ""3.1.0"",
          ""downloads"": 268937,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.1.0.json""
        },
        {
          ""version"": ""3.1.0-rc1"",
          ""downloads"": 22862,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.1.0-rc1.json""
        },
        {
          ""version"": ""3.0.0"",
          ""downloads"": 1113602,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.0.0.json""
        },
        {
          ""version"": ""3.0.0-rc1"",
          ""downloads"": 31620,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.0.0-rc1.json""
        },
        {
          ""version"": ""3.0.0-beta2"",
          ""downloads"": 31152,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.0.0-beta2.json""
        },
        {
          ""version"": ""3.0.0-beta1"",
          ""downloads"": 21245,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/3.0.0-beta1.json""
        },
        {
          ""version"": ""2.0.30506"",
          ""downloads"": 912490,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/2.0.30506.json""
        },
        {
          ""version"": ""2.0.20715"",
          ""downloads"": 1059821,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/2.0.20715.json""
        },
        {
          ""version"": ""2.0.20710"",
          ""downloads"": 1035910,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/2.0.20710.json""
        },
        {
          ""version"": ""2.0.20505"",
          ""downloads"": 180323,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/2.0.20505.json""
        },
        {
          ""version"": ""1.0.20105.408"",
          ""downloads"": 111724,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.razor/1.0.20105.408.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.5.0.2.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/antlr/index.json"",
      ""id"": ""Antlr"",
      ""description"": ""ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages."",
      ""summary"": ""ANother Tool for Language Recognition, is a language tool that provides a framework for constructing recognizers, interpreters, compilers, and translators from grammatical descriptions containing actions in a variety of target languages."",
      ""iconUrl"": ""http://www.antlr.org/images/antlr-link.gif"",
      ""tags"": [
        """"
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""3.5.0.2"",
      ""versions"": [
        {
          ""version"": ""3.5.0.2"",
          ""downloads"": 774321,
          ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.5.0.2.json""
        },
        {
          ""version"": ""3.4.1.9004"",
          ""downloads"": 1172438,
          ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.4.1.9004.json""
        },
        {
          ""version"": ""3.4.1.9004-pre"",
          ""downloads"": 169,
          ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.4.1.9004-pre.json""
        },
        {
          ""version"": ""3.1.3.42154"",
          ""downloads"": 61500,
          ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.1.3.42154.json""
        },
        {
          ""version"": ""3.1.1"",
          ""downloads"": 24757,
          ""@id"": ""https://api.nuget.org/v3/registration0/antlr/3.1.1.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.13.1.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/jquery.validation/index.json"",
      ""id"": ""jQuery.Validation"",
      ""description"": ""This jQuery plugin makes simple clientside form validation trivial, while offering lots of option for customization. That makes a good choice if youâ€™re building something new from scratch, but also when youâ€™re trying to integrate it into an existing application with lots of existing markup. The plugin comes bundled with a useful set of validation methods, including URL and email validation, while providing an API to write your own methods. All bundled methods come with default error messages in english and translations into 32 languages.\n    NOTE: This package is maintained on behalf of the library owners by the NuGet Community Packages project at http://nugetpackages.codeplex.com/"",
      ""tags"": [
        ""jQuery"",
        ""plugins""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""1.13.1"",
      ""versions"": [
        {
          ""version"": ""1.13.1"",
          ""downloads"": 200851,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.13.1.json""
        },
        {
          ""version"": ""1.13.0"",
          ""downloads"": 295139,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.13.0.json""
        },
        {
          ""version"": ""1.12.0"",
          ""downloads"": 278534,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.12.0.json""
        },
        {
          ""version"": ""1.11.1"",
          ""downloads"": 980782,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.11.1.json""
        },
        {
          ""version"": ""1.11.0"",
          ""downloads"": 264762,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.11.0.json""
        },
        {
          ""version"": ""1.10.0"",
          ""downloads"": 665547,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.10.0.json""
        },
        {
          ""version"": ""1.9.0.1"",
          ""downloads"": 716406,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.9.0.1.json""
        },
        {
          ""version"": ""1.9.0"",
          ""downloads"": 236999,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.9.0.json""
        },
        {
          ""version"": ""1.8.1"",
          ""downloads"": 975831,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.8.1.json""
        },
        {
          ""version"": ""1.8.0.1"",
          ""downloads"": 45600,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.8.0.1.json""
        },
        {
          ""version"": ""1.8.0"",
          ""downloads"": 242361,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.8.0.json""
        },
        {
          ""version"": ""1.7.0"",
          ""downloads"": 811,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.7.0.json""
        },
        {
          ""version"": ""1.6.0"",
          ""downloads"": 2149,
          ""@id"": ""https://api.nuget.org/v3/registration0/jquery.validation/1.6.0.json""
        }
      ]
    },
    {
      ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.3.json"",
      ""@type"": ""Package"",
      ""registration"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/index.json"",
      ""id"": ""Microsoft.AspNet.WebApi.Core"",
      ""description"": ""This package contains the core runtime assemblies for ASP.NET Web API. This package is used by hosts of the ASP.NET Web API runtime. To host a Web API in IIS use the Microsoft.AspNet.WebApi.WebHost package. To host a Web API in your own process use the Microsoft.AspNet.WebApi.SelfHost package."",
      ""summary"": ""This package contains the core runtime assemblies for ASP.NET Web API."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=288859"",
      ""tags"": [
        ""Microsoft"",
        ""AspNet"",
        ""WebApi"",
        ""AspNetWebApi""
      ],
      ""authors"": [
        """"
      ],
      ""version"": ""5.2.3"",
      ""versions"": [
        {
          ""version"": ""5.2.3"",
          ""downloads"": 73723,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.3.json""
        },
        {
          ""version"": ""5.2.3-beta1"",
          ""downloads"": 11212,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.3-beta1.json""
        },
        {
          ""version"": ""5.2.2"",
          ""downloads"": 557676,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.2.json""
        },
        {
          ""version"": ""5.2.2-rc"",
          ""downloads"": 9792,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.2-rc.json""
        },
        {
          ""version"": ""5.2.0"",
          ""downloads"": 309358,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.0.json""
        },
        {
          ""version"": ""5.2.0-rc"",
          ""downloads"": 19486,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.2.0-rc.json""
        },
        {
          ""version"": ""5.1.2"",
          ""downloads"": 448208,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.1.2.json""
        },
        {
          ""version"": ""5.1.1"",
          ""downloads"": 323582,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.1.1.json""
        },
        {
          ""version"": ""5.1.0"",
          ""downloads"": 297800,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.1.0.json""
        },
        {
          ""version"": ""5.1.0-rc1"",
          ""downloads"": 20496,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.1.0-rc1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 753902,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc1"",
          ""downloads"": 31421,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.0.0-rc1.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 29797,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 13922,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/5.0.0-beta1.json""
        },
        {
          ""version"": ""4.0.30506"",
          ""downloads"": 698132,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/4.0.30506.json""
        },
        {
          ""version"": ""4.0.20710"",
          ""downloads"": 1215191,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/4.0.20710.json""
        },
        {
          ""version"": ""4.0.20505"",
          ""downloads"": 113990,
          ""@id"": ""https://api.nuget.org/v3/registration0/microsoft.aspnet.webapi.core/4.0.20505.json""
        }
      ]
    }
  ]
}";

        #endregion

        #region Example search result

        public const string ExamplePSMetadata = @"{
  ""@context"": {
    ""@vocab"": ""http://schema.nuget.org/schema#""
  },
  ""totalHits"": 30905,
  ""lastReopen"": ""2015-03-02T22:23:09.1927311Z"",
  ""index"": ""v3-lucene0"",
  ""data"": [
    {
      ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.2.json"",
      ""@type"": ""Package"",
      ""registration"": ""http://api.nuget.org/v3/registration0/entityframework/index.json"",
      ""id"": ""EntityFramework"",
      ""description"": ""Entity Framework is Microsoft's recommended data access technology for new applications."",
      ""iconUrl"": ""http://go.microsoft.com/fwlink/?LinkID=386613"",
      ""tags"": [
        ""Microsoft"",
        ""EF"",
        ""Database"",
        ""Data"",
        ""O/RM"",
        ""ADO.NET""
      ],
      ""authors"": [
        ""Azure Automation Team""
      ],
      ""version"": ""6.1.2"",
      ""ModuleVersion"": ""1.0"",
      ""CompanyName"": ""Microsoft Corporation"",
      ""GUID"": ""e4da48d8-20df-4d58-bfa6-2e54486fca5b"",
      ""PowerShellHostVersion"": ""5.0"",
      ""DotNetFrameworkVersion"": ""4.0"",
      ""CLRVersion"": ""4.0"",
      ""ProcessorArchitecture"": ""x64, x86"",
      ""CmdletsToExport"": [
        ""Get-Test"",
        ""Get-Test2""
        ],
      ""FunctionsToExport"": [""Set-Test""],
      ""DscResourcesToExport"": [""xFirefox""],
      ""licenseUrl"": ""http://license.com"",
      ""projectUrl"": ""http://project.com"",
      ""releaseNotes"": ""http://release.notes.com"",
      ""versions"": [
        {
          ""version"": ""6.1.3-beta1"",
          ""downloads"": 8125,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.3-beta1.json""
        },
        {
          ""version"": ""6.1.2"",
          ""downloads"": 412391,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.2.json""
        },
        {
          ""version"": ""6.1.2-beta2"",
          ""downloads"": 24516,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.2-beta2.json""
        },
        {
          ""version"": ""6.1.2-beta1"",
          ""downloads"": 38492,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.2-beta1.json""
        },
        {
          ""version"": ""6.1.1"",
          ""downloads"": 1012924,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.1.json""
        },
        {
          ""version"": ""6.1.1-beta1"",
          ""downloads"": 27418,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.1-beta1.json""
        },
        {
          ""version"": ""6.1.0"",
          ""downloads"": 752662,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.0.json""
        },
        {
          ""version"": ""6.1.0-beta1"",
          ""downloads"": 66050,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.0-beta1.json""
        },
        {
          ""version"": ""6.1.0-alpha1"",
          ""downloads"": 36118,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.1.0-alpha1.json""
        },
        {
          ""version"": ""6.0.2"",
          ""downloads"": 1250450,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.2.json""
        },
        {
          ""version"": ""6.0.2-beta1"",
          ""downloads"": 34968,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.2-beta1.json""
        },
        {
          ""version"": ""6.0.1"",
          ""downloads"": 736735,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.1.json""
        },
        {
          ""version"": ""6.0.0"",
          ""downloads"": 596420,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0.json""
        },
        {
          ""version"": ""6.0.0-rc1"",
          ""downloads"": 60231,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0-rc1.json""
        },
        {
          ""version"": ""6.0.0-beta1"",
          ""downloads"": 60275,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0-beta1.json""
        },
        {
          ""version"": ""6.0.0-alpha3"",
          ""downloads"": 47264,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha3.json""
        },
        {
          ""version"": ""6.0.0-alpha2"",
          ""downloads"": 40641,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha2.json""
        },
        {
          ""version"": ""6.0.0-alpha1"",
          ""downloads"": 26312,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/6.0.0-alpha1.json""
        },
        {
          ""version"": ""5.0.0"",
          ""downloads"": 2249606,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/5.0.0.json""
        },
        {
          ""version"": ""5.0.0-rc"",
          ""downloads"": 97946,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/5.0.0-rc.json""
        },
        {
          ""version"": ""5.0.0-beta2"",
          ""downloads"": 14370,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/5.0.0-beta2.json""
        },
        {
          ""version"": ""5.0.0-beta1"",
          ""downloads"": 4634,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/5.0.0-beta1.json""
        },
        {
          ""version"": ""4.3.1"",
          ""downloads"": 391001,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.3.1.json""
        },
        {
          ""version"": ""4.3.0"",
          ""downloads"": 67558,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.3.0.json""
        },
        {
          ""version"": ""4.3.0-beta1"",
          ""downloads"": 3519,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.3.0-beta1.json""
        },
        {
          ""version"": ""4.2.0"",
          ""downloads"": 331290,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.2.0.json""
        },
        {
          ""version"": ""4.1.10715"",
          ""downloads"": 606224,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.1.10715.json""
        },
        {
          ""version"": ""4.1.10331"",
          ""downloads"": 341597,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.1.10331.json""
        },
        {
          ""version"": ""4.1.10311"",
          ""downloads"": 31291,
          ""@id"": ""http://api.nuget.org/v3/registration0/entityframework/4.1.10311.json""
        }
      ]
    }
  ]
}";

        #endregion
    }
}