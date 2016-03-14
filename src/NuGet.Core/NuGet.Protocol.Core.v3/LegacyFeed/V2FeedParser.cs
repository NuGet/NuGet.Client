﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Logging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Protocol.Core.v3;
using NuGet.Versioning;

namespace NuGet.Protocol
{
    /// <summary>
    /// A light weight XML parser for NuGet V2 Feeds
    /// </summary>
    public sealed class V2FeedParser
    {
        private const string W3Atom = "http://www.w3.org/2005/Atom";
        private const string MetadataNS = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";
        private const string DataServicesNS = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        private const string FindPackagesByIdFormat = "/FindPackagesById()?Id='{0}'";
        private const string SearchEndPointFormat = "/Search()?$filter={0}&searchTerm='{1}'&targetFramework='{2}'&includePrerelease={3}&$skip={4}&$top={5}";
        private const string GetPackagesFormat = "/Packages(Id='{0}',Version='{1}')";
        private const string IsLatestVersionFilterFlag = "IsLatestVersion";
        private const string IsAbsoluteLatestVersionFilterFlag = "IsAbsoluteLatestVersion";

        // XNames used in the feed
        private static readonly XName _xnameEntry = XName.Get("entry", W3Atom);
        private static readonly XName _xnameTitle = XName.Get("title", W3Atom);
        private static readonly XName _xnameContent = XName.Get("content", W3Atom);
        private static readonly XName _xnameLink = XName.Get("link", W3Atom);
        private static readonly XName _xnameProperties = XName.Get("properties", MetadataNS);
        private static readonly XName _xnameId = XName.Get("Id", DataServicesNS);
        private static readonly XName _xnameVersion = XName.Get("Version", DataServicesNS);
        private static readonly XName _xnameSummary = XName.Get("summary", W3Atom);
        private static readonly XName _xnameDescription = XName.Get("Description", DataServicesNS);
        private static readonly XName _xnameIconUrl = XName.Get("IconUrl", DataServicesNS);
        private static readonly XName _xnameLicenseUrl = XName.Get("LicenseUrl", DataServicesNS);
        private static readonly XName _xnameProjectUrl = XName.Get("ProjectUrl", DataServicesNS);
        private static readonly XName _xnameTags = XName.Get("Tags", DataServicesNS);
        private static readonly XName _xnameReportAbuseUrl = XName.Get("ReportAbuseUrl", DataServicesNS);
        private static readonly XName _xnameDependencies = XName.Get("Dependencies", DataServicesNS);
        private static readonly XName _xnameRequireLicenseAcceptance = XName.Get("RequireLicenseAcceptance", DataServicesNS);
        private static readonly XName _xnameDownloadCount = XName.Get("DownloadCount", DataServicesNS);
        private static readonly XName _xnamePublished = XName.Get("Published", DataServicesNS);
        private static readonly XName _xnameName = XName.Get("name", W3Atom);
        private static readonly XName _xnameAuthor = XName.Get("author", W3Atom);
        private static readonly XName _xnamePackageHash = XName.Get("PackageHash", DataServicesNS);
        private static readonly XName _xnamePackageHashAlgorithm = XName.Get("PackageHashAlgorithm", DataServicesNS);
        private static readonly XName _xnameMinClientVersion = XName.Get("MinClientVersion", DataServicesNS);

        private readonly HttpSource _httpSource;
        private readonly PackageSource _source;
        private readonly string _findPackagesByIdFormat;
        private readonly string _searchEndPointFormat;
        private readonly string _getPackagesFormat;

        public PackageSource Source { get { return _source; } }

        public V2FeedParser(HttpSource httpSource, string sourceUrl)
            : this(httpSource, new PackageSource(sourceUrl))
        {
        }

        /// <summary>
        /// Creates a V2 parser
        /// </summary>
        /// <param name="httpHandler">Message handler containing auth/proxy support</param>
        /// <param name="source">endpoint source</param>
        public V2FeedParser(HttpSource httpSource, PackageSource source)
        {
            if (httpSource == null)
            {
                throw new ArgumentNullException(nameof(httpSource));
            }

            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            _httpSource = httpSource;
            _source = source;
            _findPackagesByIdFormat = source.Source.TrimEnd('/') + FindPackagesByIdFormat;
            _searchEndPointFormat = source.Source.TrimEnd('/') + SearchEndPointFormat;
            _getPackagesFormat = source.Source.TrimEnd('/') + GetPackagesFormat;
        }

