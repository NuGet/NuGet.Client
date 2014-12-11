using JsonLD.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Node = JsonLD.Core.RDFDataset.Node;

namespace NuGet.Data
{
    // Static Load methods for JsonLdGraph
    public partial class JsonLdGraph
    {
        /// <summary>
        /// Load a compacted json object into a JsonLdGraph
        /// </summary>
        public static async Task<JsonLdGraph> LoadAsync(JObject compacted, JsonLdPage page)
        {
            return await Task.Run(() => Load(compacted, page));
        }

        /// <summary>
        /// Load a compacted json object into a JsonLdGraph
        /// </summary>
        public static JsonLdGraph Load(JObject compacted, JsonLdPage page)
        {
            Dictionary<int, JObject> nodes = new Dictionary<int, JObject>();
            int marker = 0;

            // Mark each node with a serial number
            Action<JObject> addSerial = (node) =>
            {
                if (!Utility.IsInContext(node))
                {
                    int serial = marker++;
                    node[Constants.CacheNode] = serial;
                    nodes.Add(serial, node);
                }
            };

            // add serials
            Utility.JsonEntityVisitor(compacted, addSerial);

            // create graph without JTokens
            var basicGraph = Utility.GetGraphFromCompacted(compacted);

            // split out the cache triples
            List<Triple> normalTriples = new List<Triple>();
            Dictionary<string, JObject> cacheTriples = new Dictionary<string, JObject>();

            foreach (var triple in basicGraph.Triples)
            {
                // cache node predicates represent the mapping between the subject and token serial
                if (triple.Predicate.IsValue(Constants.CacheNode))
                {
                    string subject = triple.Subject.GetValue();

                    int serial;
                    Int32.TryParse(triple.Object.GetValue(), out serial);

                    // Remove the serial we added
                    JObject jObject = nodes[serial];
                    jObject.Remove(Constants.CacheNode);

                    // there should not be any duplicates here
                    cacheTriples.Add(subject, jObject);
                }
                else
                {
                    // store this to go into the graph
                    normalTriples.Add(triple);
                }
            }

            // create the real graph
            JsonLdGraph jsonGraph = new JsonLdGraph();

            // merge the graph data with the compacted json tokens
            foreach (var triple in normalTriples)
            {
                string subject = triple.Subject.GetValue();

                JObject jObject = null;
                cacheTriples.TryGetValue(subject, out jObject);

                var jsonTriple = new JsonLdTriple(page, jObject, triple.Subject, triple.Predicate, triple.Object);
                jsonGraph.Assert(jsonTriple);
            }

            return jsonGraph;
        }
    }
}
