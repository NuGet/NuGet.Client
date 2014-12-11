using JsonLD.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Data
{
    public class DataClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly FileCacheBase _fileCache;
        private readonly EntityCache _entityCache;
        private bool _disposed;
        private static readonly TimeSpan _lifeSpan = TimeSpan.FromMinutes(5);
        private readonly static TimeSpan _defaultCacheLife = TimeSpan.FromHours(2);

        /// <summary>
        /// DataClient with the default options.
        /// </summary>
        public DataClient()
            : this(new CacheHttpClient(), new BrowserFileCache())
        {

        }

        /// <summary>
        /// DataClient
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="fileCache"></param>
        /// <param name="context"></param>
        public DataClient(HttpClient httpClient, FileCacheBase fileCache)
        {
            if (httpClient == null)
            {
                throw new ArgumentNullException("httpClient");
            }

            if (fileCache == null)
            {
                throw new ArgumentNullException("fileCache");
            }

            _httpClient = httpClient;
            _fileCache = fileCache;
            _entityCache = new EntityCache();
        }


        /// <summary>
        /// Retrieves a url with caching.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<JObject> GetFile(Uri uri)
        {
            return await GetFile(uri, _defaultCacheLife, true);
        }

        /// <summary>
        /// Retrieves a url with no caching.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public async Task<JObject> GetFileNoCache(Uri uri)
        {
            return await GetFile(uri, TimeSpan.MinValue, false);
        }


        /// <summary>
        /// Retrieves a url and returns it as it is.
        /// </summary>
        /// <param name="uri">http uri</param>
        /// <param name="cacheTime">cache life</param>
        /// <param name="cacheInGraph">add this file to the entity cache</param>
        /// <returns>the unmodified json at the url</returns>
        public async Task<JObject> GetFile(Uri uri, TimeSpan cacheTime, bool cacheInGraph = true)
        {
            return await GetFileInternal(uri, cacheTime, cacheInGraph, true);
        }


        private async Task<JObject> GetFileInternal(Uri uri, TimeSpan cacheTime, bool cacheInGraph=true, bool cloneJson=true)
        {
            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            if (cacheTime == null)
            {
                throw new ArgumentNullException("cacheTime");
            }

            bool cache = cacheTime.TotalSeconds > 0;

            // request the root document
            Uri fixedUri = Utility.GetUriWithoutHash(uri);

            Stream stream = null;
            JObject result = null;
            JObject clonedResult = null; // the copy we give the caller

            try
            {
                using (var uriLock = new UriLock(fixedUri))
                {
                    if (!cache || !_fileCache.TryGet(fixedUri, out stream))
                    {
                        // the stream was not in the cache or we are skipping the cache
                        int tries = 0;

                        // try up to 5 times to be a little more robust
                        while (stream == null && tries < 5)
                        {
                            tries++;

                            try
                            {
                                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, fixedUri.AbsoluteUri);

                                DataTraceSources.Verbose("[HttpClient] GET {0}", fixedUri.AbsoluteUri);

                                var response = await _httpClient.SendAsync(request);

                                Debug.Assert(response.StatusCode == HttpStatusCode.OK, "Received non-OK status code response from " + request.RequestUri.ToString());
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    stream = await response.Content.ReadAsStreamAsync();

                                    if (stream != null)
                                    {
                                        if (cache)
                                        {
                                            DataTraceSources.Verbose("[HttpClient] Caching {0}");
                                            _fileCache.Add(fixedUri, _lifeSpan, stream);
                                        }

                                        DataTraceSources.Verbose("[HttpClient] 200 OK Length: {0}", "" + stream.Length);
                                        result = await StreamToJson(stream);
                                    }
                                }
                                else
                                {
                                    DataTraceSources.Verbose("[HttpClient] FAILED {0}", "" + (int)response.StatusCode);
                                    result = new JObject();
                                    result.Add("HttpStatusCode", (int)response.StatusCode);
                                }
                            }
                            catch (HttpRequestException ex)
                            {
                                Debug.Fail("WebRequest failed: " + ex.ToString());
                                DataTraceSources.Verbose("[HttpClient] FAILED {0}", ex.ToString());

                                // request error
                                result = new JObject();
                                result.Add("HttpRequestException", ex.ToString());
                            }
                        }
                    }
                    else
                    {
                        // the stream was in the cache
                        DataTraceSources.Verbose("[HttpClient] Cached Length: {0}", "" + stream.Length);
                        result = await StreamToJson(stream);
                    }
                }
            }
            finally
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            if (result != null)
            {
                // this must be called before the entity cache thread starts using it
                if (cloneJson)
                {
                    clonedResult = result.DeepClone() as JObject;
                }
                else
                {
                    // in some scenarios we can skip cloning, such as when we are throwing away the result
                    clonedResult = result;
                }

                if (cacheInGraph)
                {
                    // this call is only blocking if the cache is overloaded
                    _entityCache.Add(result, fixedUri);
                }
            }

            return clonedResult;
        }

        /// <summary>
        /// Returns a JToken for the given entity.
        /// </summary>
        /// <param name="entity">JToken @id</param>
        /// <returns>The entity Json</returns>
        public async Task<JToken> GetEntity(Uri entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            JToken token = await _entityCache.GetEntity(entity);

            if (token == null)
            {
                // we don't have any info on the given entity, try downloading it
                await EnsureFile(entity);

                // request the entity again
                token = await _entityCache.GetEntity(entity);

                if (token == null)
                {
                    DataTraceSources.Verbose("[EntityCache] Unable to get entity {0}", entity.AbsoluteUri);
                    Debug.Fail("Unable to get entity");
                }
            }

            if (token != null)
            {
                // clone our cache copy
                token = token.DeepClone();
            }

            return token;
        }

        /// <summary>
        /// Returns the JToken associated with the entityUri. If the given properties do not exist 
        /// in the cache the needed pages WILL be fetched.
        /// </summary>
        /// <remarks>includes fetch</remarks>
        /// <param name="entityUri">@id of the JToken</param>
        /// <param name="properties">predicates uris</param>
        /// <returns></returns>
        public async Task<JToken> Ensure(Uri entityUri, IEnumerable<Uri> properties)
        {
            if (entityUri == null)
            {
                throw new ArgumentNullException("entityUri");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            return await GetEntityHelper(null, entityUri, properties);
        }

        /// <summary>
        /// Ensures that the given properties are on the JToken. If they are not inlined they will be fetched.
        /// Other data may appear in the returned JToken, but the root level will stay the same.
        /// </summary>
        /// <param name="jToken">The JToken to expand. This should have an @id.</param>
        /// <param name="properties">Expanded form properties that are needed on JToken.</param>
        /// <returns>The same JToken if it already exists, otherwise the fetched JToken.</returns>
        public async Task<JToken> Ensure(JToken token, IEnumerable<Uri> properties)
        {
            if (token == null)
            {
                throw new ArgumentNullException("token");
            }

            if (properties == null)
            {
                throw new ArgumentNullException("properties");
            }

            JObject jObject = token as JObject;

            if (jObject != null)
            {
                CompactEntityReader compactEntity = new CompactEntityReader(jObject);

                // if the entity is found on it's originating page we know it is already complete in this compact form
                if (compactEntity.IsFromPage == false)
                {
                    if (compactEntity.EntityUri != null)
                    {
                        // inspect the compact entity on a basic level to determine if it already has the properties it asked for
                        if (compactEntity.HasPredicates(properties) != true)
                        {
                            // at this point we know the compact token does not include the needed properties,
                            // we need to either download the file it lives on, or find it in the entity cache
                            // if the token is for an entity that just does not exist or is corrupted in some way
                            // the original token will be returned since the entity cache cannot improve it after
                            // trying all possible methods.
                            return await GetEntityHelper(token, compactEntity.EntityUri, properties);
                        }
                    }
                    else
                    {
                        DataTraceSources.Verbose("[EntityCache] Unable to find entity @id!");
                    }
                }
            }
            else if (token.Type == JTokenType.String)
            {
                // It's just a URL, so we definitely need to fetch it from the cache
                string tokenString = token.ToString();

                // make sure it is a url
                if (!String.IsNullOrEmpty(tokenString) && tokenString.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    Uri entityUrl = new Uri(tokenString);

                    // the entity cache should either find the child entity or if this url is a root url the full page will be returned
                    return await GetEntityHelper(token, entityUrl, properties);
                }
            }
            else
            {
                DataTraceSources.Verbose("[EntityCache] Non-JObject, unable to use this!");
            }

            // give the original token back
            return token;
        }

        /// <summary>
        /// Retrieves the best match from the entity cache. If no matches exist this will attempt to download the file.
        /// </summary>
        /// <remarks>returns a cloned copy</remarks>
        /// <returns>The entity cache result or the original token if it cannot be improved.</returns>
        private async Task<JToken> GetEntityHelper(JToken originalToken, Uri entity, IEnumerable<Uri> properties)
        {
            bool? fetch = await _entityCache.FetchNeeded(entity, properties);

            JToken token = originalToken;

            if (fetch == true)
            {
                // we are missing properties and do not have the page
                DataTraceSources.Verbose("[DataClient] GetFile required to Ensure {0}", entity.AbsoluteUri);
                await EnsureFile(entity);
            }

            // null means either there is no work to do, or that we gave up, return the original token here
            // if the original token is null, meaning this came from Ensure(uri, uri[]) then we need to get the token from the cache
            if (fetch != null || originalToken == null)
            {
                JToken entityCacheResult = await _entityCache.GetEntity(entity);

                // If the entity cache is unable to improve the result, return the original
                if (entityCacheResult != null)
                {
                    token = entityCacheResult.DeepClone();
                }
            }

            return token;
        }

        /// <summary>
        /// Converts a json stream into a JObject.
        /// </summary>
        private async static Task<JObject> StreamToJson(Stream stream)
        {
            JObject jObj = null;
            string json = string.Empty;

            if (stream != null)
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);

                    using (var reader = new StreamReader(stream))
                    {
                        json = await reader.ReadToEndAsync();
                        jObj = JObject.Parse(json);
                    }
                } 
                catch (Exception ex)
                {
                    DataTraceSources.Verbose("[StreamToJson] Failed {0}", ex.ToString());
                    jObj = new JObject();
                    jObj.Add("raw", json);
                }
            }
            else
            {
                DataTraceSources.Verbose("[StreamToJson] Null stream!");
            }

            return jObj;
        }

        /// <summary>
        /// Internal helper for GetFile that avoids cloning.
        /// </summary>
        /// <returns></returns>
        private async Task EnsureFile(Uri uri)
        {
            if (!_entityCache.HasPageOfEntity(uri))
            {
                // cache the file but ignore the result
                await GetFileInternal(uri, _defaultCacheLife, true, false);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _entityCache.Dispose();
            }
        }
    }
}
