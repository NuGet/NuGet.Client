using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace NuGet.CommandLine.Test.Caching
{
    public class MockResponseBuilder
    {
        private readonly string _baseUrl;

        public MockResponseBuilder(string baseUrl)
        {
            _baseUrl = baseUrl;
        }

        public string GetDownloadPath(PackageIdentity identity)
        {
            var id = identity.Id.ToLowerInvariant();
            var version = identity.Version.ToNormalizedString().ToLowerInvariant();

            return $"/packages/{id}.{version}.nupkg";
        }

        public string GetFlatDownloadPath(PackageIdentity identity)
        {
            var id = identity.Id.ToLowerInvariant();
            var version = identity.Version.ToNormalizedString().ToLowerInvariant();

            return $"/flat/{id}/{version}/{id}.{version}.nupkg";
        }

        public string GetDownloadUrl(PackageIdentity identity)
        {
            return _baseUrl + GetDownloadPath(identity);
        }

        public string GetODataPath(PackageIdentity identity)
        {
            var id = identity.Id;
            var version = identity.Version.ToNormalizedString();

            return $"/nuget/Packages(Id='{id}',Version='{version}')";
        }

        public string GetFindPackagesByIdPath(string id)
        {
            return $"/nuget/FindPackagesById()?id='{id}'";
        }

        public string GetODataUrl(PackageIdentity identity)
        {
            return _baseUrl + GetODataPath(identity);
        }

        public string GetRegistrationIndexPath(string id)
        {
            return $"/reg/{id.ToLowerInvariant()}/index.json";
        }

        public string GetFlatIndexPath(string id)
        {
            return $"/flat/{id.ToLowerInvariant()}/index.json";
        }

        public string GetV3IndexPath()
        {
            return "/index.json";
        }

        public string GetV2IndexPath()
        {
            return "/nuget";
        }

        public string GetV3Source()
        {
            return _baseUrl + GetV3IndexPath();
        }

        public string GetV2Source()
        {
            return _baseUrl + GetV2IndexPath();
        }

        public MockResponse BuildODataResponse(string packagePath)
        {
            var entry = GetODataElement(packagePath);
            var document = new XDocument(entry);

            return new MockResponse
            {
                ContentType = "application/atom+xml;type=entry;charset=utf-8",
                Content = Encoding.UTF8.GetBytes(document.ToString())
            };
        }
        
        public MockResponse BuildFindPackagesByIdResponse(IEnumerable<string> packagePaths)
        {
            return BuildODataFeedResponse("FindPackagesById", packagePaths);
        }

        private MockResponse BuildODataFeedResponse(string title, IEnumerable<string> packagePaths)
        {
            var nsAtom = "http://www.w3.org/2005/Atom";
            var feedId = $"{_baseUrl}/nuget/{title}";
            var document = new XDocument(
                new XElement(XName.Get("feed", nsAtom),
                    new XElement(XName.Get("id", nsAtom), feedId),
                    new XElement(XName.Get("title", nsAtom), title)));

            foreach(var packagePath in packagePaths)
            {
                document.Root.Add(GetODataElement(packagePath));
            }

            return new MockResponse
            {
                ContentType = "application/atom+xml;type=feed;charset=utf-8",
                Content = Encoding.UTF8.GetBytes(document.ToString())
            };
        }

        private XElement GetODataElement(string packagePath)
        {
            using (var packageArchiveReader = new PackageArchiveReader(packagePath))
            {
                var nuspec = packageArchiveReader.NuspecReader;
                var identity = nuspec.GetIdentity();
                var id = identity.Id;
                var version = identity.Version.ToNormalizedString();
                var hash = GetHash(packagePath);
                var description = nuspec.GetDescription();
                var listed = true;

                var downloadUrl = GetDownloadUrl(identity);
                var entryId = GetODataUrl(identity);

                var nsAtom = "http://www.w3.org/2005/Atom";
                XNamespace nsDataService = "http://schemas.microsoft.com/ado/2007/08/dataservices";
                var nsMetadata = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

                var content = new XElement(XName.Get("content", nsAtom),
                        new XAttribute("type", "application/zip"),
                        new XAttribute("src", downloadUrl));

                var properties = new XElement(XName.Get("properties", nsMetadata),
                    new XElement(nsDataService + "Version", version),
                    new XElement(nsDataService + "PackageHash", hash),
                    new XElement(nsDataService + "PackageHashAlgorithm", "SHA512"),
                    new XElement(nsDataService + "Description", description),
                    new XElement(nsDataService + "Listed", listed));

                var entry = new XElement(XName.Get("entry", nsAtom),
                    new XAttribute(XNamespace.Xmlns + "d", nsDataService),
                    new XAttribute(XNamespace.Xmlns + "m", nsMetadata),
                    new XElement(XName.Get("id", nsAtom), entryId),
                    new XElement(XName.Get("title", nsAtom), id),
                    content,
                    properties);

                return entry;
            }
        }

        public MockResponse BuildRegistrationIndexResponse(MockServer mockServer, PackageIdentity identity)
        {
            var id = identity.Id.ToLowerInvariant();
            var version = identity.Version.ToNormalizedString().ToLowerInvariant();

            var registrationIndex = Util.CreateSinglePackageRegistrationBlob(mockServer, id, version);

            return new MockResponse
            {
                ContentType = "text/javascript",
                Content = Encoding.UTF8.GetBytes(registrationIndex.ToString())
            };
        }

        public MockResponse BuildFlatIndex(NuGetVersion version)
        {
            var flatIndex = JsonConvert.SerializeObject(new
            {
                versions = new[]
                {
                    version.ToNormalizedString()
                }
            });

            return new MockResponse
            {
                ContentType = "text/javascript",
                Content = Encoding.UTF8.GetBytes(flatIndex.ToString())
            };
        }

        public MockResponse BuildV3IndexResponse(MockServer mockServer)
        {
            var indexJson = Util.CreateIndexJson();

            Util.AddFlatContainerResource(indexJson, mockServer);
            Util.AddRegistrationResource(indexJson, mockServer);

            return new MockResponse
            {
                ContentType = "text/javascript",
                Content = Encoding.UTF8.GetBytes(indexJson.ToString())
            };
        }

        public MockResponse BuildV2IndexResponse(MockServer mockServer)
        {
            return new MockResponse
            {
                ContentType = null,
                Content = new byte[0]
            };
        }

        public MockResponse BuildDownloadResponse(string packagePath)
        {
            return new MockResponse
            {
                ContentType = "application/zip",
                Content = File.ReadAllBytes(packagePath)
            };
        }

        private static string GetHash(string packagePath)
        {
            using (var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read))
            {
                using (var sha512 = SHA512.Create())
                {
                    return Convert.ToBase64String(sha512.ComputeHash(stream));
                }
            }
        }
    }
}
