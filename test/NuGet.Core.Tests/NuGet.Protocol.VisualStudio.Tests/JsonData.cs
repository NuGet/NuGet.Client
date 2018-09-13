using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGet.Protocol.VisualStudio.Tests
{
    public static class JsonData
    {
        #region PSAutoCompleteV2Example
        public const string PSAutoCompleteV2Example = @"[""elm.TypeScript.DefinitelyTyped"",""elmah"",""Elmah.AzureTableStorage"",""Elmah.BlogEngine.Net"",""Elmah.Contrib.EntityFramework"",""Elmah.Contrib.Mvc"",""Elmah.Contrib.WebApi"",""elmah.corelibrary"",""elmah.corelibrary.ews"",""Elmah.ElasticSearch"",""Elmah.Everywhere"",""elmah.ews"",""Elmah.FallbackErrorLog"",""elmah.filtering.sample"",""elmah.io"",""elmah.io.client"",""elmah.io.core"",""elmah.io.log4net"",""elmah.io.umbraco"",""elmah.mongodb"",""elmah.msaccess"",""Elmah.MVC"",""Elmah.MVC.ews"",""Elmah.MVC.XMLLight"",""elmah.mysql"",""elmah.oracle"",""elmah.postgresql"",""Elmah.RavenDB"",""Elmah.RavenDB.3"",""Elmah.RavenDB-4.5""]";
        #endregion PSAutoCompleteV2Example


        #region PSAutoCompleteV3Example
        public const string PsAutoCompleteV3Example = @"{
              ""@context"": {
                ""@vocab"": ""http://schema.nuget.org/schema#""
              },
              ""totalHits"": 79,
              ""indexName"": ""v3-lucene0"",
              ""data"": [
                ""elmah"",
                ""ElmahR.Elmah"",
                ""elmah.io"",
                ""ElmahR.Core"",
                ""elmah.xml"",
                ""elmah.mysql"",
                ""elmahappender_log4net"",
                ""Nancy.Elmah"",
                ""elmah.ews"",
                ""NLog.Elmah"",
                ""Elmah.Mvc"",
                ""ElmahR.Appharbor"",
                ""ElmahR.Api.Nancy"",
                ""ElmahFiddler"",
                ""ElmahR.Modules.Dashboard"",
                ""ElmahR.IoC.NInject"",
                ""ElmahR.Api.Client"",
                ""ElmahEFLogger"",
                ""Serilog.Sinks.ElmahIO"",
                ""Logary.Targets.ElmahIO""
              ]
            }";
        #endregion PSAutoCompleteV3Example

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

    }
}
