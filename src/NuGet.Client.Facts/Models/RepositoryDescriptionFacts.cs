using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;
using Xunit;

namespace NuGet.Client.Models
{
    public class RepositoryDescriptionFacts
    {
        public class TheConstructor
        {
            [Fact]
            public void GivenANullVersion_ItThrows()
            {
                Assert.Throws<ArgumentNullException>("version", () => new RepositoryDescription(null, Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()));
            }

            [Fact]
            public void GivenANullMirrorsList_ItThrows()
            {
                Assert.Throws<ArgumentNullException>("mirrors", () => new RepositoryDescription(new Version(3, 0), null, Enumerable.Empty<ServiceDescription>()));
            }

            [Fact]
            public void GivenANullServicesList_ItThrows()
            {
                Assert.Throws<ArgumentNullException>("services", () => new RepositoryDescription(new Version(3, 0), Enumerable.Empty<Uri>(), null));
            }
        }

        public class TheFromJsonMethod
        {
            // Exceptions

            [Fact]
            public void GivenANullJObject_ItThrows()
            {
                Assert.Throws<ArgumentNullException>("json", () => RepositoryDescription.FromJson(null, new Tracer("foo", TraceSinks.Null), new Uri("http://api.nuget.org")));
            }

            [Fact]
            public void GivenANullTracer_ItThrows()
            {
                Assert.Throws<ArgumentNullException>("trace", () => RepositoryDescription.FromJson(new JObject(), null, new Uri("http://api.nuget.org")));
            }

            // Valid JSON

