using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonLD.Core;
using Node = JsonLD.Core.RDFDataset.Node;
using System.Threading;

namespace NuGet.Data
{
    /// <summary>
    /// Represents the named graph. This class is responsible for loading the compacted json 
    /// into an RDF graph. These pages are cached and stored in the entity cache.
    /// </summary>
    public class JsonLdPage : IEquatable<JsonLdPage>, IDisposable
    {
        private readonly Uri _uri;
        private readonly DateTime _added;
        private JsonLdGraph _graph;
        private DateTime _lastUsed;
        private JObject _compacted;
        private Task _loadTask;
        private ManualResetEventSlim _loadWait;
        private bool _isLoaded;

        public JsonLdPage(Uri uri, JObject compacted)
        {
            _uri = uri;
            _added = DateTime.UtcNow;
            _lastUsed = _added;
            _compacted = compacted;
            _loadWait = new ManualResetEventSlim();
        }

        /// <summary>
        /// Address of the file.
        /// </summary>
        public Uri Uri
        {
            get
            {
                return _uri;
            }
        }

        /// <summary>
        /// True if Added or LastUsed is more recent than the cutOff date.
        /// </summary>
        public bool UsedAfter(DateTime cutOff)
        {
            return (LastUsed.CompareTo(cutOff) > 0);
        }


        /// <summary>
        /// UTC date of when this page was added
        /// </summary>
        public DateTime Added
        {
            get
            {
                return _added;
            }
        }

        /// <summary>
        /// UTC Date of when this page was last accessed
        /// </summary>
        public DateTime LastUsed
        {
            get
            {
                return _lastUsed;
            }
        }

        public void UpdateLastUsed()
        {
            _lastUsed = DateTime.UtcNow;
        }

        /// <summary>
        /// RDF Graph of the Json LD page.
        /// </summary>
        public JsonLdGraph Graph
        {
            get
            {
                return _graph;
            }

            internal set
            {
                _graph = value;
            }
        }

        /// <summary>
        /// The original compacted json.
        /// </summary>
        /// <remarks>This turns into a cloned copy that matches the graph after the graph is loaded.</remarks>
        public JObject Compacted
        {
            get
            {
                return _compacted;
            }
        }

        /// <summary>
        /// True if the uri without the hash matches this page uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        public bool IsEntityFromPage(Uri uri)
        {
            return Utility.CompareRootUris(_uri, uri);
        }

        /// <summary>
        /// Uri compare
        /// </summary>
        public bool Equals(JsonLdPage other)
        {
            return Uri.Equals(other.Uri);
        }

        public override string ToString()
        {
            return Uri.AbsoluteUri;
        }

        /// <summary>
        /// A simple bool check to see if loaded has finished. This avoids any disposed exceptions.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                return _isLoaded;
            }
        }

        /// <summary>
        /// This semaphore is set after all work for this page has been completed.
        /// </summary>
        public ManualResetEventSlim Loaded
        {
            get
            {
                return _loadWait;
            }
        }

        /// <summary>
        /// Loads the compacted json into the RDF graph.
        /// Steps:
        /// 1. Load the graph
        /// 2. Call callback(this page)
        /// 3. Set the Loaded semaphore
        /// 
        /// The callback should be used to merge the finished graph into the master graph.
        /// </summary>
        /// <param name="callback"></param>
        public void BeginLoad(Action<JsonLdPage> callback)
        {
            if (_loadTask == null)
            {
                _loadTask = Task.Run(() => Load(callback));
            }
        }

        private void Load(Action<JsonLdPage> callback)
        {
            JObject workingCopy = _compacted;

            try
            {
                if (!Utility.IsValidJsonLd(workingCopy))
                {
                    DataTraceSources.Verbose("[EntityCache] Invalid JsonLd skipping {0}", Uri.AbsoluteUri);

                    // we can't parse this page, load a blank graph
                    _graph = new JsonLdGraph();

                    return;
                }

                // we have to modify the json to create the graph, since other people are free to use
                // _compacted during this time we have to make a copy, after we finish we will throw 
                // away _compacted and provide the copy we used instead.
                workingCopy = _compacted.DeepClone() as JObject;

                Uri rootUri = Utility.GetEntityUri(workingCopy);

                if (rootUri == null)
                {
                    // remove the blank node
                    string blankUrl = "http://blanknode.nuget.org/" + Guid.NewGuid().ToString();
                    workingCopy["@id"] = blankUrl;
                    DataTraceSources.Verbose("[EntityCache] BlankNode Doc {0}", blankUrl);
                }

                DataTraceSources.Verbose("[EntityCache] Added {0}", Uri.AbsoluteUri);

                // Load
                _graph = JsonLdGraph.Load(workingCopy, this);

                // make the callback which should merge us into the master graph
                callback(this);
            }
            catch (Exception ex)
            {
                // Something horrible happened when parsing the json-ld. 
                // The original file may be corrupted. The best option here is to leave the page
                // out of the entity cache. Requests for entities from the page should default to just returning
                // the compacted JTokens back. Those jtokens have as much info as we can get in this bad state.

                DataTraceSources.Verbose("[EntityCache] Unable to load!! {0} {1}", Uri.AbsoluteUri, ex.ToString());
            }
            finally
            {
                if (_graph == null)
                {
                    _graph = new JsonLdGraph();
                }

                // replace the original with the copy we used for the graph
                _compacted = workingCopy;

                _loadWait.Set();
                _isLoaded = true;
            }
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _loadWait.Dispose();

                if (_loadTask != null)
                {
                    // we have to wait for the task to finish before we can dispose of it
                    _loadTask.Wait();
                    _loadTask.Dispose();
                }
            }
        }

        public bool IsDisposed
        {
            get
            {
                return _disposed;
            }
        }
    }
}
