using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonLD.Core;
using Node = JsonLD.Core.RDFDataset.Node;

namespace NuGet.Data
{
    public partial class JsonLdGraph : JsonLdTripleCollection
    {
        private readonly Dictionary<string, Dictionary<string, List<JsonLdTriple>>> _subjectIndex;

        public JsonLdGraph()
            : this(Enumerable.Empty<JsonLdTriple>())
        {

        }

        public JsonLdGraph(IEnumerable<JsonLdTriple> triples)
            : base(null)
        {
            _subjectIndex = new Dictionary<string, Dictionary<string, List<JsonLdTriple>>>();
        }

        public void Assert(JsonLdTriple triple)
        {
            lock (this)
            {
                AssertNoLock(triple);
            }
        }

        private void AssertNoLock(JsonLdTriple triple)
        {
            string subject = triple.Subject.GetValue();
            string predicate = triple.Predicate.GetValue();
            string obj = triple.Object.GetValue();

            Dictionary<string, List<JsonLdTriple>> inner = null;
            if (_subjectIndex.TryGetValue(subject, out inner))
            {
                List<JsonLdTriple> objs = null;
                if (inner.TryGetValue(predicate, out objs))
                {
                    JsonLdTriple existingTriple = objs.Where(t => t.Object.IsValue(obj)).FirstOrDefault();

                    if (existingTriple != null)
                    {
                        // move the existing one to the alts if we have a better one now
                        if (IsBetter(triple, existingTriple))
                        {
                            objs.Remove(existingTriple);
                            objs.Add(triple);
                        }
                    }
                    else
                    {
                        // normal add
                        objs.Add(triple);
                    }
                }
                else
                {
                    inner.Add(predicate, new List<JsonLdTriple>() { triple });
                }
            }
            else 
            {
                // completely new
                inner = new Dictionary<string, List<JsonLdTriple>>();
                _subjectIndex.Add(subject, inner);

                inner.Add(predicate, new List<JsonLdTriple>() { triple });
            }
        }

        public void Assert(JsonLdPage page, JObject jsonNode, Node subNode, Node predNode, Node objNode)
        {
            Assert(new JsonLdTriple(page, jsonNode, subNode, predNode, objNode));
        }

        public override int Count
        {
            get
            {
                lock (this)
                {
                    return _subjectIndex.Count;
                }
            }
        }

        public void RemovePage(JsonLdPage page)
        {
            lock (this)
            {
                var subjects = _subjectIndex.Keys.ToArray();
                foreach (var subject in subjects)
                {
                    var preds = _subjectIndex[subject].Keys.ToArray();

                    foreach (var pred in preds)
                    {
                        _subjectIndex[subject][pred].RemoveAll(t => t.Page.Equals(page));

                        if (_subjectIndex[subject][pred].Count < 1)
                        {
                            _subjectIndex[subject].Remove(pred);
                        }
                    }

                    if (_subjectIndex[subject].Count < 1)
                    {
                        _subjectIndex.Remove(subject);
                    }
                }
            }
        }

        public void Merge(JsonLdGraph graph)
        {
            lock (this)
            {
                foreach (var triple in graph.Triples)
                {
                    AssertNoLock(triple);
                }
            }
        }

        public JsonLdTripleCollection SelectSubject(Uri uri)
        {
            lock (this)
            {
                List<JsonLdTriple> triples = new List<JsonLdTriple>();

                Dictionary<string, List<JsonLdTriple>> inner = null;
                if (_subjectIndex.TryGetValue(uri.AbsoluteUri, out inner))
                {
                    foreach (var pair in inner.Values)
                    {
                        triples.AddRange(pair);
                    }
                }

                return new JsonLdTripleCollection(triples);
            }
        }

        public JsonLdTripleCollection SelectSubjectPredicate(Uri subject, Uri predicate)
        {
            lock (this)
            {
                List<JsonLdTriple> triples = new List<JsonLdTriple>();

                Dictionary<string, List<JsonLdTriple>> inner = null;
                if (_subjectIndex.TryGetValue(subject.AbsoluteUri, out inner))
                {
                    inner.TryGetValue(predicate.AbsoluteUri, out triples);
                }

                return new JsonLdTripleCollection(triples);
            }
        }


        /// <summary>
        /// triples containing the most complete JTokens
        /// </summary>
        public override IEnumerable<JsonLdTriple> Triples
        {
            get
            {
                lock (this)
                {
                    List<JsonLdTriple> triples = new List<JsonLdTriple>();

                    foreach (var pair in _subjectIndex.Values)
                    {
                        foreach (var innerPair in pair.Values)
                        {
                            triples.AddRange(innerPair);
                        }
                    }

                    return triples;
                }
            }
        }

        /// <summary>
        /// Highest is best
        /// </summary>
        public static JsonLdTriple GetBest(IEnumerable<JsonLdTriple> triples)
        {
            return triples.OrderByDescending(t => t.JsonNode != null ? 1000 : 0).ThenByDescending(t => t.HasIdMatchingUrl ? 400 : 0).FirstOrDefault();
        }

        /// <summary>
        /// True if A is better
        /// </summary>
        public static bool IsBetter(JsonLdTriple a, JsonLdTriple b)
        {
            if (a == b)
            {
                return false;
            }

            if (a.JsonNode != null && b.JsonNode == null)
            {
                return true;
            }

            if (a.HasIdMatchingUrl && !b.HasIdMatchingUrl)
            {
                return true;
            }

            if (a.JsonNode != null && b.JsonNode != null)
            {
                return a.JsonNode.Descendants().Count() > a.JsonNode.Descendants().Count();
            }

            return false;
        }
    }
}