            [Fact]
            public void GivenASpecificVersion_ItCorrectlyParsesIt()
            {
                Assert.Equal(
                    new RepositoryDescription(new Version(4, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    FromJson("{version:4, services:{}, mirrors: []}").Result);
            }

            // Invalid JSON

            [Fact]
            public void GivenANullVersion_ItUsesVersion0()
            {
                InvalidJsonTest(
                    "{version:null}",
                    new RepositoryDescription(new Version(0, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_MissingExpectedProperty, "version"));
            }

            [Fact]
            public void GivenAnEmptyJObject_ItReturnsANuGetDescriptionWithNoServicesOrMirrorsAndVersion0()
            {
                InvalidJsonTest(
                    "{}",
                    new RepositoryDescription(new Version(0, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_MissingExpectedProperty, "version"));
            }

            [Theory]
            [InlineData("42")]
            [InlineData("{}")]
            [InlineData("[]")]
            [InlineData("null")]
            [InlineData("true")]
            public void GivenAnInvalidMirror_ItIsTracedAndIgnored(string invalidUrlJson)
            {
                string json = @"{
                    version: 3,
                    services:{},
                    mirrors:[
                        ""http://api.nuget.org/"", 
                        " + invalidUrlJson + @", 
                        ""http://static-api.nuget.org/""
                    ]
                }";

                InvalidJsonTest(
                    json,
                    new RepositoryDescription(
                        new Version(3, 0), 
                        new [] {
                            new Uri("http://api.nuget.org/"),
                            new Uri("http://static-api.nuget.org/")
                        }, Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidMirrorUrl, invalidUrlJson));
            }

            [Theory]
            [InlineData("42")]
            [InlineData("{}")]
            [InlineData("[]")]
            [InlineData("null")]
            [InlineData("true")]
            public void GivenAnInvalidServiceUrl_ItIsTracedAndIgnored(string invalidNameJson)
            {
                string json = @"{
                    version: 3,
                    mirrors:[],
                    services: {
                        search: ""/search"", 
                        invalid: " + invalidNameJson + @", 
                        v2feed: ""/v2feed""
                    }
                }";

                InvalidJsonTest(
                    json,
                    new RepositoryDescription(
                        new Version(3, 0),
                        Enumerable.Empty<Uri>(), 
                        new [] {
                            new ServiceDescription("search", new Uri("/search", UriKind.Relative)),
                            new ServiceDescription("v2feed", new Uri("/v2feed", UriKind.Relative))
                        }),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidServiceUrl, invalidNameJson, "invalid"));
            }

            [Theory]
            [InlineData("{version:\"test\", services: {}, mirrors: []}", "test")]
            [InlineData("{version:{}, services: {}, mirrors: []}", "{}")]
            [InlineData("{version:-2, services: {}, mirrors: []}", "-2")]
            [InlineData("{version:[], services: {}, mirrors: []}", "[]")]
            [InlineData("{version:true, services: {}, mirrors: []}", "true")]
            public void GivenANonIntegerVersion_ItReportsWarningAndUses0AsVersion(string json, string actualVersion)
            {
                InvalidJsonTest(
                    json, 
                    new RepositoryDescription(new Version(0, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidVersion, actualVersion));
            }

            [Theory]
            [InlineData("{version: 3, services: {}, mirrors:\"test\"}", "test")]
            [InlineData("{version: 3, services: {}, mirrors:{}}", "{}")]
            [InlineData("{version: 3, services: {}, mirrors:42}", "42")]
            [InlineData("{version: 3, services: {}, mirrors:true}", "true")]
            public void GivenANonArrayMirrorsProperty_ItReportsWarningAndHasEmptyMirrors(string json, string actualMirrors)
            {
                InvalidJsonTest(
                    json,
                    new RepositoryDescription(new Version(3, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidMirrors, actualMirrors));
            }

            [Theory]
            [InlineData("{version: 3, mirrors: [], services:\"test\"}", "test")]
            [InlineData("{version: 3, mirrors: [], services:[]}", "[]")]
            [InlineData("{version: 3, mirrors: [], services:42}", "42")]
            [InlineData("{version: 3, mirrors: [], services:true}", "true")]
            public void GivenANonObjectServicesProperty_ItReportsWarningAndHasEmptyServices(string json, string actualServices)
            {
                InvalidJsonTest(
                    json,
                    new RepositoryDescription(new Version(3, 0), Enumerable.Empty<Uri>(), Enumerable.Empty<ServiceDescription>()),
                    String.Format(CultureInfo.CurrentCulture, Strings.RepositoryDescription_InvalidServices, actualServices));
            }

            private void InvalidJsonTest(string json, RepositoryDescription expected, params string[] warnings)
            {
                var result = FromJson(json, expectWarnings: true);
                Assert.Equal(expected, result.Result);

                foreach (var warning in warnings)
                {
                    // Find the matching event
                    var evt = result.FindEvent("JsonParseWarning", new { warning });
                    Assert.NotNull(evt);
                    
                    // Ensure there is a JToken
                    Assert.NotNull(evt.Payload["token"]);
                }
            }

            private ParseTestResult<RepositoryDescription> FromJson(string json, bool expectWarnings = false)
            {
                return FromJson(json, documentRoot: null, expectWarnings: expectWarnings);
            }

            private ParseTestResult<RepositoryDescription> FromJson(string json, Uri documentRoot, bool expectWarnings = false)
            {
                var sink = new CapturingTraceSink();
                var result = RepositoryDescription.FromJson(
                    JObject.Parse(json),
                    new Tracer("test", sink),
                    documentRoot);
                var ret = new ParseTestResult<RepositoryDescription>(sink.Events, result);
                if (!expectWarnings)
                {
                    Assert.Empty(ret.GetEventsByMethod("JsonParseWarning"));
                }
                return ret;
            }

            private class ParseTestResult<T>
            {
                public IReadOnlyList<TraceSinkEvent> TraceEvents { get; private set; }
                public T Result { get; private set; }

                public ParseTestResult(IReadOnlyList<TraceSinkEvent> traceEvents, T result)
                {
                    TraceEvents = traceEvents;
                    Result = result;
                }

                public IEnumerable<TraceSinkEvent> GetEventsByMethod(string method)
                {
                    return TraceEvents.Where(evt => String.Equals(evt.Name, method, StringComparison.OrdinalIgnoreCase));
                }

                public TraceSinkEvent FindEvent(string method, object parameters)
                {
                    var expectedPayload = Utils.ObjectToDictionary(parameters);
                    return TraceEvents.SingleOrDefault(evt =>
                        String.Equals(evt.Name, method, StringComparison.OrdinalIgnoreCase) &&

                        // All expected keys must be present and equal.
                        expectedPayload.All(pair => evt.Payload.ContainsKey(pair.Key) && Equals(evt.Payload[pair.Key], expectedPayload[pair.Key])));
                }

                
            }
        }
    }
}
