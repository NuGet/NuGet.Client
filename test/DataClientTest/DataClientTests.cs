using Newtonsoft.Json.Linq;
using NuGet.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DataTest
{
    public class DataClientTests
    {

        public DataClientTests()
        {
            // remove trace listeners to avoid asserts
            List<TraceListener> listeners = new List<TraceListener>();
            foreach (TraceListener t in Debug.Listeners)
            {
                listeners.Add(t);
            }

            foreach (var listener in listeners)
            {
                Debug.Listeners.Remove(listener);
            }
        }

        [Fact]
        public async Task DataClient_Ensure_NonRDF()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.NonRDF);
                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // this property is inline
                var json = await client.Ensure(new Uri("http://test/doc#a"), new Uri[] { new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type") });
                Assert.Equal(1, count);
                Assert.Null(json);
            }
        }

        [Fact]
        public async Task DataClient_Ensure_BlankNode()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BlankNode);
                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                JObject bare = await client.GetFile(new Uri("http://test/mySearch"));

                // this property is inline
                var json = await client.Ensure(new Uri("http://test/doc#a"), new Uri[] { new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type") });
                Assert.Equal(1, count);
                Assert.Equal("Child", json["@type"]);
            }
        }


        [Fact]
        public async Task DataClient_Ensure_JToken_Corrupt()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                JObject bare = await client.GetFile(new Uri("http://test/docBare"));

                JObject obj = new JObject();
                obj.Add("blah", "blah");

                // this property is inline
                var json = await client.Ensure(obj, new Uri[] { new Uri("http://schema.org/test#name") });
                Assert.Equal(1, count);
                Assert.True(Object.ReferenceEquals(obj, json));
            }
        }

        [Fact]
        public async Task DataClient_Ensure_JToken_MissingInline()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                JObject bare = await client.GetFile(new Uri("http://test/docBare"));

                // this property is inline
                var json = await client.Ensure(bare["test:items"].First, new Uri[] { new Uri("http://schema.org/test#name") });
                Assert.Equal(2, count);
                Assert.Equal("childA", json["test:name"]);
            }
        }

        [Fact]
        public async Task DataClient_Ensure_JToken_Inline()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                JObject bare = await client.GetFile(new Uri("http://test/docBare"));

                // this property is inline
                var json = await client.Ensure(bare["test:items"].First, new Uri[] { new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type") });
                Assert.Equal(1, count);
                Assert.Equal("Child", json["@type"]);
            }
        }


        [Fact]
        public async Task DataClient_Ensure_NonExist()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                await client.GetFile(new Uri("http://test/docBare"));

                // this property is inline
                var json = await client.Ensure(new Uri("http://test/doc#zzzzzz"), new Uri[] { new Uri("http://nevergoingtoexist/blah") });
                Assert.Equal(2, count);
                Assert.Null(json);
            }
        }


        [Fact]
        public async Task DataClient_Ensure_MissingInline()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                await client.GetFile(new Uri("http://test/docBare"));

                // this property is inline
                var json = await client.Ensure(new Uri("http://test/doc#a"), new Uri[] { new Uri("http://nevergoingtoexist/blah") });
                Assert.Equal(2, count);
                Assert.Equal("childA", json["test:name"].ToString());
            }
        }


        [Fact]
        public async Task DataClient_Ensure_Inline()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                await client.GetFile(new Uri("http://test/docBare"));

                // this property is inline
                var json = await client.Ensure(new Uri("http://test/doc#a"), new Uri[] { new Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type") });
                Assert.Equal(1, count);
                Assert.Equal("Child", json["@type"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_Ensure_Follow()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a bare file
                await client.GetFile(new Uri("http://test/docBare"));

                // ensure a child property
                var json = await client.Ensure(new Uri("http://test/doc#c"), new Uri[] { new Uri("http://schema.org/test#name") });
                Assert.Equal(2, count);
                Assert.Equal("grandChildC", json["name"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_Ensure_EmptyCache()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;

                var response = new HttpResponseMessage(HttpStatusCode.OK);

                if (request.RequestUri.AbsoluteUri == "http://test/docBare")
                {
                    response.Content = new TestContent(TestJson.BasicGraphBare);
                }

                if (request.RequestUri.AbsoluteUri == "http://test/doc")
                {
                    response.Content = new TestContent(TestJson.BasicGraph);
                }

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // start with a child property from nothing
                var json = await client.Ensure(new Uri("http://test/doc#c"), new Uri[] { new Uri("http://schema.org/test#name") });
                Assert.Equal(1, count);
                Assert.Equal("grandChildC", json["name"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_GetEntity_404()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetEntity(new Uri("http://test/doc"));
                Assert.Equal(5, count);
                Assert.Equal("404", json["HttpStatusCode"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_GetFile_404()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetFile(new Uri("http://test/doc"));
                Assert.Equal(5, count);
                Assert.Equal("404", json["HttpStatusCode"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_GetEntity_NonExist()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetEntity(new Uri("http://test/doc#nonexist"));
                Assert.Equal(1, count);
                Assert.Null(json);
            }
        }

        [Fact]
        public async Task DataClient_GetEntity_NonExist2()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetEntity(new Uri("http://blah/blah#a"));
                Assert.Equal(1, count);
                Assert.Null(json);
            }
        }


        [Fact]
        public async Task DataClient_GetEntity_Child()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetEntity(new Uri("http://test/doc#c"));
                Assert.Equal(1, count);
                Assert.Equal("grandChildC", json["name"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_GetEntity_Root()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetEntity(new Uri("http://test/doc"));
                Assert.Equal(1, count);
                Assert.Equal("test", json["name"].ToString());
            }
        }

        [Fact]
        public async Task DataClient_Basic_EntityCache()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                // verify no exceptions are thrown here
                var json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, true);
                json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, true);
                json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, true);

                Assert.Equal(3, count);
            }
        }

        [Fact]
        public async Task DataClient_Basic_NoCache()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, false);
                json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, false);
                json = await client.GetFile(new Uri("http://test/doc"), TimeSpan.MinValue, false);

                Assert.Equal(3, count);
            }
        }

        [Fact]
        public async Task DataClient_Basic_UseCache()
        {
            int count = 0;
            TestHandler handler = new TestHandler((request) =>
            {
                count++;
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new TestContent(TestJson.BasicGraph);

                return response;
            });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetFile(new Uri("http://test/doc"));
                json = await client.GetFile(new Uri("http://test/doc"));
                json = await client.GetFile(new Uri("http://test/doc"));

                Assert.Equal(1, count);
            }
        }

        [Fact]
        public async Task DataClient_Basic()
        {
            TestHandler handler = new TestHandler((request) =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Content = new TestContent(TestJson.BasicGraph);

                    return response;
                });

            CacheHttpClient httpClient = new CacheHttpClient(handler);

            using (var client = new DataClient(httpClient, new MemoryFileCache()))
            {
                var json = await client.GetFile(new Uri("http://test/doc"));
                Assert.Equal("test", json["name"].ToString());
            }
        }

        public class TestContent : HttpContent
        {
            private Stream _stream;

            public TestContent(JObject obj)
            {
                _stream = new MemoryStream(Encoding.UTF8.GetBytes(obj.ToString()));
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                _stream.Seek(0, SeekOrigin.Begin);
                _stream.CopyTo(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _stream.Length;
                return true;
            }
        }

        public class TestHandler : HttpMessageHandler
        {
            private Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return _handler(request);
            }
        }
    }
}
