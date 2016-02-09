using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Indexing
{
    /// <summary>
    /// Lucene-based search results indexer.
    /// </summary>
    public class LuceneSearchResultsIndexer : ISearchResultsIndexer
    {
        public IDictionary<string, int> Rank(string queryString, IEnumerable<IPackageSearchMetadata> entries)
        {
            using (var directory = new RAMDirectory())
            {
                AddToIndex(directory, entries);

                var searcher = new IndexSearcher(directory);
                var query = NuGetQuery.MakeQuery(queryString);
                var topDocs = searcher.Search(query, 100);

                var ranking = topDocs.ScoreDocs
                    .Select(d => searcher.Doc(d.Doc))
                    .Zip(Enumerable.Range(0, topDocs.ScoreDocs.Length), (doc, rank) => new { doc, rank })
                    .ToDictionary(x => x.doc.Get("Id"), x => x.rank);

                return ranking;
            }
        }

        private static void AddToIndex(Directory directory, IEnumerable<IPackageSearchMetadata> entries)
        {
            var packageAnalyzer = new PackageAnalyzer();
            using (var writer = new IndexWriter(directory, packageAnalyzer, IndexWriter.MaxFieldLength.UNLIMITED))
            {
                foreach (var document in entries.Select(CreateDocument))
                {
                    writer.AddDocument(document);
                }
                writer.Commit();
            }
        }

        private static Document CreateDocument(IPackageSearchMetadata item)
        {
            var doc = new Document();
            doc.Add(new Field("Id", item.Identity?.Id ?? string.Empty, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("Version", item.Identity?.Version?.ToString() ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Summary", item.Summary ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Description", item.Description ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Title", item.Title ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            doc.Add(new Field("Tags", item.Tags ?? string.Empty, Field.Store.NO, Field.Index.ANALYZED));
            return doc;
        }
    }
}