        /// <summary>
        /// Get an exact package
        /// </summary>
        public async Task<V2FeedPackageInfo> GetPackage(
            PackageIdentity package,
            ILogger log,
            CancellationToken token)
        {
            if (package == null)
            {
                throw new ArgumentException(nameof(PackageIdentity));
            }

            if (!package.HasVersion)
            {
                throw new ArgumentException(nameof(package.Version));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var uri = String.Format(
                CultureInfo.InvariantCulture,
                _getPackagesFormat,
                package.Id,
                package.Version.ToNormalizedString());

            // Try to find the package directly
            // Set max count to -1, get all packages 
            var packages = await QueryV2Feed(uri, package.Id, -1, log, token);

            // If not found use FindPackagesById
            if (packages.Count < 1)
            {
                var allPackages = await FindPackagesByIdAsync(package.Id, log, token);

                return packages.Where(p => p.Version == package.Version)
                    .FirstOrDefault();
            }

            return packages.FirstOrDefault();
        }

        /// <summary>
        /// Retrieves all packages with the given Id from a V2 feed.
        /// </summary>
        public async Task<IReadOnlyList<V2FeedPackageInfo>> FindPackagesByIdAsync(
            string id,
            bool includeUnlisted,
            bool includePrerelease,
            ILogger log,
            CancellationToken token)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException(nameof(id));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            var uri = string.Format(CultureInfo.InvariantCulture, _findPackagesByIdFormat, id);
            // Set max count to -1, get all packages
            var packages = await QueryV2Feed(uri, id, -1, log, token);

            var filtered = packages.Where(p => (includeUnlisted || p.IsListed)
                && (includePrerelease || !p.Version.IsPrerelease));

            return filtered.OrderByDescending(p => p.Version).Distinct().ToList();
        }

        /// <summary>
        /// Retrieves all packages with the given Id from a V2 feed.
        /// </summary>
        public Task<IReadOnlyList<V2FeedPackageInfo>> FindPackagesByIdAsync(string id, ILogger log, CancellationToken token)
        {
            return FindPackagesByIdAsync(id, includeUnlisted: true, includePrerelease: true, log: log, token: token);
        }

        public async Task<IReadOnlyList<V2FeedPackageInfo>> Search(string searchTerm, SearchFilter filters, int skip, int take, ILogger log, CancellationToken cancellationToken)
        {
            var targetFramework = String.Join(@"/", filters.SupportedFrameworks);
            var uri = String.Format(CultureInfo.InvariantCulture, _searchEndPointFormat,
                                    filters.IncludePrerelease ? IsAbsoluteLatestVersionFilterFlag : IsLatestVersionFilterFlag,
                                    searchTerm,
                                    targetFramework,
                                    filters.IncludePrerelease.ToString().ToLowerInvariant(),
                                    skip,
                                    take);

            return await QueryV2Feed(uri, null, take, log, cancellationToken);
        }

        public async Task<DownloadResourceResult> DownloadFromUrl(PackageIdentity package,
            Uri downloadUri,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            return await GetDownloadResultUtility.GetDownloadResultAsync(_httpSource, package, downloadUri, settings, log, token);
        }

        public async Task<DownloadResourceResult> DownloadFromIdentity(PackageIdentity package,
            ISettings settings,
            ILogger log,
            CancellationToken token)
        {
            var packageInfo = await GetPackage(package, log, token);

            if (packageInfo == null)
            {
                return new DownloadResourceResult(DownloadResourceResultStatus.NotFound);
            }

            return await GetDownloadResultUtility.GetDownloadResultAsync(_httpSource, package, new Uri(packageInfo.DownloadUrl), settings, log, token);
        }

        /// <summary>
        /// Finds all entries on the page and parses them
        /// </summary>
        private IEnumerable<V2FeedPackageInfo> ParsePage(XDocument doc, string id)
        {
            if (doc.Root.Name == _xnameEntry)
            {
                return new List<V2FeedPackageInfo> { ParsePackage(id, doc.Root) };
            }
            else
            {
                return doc.Root.Elements(_xnameEntry)
                    .Select(x => ParsePackage(id, x));
            }
        }

