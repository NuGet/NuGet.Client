using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Newtonsoft.Json.Linq;
using NuGet.Indexing;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SearchAggregator.Services
{
    public class SearchResultsAggregator
    {
        private readonly IList<Queue<KeyValuePair<string, JObject>>> results;

        public SearchResultsAggregator()
        {
            results = new List<Queue<KeyValuePair<string, JObject>>>();
        }

        public void Add(string searchResource, JObject searchResults)
        {
            var result = new Queue<KeyValuePair<string, JObject>>();
            int i = 0;
            foreach (JObject searchResult in searchResults["data"])
            {
                searchResult["sourceRank"] = i++; // this is a diagnostic aid and not used in the algorithm
                searchResult["source"] = searchResource;
                result.Enqueue(new KeyValuePair<string, JObject>(searchResult["id"].ToString(), searchResult));
            }
            results.Add(result);
        }

        public JObject Aggregate(string q)
        {
            var sw = new Stopwatch();
            sw.Start();

            var result = JObject.Parse(@"
{
    ""@context"": {
        ""@vocab"": ""http://schema.nuget.org/schema#""
    },
    ""index"": ""search-aggregator"",
    ""lastReopen"": """",
    ""data"": []
}");
            result["lastReopen"] = DateTime.UtcNow;

            var merged = Merge(results, q);

            result["data"] = new JArray(merged.Select(e => e.Value.DeepClone()).ToArray());

            sw.Stop();
            Trace.WriteLine($"Merged in: {sw.ElapsedMilliseconds}");

            return result;
        }

        private static Queue<KeyValuePair<string, JObject>> Merge(
            IEnumerable<Queue<KeyValuePair<string, JObject>>> results, string q)
        {
            //  (1) create a combined dictionary in order to merge results that have same id

            IDictionary<string, JObject> combined = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var result in results)
            {
                foreach (var item in result)
                {
                    JObject value;
                    if (combined.TryGetValue(item.Key, out value))
                    {
                        combined[item.Key] = MergeResult(value, item.Value);
                    }
                    else
                    {
                        combined.Add(item);
                    }
                }
            }

            if (combined.Count == 0)
            {
                return new Queue<KeyValuePair<string, JObject>>();
            }

            //  (2) create an in-memory Lucene index

            //  **** NOTE: PackageAnalyzer is a class from NuGet.Indexing ****

            var directory = new RAMDirectory();
            using (var writer = new IndexWriter(directory, new PackageAnalyzer(), IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var item in combined.Values)
                {
                    writer.AddDocument(CreateDocument(item));
                }
                writer.Commit();
            }

            //  (3) re-query the in-memory index

            //  **** NOTE: NuGetQuery is a class from NuGet.Indexing ****

            var searcher = new IndexSearcher(directory);
            var query = NuGetQuery.MakeQuery(q);
            var topDocs = searcher.Search(query, 100);

            //  (4) build a lookup of the local re-calculated ranking

            var combinedRanking = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int i = 0;
            foreach (ScoreDoc scoreDoc in topDocs.ScoreDocs)
            {
                string id = searcher.Doc(scoreDoc.Doc).Get("Id");
                combined[id]["localRank"] = i; // this is a diagnostic aid and not used in the algorithm 
                combinedRanking[id] = i++;
            }

            //  (5) now combine the results from the individual sources with a merged sort - the hashset helps filter duplicates

            var combinedResult = new Queue<KeyValuePair<string, JObject>>();
            var distinctCombinedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string winner = null;
            do
            {
                winner = null;
                foreach (var result in results)
                {
                    if (result.Count == 0)
                    {
                        continue;
                    }
                    var current = result.Peek().Key;

                    if (winner == null)
                    {
                        winner = current;
                    }
                    else
                    {
                        if (combinedRanking[current] < combinedRanking[winner])
                        {
                            winner = current;
                        }
                    }
                }

                if (winner != null)
                {
                    if (!distinctCombinedIds.Contains(winner))
                    {
                        combinedResult.Enqueue(new KeyValuePair<string, JObject>(winner, combined[winner]));
                        distinctCombinedIds.Add(winner);
                    }

                    foreach (var result in results)
                    {
                        if (result.Count == 0)
                        {
                            continue;
                        }
                        if (distinctCombinedIds.Contains(result.Peek().Key))
                        {
                            result.Dequeue();
                        }
                    }
                }
            }
            while (winner != null);

            return combinedResult;
        }

        private static Document CreateDocument(JObject item)
        {
            var doc = new Document();
            doc.Add(new Field("Id", item["id"].ToString(), Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Version", item["version"].ToString(), Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Summary", ((string)item["summary"]) ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Description", ((string)item["description"]) ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Title", ((string)item["title"]) ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            foreach (var tag in item["tags"] ?? Enumerable.Empty<JToken>())
            {
                doc.Add(new Field("Tags", tag.ToString(), Field.Store.NO, Field.Index.ANALYZED));
            }

            return doc;
        }

        private static JObject MergeResult(JObject lhs, JObject rhs)
        {
            //  (1) combine version lists

            var versions = new SortedDictionary<NuGetVersion, JObject>();
            AddVersions(versions, lhs);
            AddVersions(versions, rhs);
            var combinedVersions = new JArray(versions.Values.ToArray());

            //  (2) determine which top-level result to include in combined

            var lhsVersion = NuGetVersion.Parse(lhs["version"].ToString());
            var rhsVersion = NuGetVersion.Parse(rhs["version"].ToString());
            if (lhsVersion >= rhsVersion)
            {
                lhs["versions"] = combinedVersions;
                return lhs;
            }
            else
            {
                rhs["versions"] = combinedVersions;
                return rhs;
            }
        }

        private static void AddVersions(SortedDictionary<NuGetVersion, JObject> versions, JObject obj)
        {
            foreach (JObject version in obj["versions"])
            {
                var nugetVersion = NuGetVersion.Parse(version["version"].ToString());
                if (!versions.ContainsKey(nugetVersion))
                {
                    versions.Add(nugetVersion, version);
                }
            }
        }
    }
}
