using JsonLD.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Data
{
    /// <summary>
    /// Thread safe cache of graphs.
    /// </summary>
    public class EntityCache : IDisposable
    {
        private JsonLdGraph _masterGraph;
        private readonly ConcurrentDictionary<Uri, JsonLdPage> _pages;
        private bool _disposed;
        private readonly System.Threading.Timer _tidyTimer;
        private readonly TimeSpan _cacheExpiration;

        public EntityCache()
            : this(TimeSpan.FromMinutes(5))
        {

        }

        public EntityCache(TimeSpan cacheExpiration)
        {
            _masterGraph = new JsonLdGraph();
            _pages = new ConcurrentDictionary<Uri, JsonLdPage>();
            _cacheExpiration = cacheExpiration;
            _tidyTimer = new System.Threading.Timer(TidyTimerTick, null, _cacheExpiration, _cacheExpiration);
        }

        /// <summary>
        /// True if the page has been added to the cache.
        /// </summary>
        public bool HasPageOfEntity(Uri entity)
        {
            bool result = false;
            Uri uri = Utility.GetUriWithoutHash(entity);

            JsonLdPage page = null;
            if (_pages.TryGetValue(uri, out page))
            {
                // make the page as accessed so it stays around
                page.UpdateLastUsed();
                result = !page.IsDisposed; // to be extra safe
            }

            return result;
        }

        /// <summary>
        /// A minimally blocking call that starts a background task to update the graph.
        /// </summary>
        /// <param name="compacted"></param>
        /// <param name="pageUri"></param>
        public void Add(JObject compacted, Uri pageUri)
        {
            Debug.Assert(pageUri.AbsoluteUri.IndexOf("#") == -1, "Add should be on the full Uri and not the child Uri!");

            JsonLdPage page = new JsonLdPage(pageUri, compacted);

            if (_pages.TryAdd(pageUri, page))
            {
                // start the graph load
                page.BeginLoad(AddCallback);
            }
            else
            {
                // clean up
                page.Dispose();
            }
        }

        /// <summary>
        /// Look up the entity in the cache.
        /// </summary>
        public async Task<JToken> GetEntity(Uri entity)
        {
            // If the uri matches a page we have, use that since it is guaranteed to be the best match
            JToken token = GetEntityFromPage(entity);

            if (token == null)
            {
                // if we don't have the page, or the entity is a # uri, try finding the entity in the graph
                token = await Task<JToken>.Run(() => GetEntityFromGraph(entity));
            }

            return token;
        }

        /// <summary>
        /// True - we are missing properties and do not have the page
        /// False - we have the official page, and should turn that JToken
        /// Null - no properties are missing, and we don't have the page, but we can return the same one
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public async Task<bool?> FetchNeeded(Uri entity, IEnumerable<Uri> properties)
        {
            bool? result = true;

            if (HasPageOfEntity(entity))
            {
                result = false;
            }
            else
            {
                result = await Task<bool?>.Run(() => FetchNeededGraphLookup(entity, properties));
            }

            return result;
        }

        /// <summary>
        /// Check if the properties exist in the master graph.
        /// </summary>
        private bool? FetchNeededGraphLookup(Uri entity, IEnumerable<Uri> properties)
        {
            bool? result = true;

            // otherwise check if we already have the pieces
            IEnumerable<JsonLdTriple> triples = null;

            // wait for all existing adds to finish
            WaitForTasks();

            lock (this)
            {
                triples = _masterGraph.SelectSubject(entity);

                // update the last access time for these pages
                foreach (var page in triples.Select(t => t.Page).Distinct())
                {
                    page.UpdateLastUsed();
                }
            }

            bool missing = false;

            foreach (Uri prop in properties)
            {
                if (!triples.Where(t => StringComparer.Ordinal.Equals(prop.AbsoluteUri, t.Predicate.GetValue())).Any())
                {
                    missing = true;
                    break;
                }
            }

            if (!missing)
            {
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Called by JsonLdPage after the graph has loaded.
        /// </summary>
        private void AddCallback(JsonLdPage page)
        {
            page.UpdateLastUsed();
            MergeGraph(page.Graph);
        }

        /// <summary>
        /// Add a graph to the master graph.
        /// </summary>
        private void MergeGraph(JsonLdGraph graph)
        {
            lock (this)
            {
                _masterGraph.Merge(graph);
            }
        }

        /// <summary>
        /// Waits until all queued tasks have completed. Do not run this from inside a lock!
        /// </summary>
        public void WaitForTasks()
        {
            JsonLdPage[] pages = _pages.Values.Where(p => !p.IsLoaded).ToArray();

            foreach (JsonLdPage page in pages)
            {
                try
                {
                    if (!page.IsDisposed && !page.IsLoaded)
                    {
                        // wait for the graph to get merged into the master graph
                        page.Loaded.Wait();
                    }
                }
                catch (ObjectDisposedException)
                {
                    // in the very rare chance this happens, just ignore the removed page
                }
            }
        }

        /// <summary>
        /// Return the entity from the graph
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private JToken GetEntityFromGraph(Uri entity)
        {
            JToken token = null;

            JsonLdTripleCollection triples = null;

            DataTraceSources.Verbose("[EntityCache] GetEntity {0}", entity.AbsoluteUri);

            // make sure everything added at this point has gone into the master graph
            WaitForTasks();

            lock (this)
            {
                // find the best JToken for this subject that we have
                triples = _masterGraph.SelectSubject(entity);

                // update the last access time for these pages
                foreach (var page in triples.Select(t => t.Page).Distinct())
                {
                    page.UpdateLastUsed();
                }
            }

            // find the best jtoken for the subject
            JsonLdTriple triple = triples.Where(n => n.JsonNode != null).OrderByDescending(t => t.HasIdMatchingUrl ? 1 : 0).FirstOrDefault();

            if (triple != null)
            {
                token = triple.JsonNode;
            }

            return token;
        }

        /// <summary>
        /// Returns the JObject of the page if we have it.
        /// </summary>
        private JObject GetEntityFromPage(Uri entity)
        {
            JObject result = null;

            if (Utility.IsRootUri(entity))
            {
                JsonLdPage matchingPage = null;
                if (_pages.TryGetValue(entity, out matchingPage))
                {
                    matchingPage.UpdateLastUsed();
                    result = matchingPage.Compacted;
                }
            }

            return result;
        }


        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // wait for the timer to stop to avoid loose threads
                using (System.Threading.WaitHandle handle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset))
                {
                    _tidyTimer.Dispose(handle);
                    handle.WaitOne();
                }

                foreach (var page in _pages.Values)
                {
                    page.Dispose();
                }

                _pages.Clear();
            }
        }

        /// <summary>
        /// Clean up timer call.
        /// </summary>
        /// <param name="obj"></param>
        private void TidyTimerTick(object obj)
        {
            if (!_disposed)
            {
                // avoid overlapping ticks
                lock (_tidyTimer)
                {
                    CleanUp(_cacheExpiration);
                }
            }
        }

        /// <summary>
        /// Removes all pages not used within the given time span.
        /// </summary>
        private void CleanUp(TimeSpan keepPagesUsedWithin)
        {
            DateTime cutOff = DateTime.UtcNow.Subtract(keepPagesUsedWithin);

            // lock to keep any new pages from being added during this
            lock (this)
            {
                // just in case we show really late
                if (_disposed)
                {
                    return;
                }

                // create a working set of pages that can be considered locked
                JsonLdPage[] pages = _pages.Values.ToArray();

                // if pages are still loading we should skip the clean up
                // TODO: post-preview this should force a clean up if the graph is huge
                if (pages.All(p => p.IsLoaded))
                {
                    // check if a clean up is needed
                    if (pages.Any(p => !p.UsedAfter(cutOff)))
                    {
                        List<JsonLdPage> keep = new List<JsonLdPage>(pages.Length);
                        List<JsonLdPage> remove = new List<JsonLdPage>(pages.Length);

                        // pages could potentially change last accessed times, so make the decisions in one shot
                        foreach (var page in pages)
                        {
                            if (page.UsedAfter(cutOff))
                            {
                                keep.Add(page);
                            }
                            else
                            {
                                remove.Add(page);
                            }
                        }

                        // second check to make sure we need to do this
                        if (remove.Count > 0)
                        {
                            DataTraceSources.Verbose("[EntityCache] EntityCache rebuild started.");

                            JsonLdGraph graph = new JsonLdGraph();

                            // graph merge
                            foreach (var page in keep)
                            {
                                graph.Merge(page.Graph);
                            }

                            _masterGraph = graph;

                            DataTraceSources.Verbose("[EntityCache] EntityCache rebuild complete.");

                            // remove and dispose of the old pages
                            foreach (var page in remove)
                            {
                                JsonLdPage removedPage = null;
                                if (_pages.TryRemove(page.Uri, out removedPage))
                                {
                                    Debug.Assert(!removedPage.UsedAfter(cutOff), "Someone used a page that was scheduled to be removed. This should have been locked.");
                                    removedPage.Dispose();
                                }
                                else
                                {
                                    Debug.Fail(page.Uri.AbsoluteUri + " disappeared from the page cache.");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