        /// <summary>
        /// Parse an entry into a V2FeedPackageInfo
        /// </summary>
        private V2FeedPackageInfo ParsePackage(string id, XElement element)
        {
            var properties = element.Element(_xnameProperties);
            var idElement = properties.Element(_xnameId);
            var titleElement = element.Element(_xnameTitle);

            // If 'Id' element exist, use its value as accurate package Id
            // Otherwise, use the value of 'title' if it exist
            // Use the given Id as final fallback if all elements above don't exist
            string identityId = idElement?.Value ?? titleElement?.Value ?? id;
            string versionString = properties.Element(_xnameVersion).Value;
            NuGetVersion version = NuGetVersion.Parse(versionString);
            string downloadUrl = element.Element(_xnameContent).Attribute("src").Value;

            string title = titleElement?.Value;
            string summary = GetValue(element, _xnameSummary);
            string description = GetValue(properties, _xnameDescription);
            string iconUrl = GetValue(properties, _xnameIconUrl);
            string licenseUrl = GetValue(properties, _xnameLicenseUrl);
            string projectUrl = GetValue(properties, _xnameProjectUrl);
            string reportAbuseUrl = GetValue(properties, _xnameReportAbuseUrl);
            string tags = GetValue(properties, _xnameTags);
            string dependencies = GetValue(properties, _xnameDependencies);

            string downloadCount = GetValue(properties, _xnameDownloadCount);
            bool requireLicenseAcceptance = GetValue(properties, _xnameRequireLicenseAcceptance) == "true";

            string packageHash = GetValue(properties, _xnamePackageHash);
            string packageHashAlgorithm = GetValue(properties, _xnamePackageHashAlgorithm);
            string minClientVersionString = GetValue(properties, _xnameMinClientVersion);

            NuGetVersion minClientVersion = null;

            if (minClientVersionString != null)
            {
                NuGetVersion.TryParse(minClientVersionString, out minClientVersion);
            }

            DateTimeOffset? published = null;

            DateTimeOffset pubVal = DateTimeOffset.MinValue;
            string pubString = GetValue(properties, _xnamePublished);
            if (DateTimeOffset.TryParse(pubString, out pubVal))
            {
                published = pubVal;
            }

            // TODO: is this ever populated in v2?
            IEnumerable<string> owners = null;

            IEnumerable<string> authors = null;

            var authorNode = element.Element(_xnameAuthor);
            if (authorNode != null)
            {
                authors = authorNode.Elements(_xnameName).Select(e => e.Value);
            }

            return new V2FeedPackageInfo(new PackageIdentity(identityId, version),
                title, summary, description, authors, owners, iconUrl, licenseUrl,
                projectUrl, reportAbuseUrl, tags, published, dependencies,
                requireLicenseAcceptance, downloadUrl, downloadCount,
                packageHash,
                packageHashAlgorithm,
                minClientVersion
                );
        }

        /// <summary>
        /// Retrieve an XML value safely
        /// </summary>
        private static string GetValue(XElement parent, XName childName)
        {
            string value = null;

            if (parent != null)
            {
                XElement child = parent.Element(childName);

                if (child != null)
                {
                    value = child.Value;
                }
            }

            return value;
        }

        private async Task<List<V2FeedPackageInfo>> QueryV2Feed(string uri, string id, int max, ILogger log, CancellationToken token)
        {
            var results = new List<V2FeedPackageInfo>();
            var page = 1;

            // first request
            Task<HttpResponseMessage> urlRequest = _httpSource.GetAsync(new Uri(uri), log, token);

            // TODO: re-implement caching at a higher level for both v2 and v3
            while (!token.IsCancellationRequested && urlRequest != null)
            {
                // TODO: Pages for a package Id are cached separately.
                // So we will get inaccurate data when a page shrinks.
                // However, (1) In most cases the pages grow rather than shrink;
                // (2) cache for pages is valid for only 30 min.
                // So we decide to leave current logic and observe.
                using (var data = await urlRequest)
                {
                    try
                    {
                        var doc = await _httpSource.DownloadUtility.BufferAndProcessAsync(
                            source: await data.Content.ReadAsStreamAsync(),
                            process: stream => Task.FromResult(LoadXml(stream)),
                            downloadName: uri,
                            token: token);

                        // find results on the page
                        var result = ParsePage(doc, id);
                        results.AddRange(result);

                        var nextUri = GetNextUrl(doc);

                        urlRequest = null;
                        if (max < 0 || results.Count < max)
                        {
                            // Request the next url in parallel to parsing the current page
                            if (!string.IsNullOrEmpty(nextUri) && uri != nextUri)
                            {
                                // a bug on the server side causes the same next link to be returned 
                                // for every page. To avoid falling into an infinite loop we must
                                // keep track here.
                                uri = nextUri;

                                urlRequest = _httpSource.GetAsync(new Uri(nextUri), log, token);
                            }

                            page++;
                        }
                    }
                    catch (XmlException ex)
                    {
                        log.LogVerbose(ex.ToString());
                        throw;
                    }
                }
            }

            if (max > -1 && results.Count > max)
            {
                // Remove extra results if the page contained extras
                results = results.Take(max).ToList();
            }

            return results;
        }

        internal static string GetNextUrl(XDocument doc)
        {
            // Example of what this looks like in the odata feed:
            // <link rel="next" href="{nextLink}" />
            return (from e in doc.Root.Elements(_xnameLink)
                    let attr = e.Attribute("rel")
                    where attr != null && string.Equals(attr.Value, "next", StringComparison.OrdinalIgnoreCase)
                    select e.Attribute("href") into nextLink
                    where nextLink != null
                    select nextLink.Value).FirstOrDefault();
        }

        internal static XDocument LoadXml(Stream stream)
        {
            var xmlReader = XmlReader.Create(stream, new XmlReaderSettings()
            {
                CloseInput = true,
                IgnoreWhitespace = true,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true
            });

            return XDocument.Load(xmlReader, LoadOptions.None);
        }
    }
}
