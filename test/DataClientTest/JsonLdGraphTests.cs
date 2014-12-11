using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DataTest
{
    public class JsonLdGraphTests
    {
        [Fact]
        public void JsonLdGraph_Merge_PriorityOverrideRev()
        {
            JObject compacted = TestJson.BasicGraph;
            JObject compacted2 = TestJson.BasicGraphBare;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);
            JsonLdPage page2 = new JsonLdPage(new Uri("http://test/docBare"), compacted2);

            // reversed merge order
            JsonLdGraph graph2 = JsonLdGraph.Load(compacted, page);
            JsonLdGraph graph = JsonLdGraph.Load(compacted2, page2);

            graph.Merge(graph2);

            var triples = graph.SelectSubjectPredicate(new Uri("http://test/doc#a"), new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type"));

            Assert.Equal(1, triples.Count);
            Assert.Equal("http://test/doc", triples.Single().Page.Uri.AbsoluteUri);
        }

        [Fact]
        public void JsonLdGraph_Merge_PriorityOverride()
        {
            JObject compacted = TestJson.BasicGraph;
            JObject compacted2 = TestJson.BasicGraphBare;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);
            JsonLdPage page2 = new JsonLdPage(new Uri("http://test/docBare"), compacted2);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);
            JsonLdGraph graph2 = JsonLdGraph.Load(compacted2, page2);

            graph.Merge(graph2);

            var triples = graph.SelectSubjectPredicate(new Uri("http://test/doc#a"), new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type"));

            // make sure we get doc#a from doc and not docBare
            Assert.Equal(1, triples.Count);
            Assert.Equal("http://test/doc", triples.Single().Page.Uri.AbsoluteUri);
        }

        [Fact]
        public void JsonLdGraph_Merge_Priority()
        {
            JObject compacted = TestJson.BasicGraph;
            JObject compacted2 = TestJson.BasicGraphBare;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);
            JsonLdPage page2 = new JsonLdPage(new Uri("http://test/docBare"), compacted2);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);
            JsonLdGraph graph2 = JsonLdGraph.Load(compacted2, page2);

            graph.Merge(graph2);

            // there are 4 extra - non duplicates
            Assert.Equal(21, graph.Triples.Count());
        }

        [Fact]
        public void JsonLdGraph_Merge_Basic2()
        {
            JObject compacted = TestJson.BasicGraph;
            JObject compacted2 = TestJson.BasicGraph2;
            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);
            JsonLdPage page2 = new JsonLdPage(new Uri("http://test/doc2"), compacted2);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);
            JsonLdGraph graph2 = JsonLdGraph.Load(compacted2, page2);

            graph.Merge(graph2);

            // double
            Assert.Equal(34, graph.Triples.Count());
        }

        [Fact]
        public void JsonLdGraph_Merge_Basic()
        {
            JObject compacted = TestJson.BasicGraph;
            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);
            JsonLdGraph graph2 = JsonLdGraph.Load(compacted, page);

            graph.Merge(graph2);

            // no change
            Assert.Equal(17, graph.Triples.Count());
        }

        [Fact]
        public void JsonLdGraph_Basic()
        {
            JObject compacted = TestJson.BasicGraph;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);

            Assert.Equal(17, graph.Triples.Count());
        }

        [Fact]
        public void JsonLdGraph_SelectSubject()
        {
            JObject compacted = TestJson.BasicGraph;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);

            Assert.Equal(3, graph.SelectSubject(new Uri("http://test/doc#a")).Count);
        }

        [Fact]
        public void JsonLdGraph_SelectSubjectPredicate()
        {
            JObject compacted = TestJson.BasicGraph;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);

            Assert.Equal(1, graph.SelectSubjectPredicate(new Uri("http://test/doc#a"), new Uri("http://schema.org/test#info")).Count);
        }

        [Fact]
        public void JsonLdGraph_RemovePage()
        {
            JObject compacted = TestJson.BasicGraph;

            JsonLdPage page = new JsonLdPage(new Uri("http://test/doc"), compacted);

            JsonLdGraph graph = JsonLdGraph.Load(compacted, page);

            graph.RemovePage(page);

            Assert.Equal(0, graph.Triples.Count());
        }
    }
}
