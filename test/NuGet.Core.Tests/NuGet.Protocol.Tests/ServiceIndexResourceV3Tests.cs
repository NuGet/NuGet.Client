// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Events;
using NuGet.Protocol.Model;
using NuGet.Protocol.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Protocol.Tests
{
    public class ServiceIndexResourceV3Tests
    {
        [Fact]
        public void Constructor_InitializesProperties()
        {
            var serviceIndex = CreateServiceIndex();
            var expectedJson = serviceIndex.ToString();
            var expectedRequestTime = DateTime.UtcNow;
            var resource = new ServiceIndexResourceV3(serviceIndex, expectedRequestTime);

            Assert.Equal(expectedJson, resource.Json);
            Assert.Equal(expectedRequestTime, resource.RequestTime);
            Assert.Equal(1, resource.Entries.Count);
            Assert.Equal("a", resource.Entries[0].Type);
            Assert.Equal("http://unit.test/b", resource.Entries[0].Uri.ToString());
        }

        [Fact]
        public void GetServiceEntries_InvokesDiagnosticEventForSourceResources()
        {
            // Arrange
            int eventInvokeCount = 0;
            List<ProtocolDiagnosticServiceIndexEntryEvent> capturedEvents = new List<ProtocolDiagnosticServiceIndexEntryEvent>();

            ProtocolDiagnostics.ServiceIndexEntryEvent += (pdEvent) =>
            {
                eventInvokeCount++;
                capturedEvents.Add(pdEvent);
            };

            var source = $"https://test/index.json";
            var content = CreateServiceIndexWithFourResourceTypesTwoHTTP();

            var expectedRequestTime = DateTime.UtcNow;
            var resource = new ServiceIndexResourceV3(content, expectedRequestTime, new Configuration.PackageSource(source));

            // Act
            var result = resource.GetServiceEntries(ServiceTypes.SearchQueryService);

            // Assert
            int httpResourceCapture = 0;

            foreach (var serviceIndexEvent in capturedEvents)
            {
                Assert.Equal(serviceIndexEvent.Source, source);
                httpResourceCapture += serviceIndexEvent.HttpsSourceHasHttpResource ? 1 : 0;
            }

            Assert.Equal(2, httpResourceCapture);
            Assert.Equal(2, eventInvokeCount);
        }

        private static JObject CreateServiceIndexWithFourResourceTypesTwoHTTP()
        {
            var obj = new JObject
            {
                { "version", "3.1.0-beta" },
                { "resources", new JArray
                    {
                        new JObject
                        {
                            { "@type", "SearchQueryService/Versioned" },
                            { "@id", "http://tempuri.org/A/5.0.0/2" },
                            { "clientVersion", "5.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "SearchQueryService/Versioned" },
                            { "@id", "http://tempuri.org/A/5.0.0/1" },
                            { "clientVersion", "5.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "SearchQueryService/Versioned" },
                            { "@id", "https://test" },
                            { "clientVersion", "4.0.0" },
                        },
                        new JObject
                        {
                            { "@type", "SearchQueryService/Versioned" },
                            { "@id", "https://test" },
                            { "clientVersion", "5.0.0" },
                        },
                    }
                }
            };

            return obj;
        }

        private static JObject CreateServiceIndex()
        {
            return new JObject
            {
                { "version", "1.2.3" },
                { "resources", new JArray
                    {
                        new JObject
                        {
                            { "@type", "a" },
                            { "@id", "http://unit.test/b" }
                        }
                    }
                }
            };
        }

        // STJ path: mirrors of the JObject-based tests above

        [Fact]
        public void Constructor_WithModel_InitializesProperties()
        {
            var model = DeserializeModel(@"{""version"":""1.2.3"",""resources"":[{""@id"":""http://contoso.test/b"",""@type"":""a""}]}");
            var expectedRequestTime = DateTime.UtcNow;

            var resource = new ServiceIndexResourceV3(model, expectedRequestTime, packageSource: null);

            Assert.Equal(expectedRequestTime, resource.RequestTime);
            Assert.Equal(1, resource.Entries.Count);
            Assert.Equal("a", resource.Entries[0].Type);
            Assert.Equal("http://contoso.test/b", resource.Entries[0].Uri.ToString());
        }

        [Fact]
        public void Constructor_WithModel_JsonProperty_RoundTrips()
        {
            var model = DeserializeModel(@"{""version"":""1.2.3"",""resources"":[{""@id"":""http://contoso.test/b"",""@type"":""a""}]}");

            var resource = new ServiceIndexResourceV3(model, DateTime.UtcNow, packageSource: null);

            // Json property re-serializes from the model; verify it round-trips to equivalent content.
            using var doc = JsonDocument.Parse(resource.Json);
            Assert.Equal("1.2.3", doc.RootElement.GetProperty("version").GetString());
            Assert.Equal(1, doc.RootElement.GetProperty("resources").GetArrayLength());
        }

        [Fact]
        public void Constructor_WithModel_TypeAsArray_ExpandsToEntryPerType()
        {
            var model = DeserializeModel(@"{""version"":""3.0.0"",""resources"":[{""@id"":""http://contoso.test/b"",""@type"":[""a"",""b""]}]}");

            var resource = new ServiceIndexResourceV3(model, DateTime.UtcNow, packageSource: null);

            Assert.Equal(2, resource.Entries.Count);
            Assert.Contains(resource.Entries, e => e.Type == "a");
            Assert.Contains(resource.Entries, e => e.Type == "b");
            Assert.All(resource.Entries, e => Assert.Equal("http://contoso.test/b", e.Uri.ToString()));
        }

        [Fact]
        public void Constructor_WithModel_ClientVersionAsArray_CreatesEntryPerVersion()
        {
            var model = DeserializeModel(@"{""version"":""3.0.0"",""resources"":[{""@id"":""http://contoso.test/b"",""@type"":""a"",""clientVersion"":[""4.0.0"",""5.0.0""]}]}");

            var resource = new ServiceIndexResourceV3(model, DateTime.UtcNow, packageSource: null);

            Assert.Equal(2, resource.Entries.Count);
            Assert.Contains(resource.Entries, e => e.ClientVersion == new SemanticVersion(4, 0, 0));
            Assert.Contains(resource.Entries, e => e.ClientVersion == new SemanticVersion(5, 0, 0));
        }

        [Fact]
        public void GetServiceEntries_WithModel_InvokesDiagnosticEventForHttpResourcesUnderHttpsSource()
        {
            int eventInvokeCount = 0;
            var capturedEvents = new List<ProtocolDiagnosticServiceIndexEntryEvent>();

            ProtocolDiagnostics.ServiceIndexEntryEvent += (pdEvent) =>
            {
                eventInvokeCount++;
                capturedEvents.Add(pdEvent);
            };

            var source = "https://contoso.test/index.json";
            var model = DeserializeModel(CreateServiceIndexJsonWithFourResourceTypesTwoHttp());

            var resource = new ServiceIndexResourceV3(model, DateTime.UtcNow, new Configuration.PackageSource(source));
            resource.GetServiceEntries(ServiceTypes.SearchQueryService);

            int httpResourceCapture = 0;
            foreach (var serviceIndexEvent in capturedEvents)
            {
                Assert.Equal(source, serviceIndexEvent.Source);
                httpResourceCapture += serviceIndexEvent.HttpsSourceHasHttpResource ? 1 : 0;
            }

            Assert.Equal(2, httpResourceCapture);
            Assert.Equal(2, eventInvokeCount);
        }

        private static ServiceIndexModel DeserializeModel(string json)
            => JsonSerializer.Deserialize(json, JsonContext.Default.ServiceIndexModel)!;

        private static string CreateServiceIndexJsonWithFourResourceTypesTwoHttp() => @"{
            ""version"": ""3.1.0-beta"",
            ""resources"": [
                { ""@type"": ""SearchQueryService/Versioned"", ""@id"": ""http://contoso.test/A/5.0.0/2"", ""clientVersion"": ""5.0.0"" },
                { ""@type"": ""SearchQueryService/Versioned"", ""@id"": ""http://contoso.test/A/5.0.0/1"", ""clientVersion"": ""5.0.0"" },
                { ""@type"": ""SearchQueryService/Versioned"", ""@id"": ""https://contoso.test"", ""clientVersion"": ""4.0.0"" },
                { ""@type"": ""SearchQueryService/Versioned"", ""@id"": ""https://contoso.test"", ""clientVersion"": ""5.0.0"" }
            ]}";
    }
}
