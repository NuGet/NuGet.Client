// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
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
        public void GetServiceEntries_WithResourceEndPoint_ThrowsException()
        {
            // Arrange
            var serviceIndex = CreateServiceIndexWithHttpResources();
            var resource = new ServiceIndexResourceV3(serviceIndex, DateTime.Now);

            // Act & Assert
            Assert.Throws<ArgumentException>(() => resource.GetServiceEntries("SearchQueryService"));
            Assert.Throws<ArgumentException>(() => resource.GetServiceEntries("RegistrationsBaseUrl"));
            Assert.Throws<ArgumentException>(() => resource.GetServiceEntries("LegacyGallery"));
        }

        [Fact]
        public void GetServiceEntries_WithHttpResourceEndPointAndAllowInsecureConnections_Succeeds()
        {
            // Arrange
            var serviceIndex = CreateServiceIndexWithHttpResources();
            var resource = new ServiceIndexResourceV3(serviceIndex, DateTime.Now);
            resource._allowInsecureConnections = true;

            // Act
            var searchRec = resource.GetServiceEntries("SearchQueryService").FirstOrDefault().Uri.ToString();
            var regRec = resource.GetServiceEntries("RegistrationsBaseUrl").FirstOrDefault().Uri.ToString();
            var legacyRec = resource.GetServiceEntries("LegacyGallery").FirstOrDefault().Uri.ToString();

            // Assert
            Assert.Equal(searchRec, "http://search/");
            Assert.Equal(regRec, "http://reg/");
            Assert.Equal(legacyRec, "http://legacy/");
        }

        [Fact]
        public void GetServiceEntries_RequestsHttpsResourceInServiceIndexContainingOtherHttpResourcesWithoutAllowInsecureConnections_Succeeds()
        {
            // Arrange
            var serviceIndex = CreateServiceIndexWithHttpResources();
            var resource = new ServiceIndexResourceV3(serviceIndex, DateTime.Now);

            // Act
            var vulnRec = resource.GetServiceEntries("VulnerabilityInfo/6.7.0").FirstOrDefault().Uri.ToString();

            // Assert
            Assert.Equal(vulnRec, "https://vulnerability/");
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

        private static JObject CreateServiceIndexWithHttpResources()
        {
            return new JObject
            {
                { "version", "1.2.3" },
                { "resources", new JArray
                    {
                        new JObject
                        {
                            { "@type", "SearchQueryService" },
                            { "@id", "http://search" }
                        },
                        new JObject
                        {
                            { "@type", "RegistrationsBaseUrl" },
                            { "@id", "http://reg" }
                        },
                        new JObject
                        {
                            { "@type", "LegacyGallery" },
                            { "@id", "http://legacy" }
                        },
                        new JObject
                        {
                            { "@type", "VulnerabilityInfo/6.7.0" },
                            { "@id", "https://vulnerability" }
                        }
                    }
                }
            };
        }
    }
}
