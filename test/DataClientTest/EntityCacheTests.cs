using NuGet.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DataTest
{
    public class EntityCacheTests
    {
        [Fact]
        public async Task EntityCache_Basic()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");

                entityCache.Add(TestJson.BasicGraph, uri);

                Assert.True(entityCache.HasPageOfEntity(uri));
                Assert.False(entityCache.HasPageOfEntity(new Uri("http://test/docA")));

                var root = await entityCache.GetEntity(uri);

                Assert.Equal("test", root["name"]);
            }
        }

        [Fact]
        public async Task EntityCache_Multiple()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);

                Uri uri2 = new Uri("http://test/doc2");
                entityCache.Add(TestJson.BasicGraph2, uri2);

                Assert.True(entityCache.HasPageOfEntity(uri));
                Assert.True(entityCache.HasPageOfEntity(new Uri("http://test/doc#c")));
                Assert.True(entityCache.HasPageOfEntity(new Uri("http://test/doc#nonexist")));
                Assert.False(entityCache.HasPageOfEntity(new Uri("http://test/docnonexist")));

                Assert.True(entityCache.HasPageOfEntity(uri2));
                Assert.True(entityCache.HasPageOfEntity(new Uri("http://test/doc2#c")));
                Assert.True(entityCache.HasPageOfEntity(new Uri("http://test/doc2#nonexist")));
                Assert.False(entityCache.HasPageOfEntity(new Uri("http://test/doc2nonexist")));

                Assert.False(entityCache.HasPageOfEntity(new Uri("http://test/docA")));

                var root = await entityCache.GetEntity(uri);

                Assert.Equal("test", root["name"]);
            }
        }

        [Fact]
        public async Task EntityCache_AddDuplicate()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.Add(TestJson.BasicGraph, uri);

                entityCache.WaitForTasks();

                var root = await entityCache.GetEntity(new Uri("http://test/doc#c"));

                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                var root2 = await entityCache.GetEntity(new Uri("http://test/doc#c"));

                Assert.True(Object.ReferenceEquals(root, root2));
            }
        }

        [Fact]
        public async Task EntityCache_FetchNeeded_SamePage()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                bool? b = await entityCache.FetchNeeded(new Uri("http://test/doc#c"), new Uri[] { new Uri("http://test#prop") });

                Assert.Equal(false, b);
            }
        }

        [Fact]
        public async Task EntityCache_FetchNeeded_NewPage()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                bool? b = await entityCache.FetchNeeded(new Uri("http://test/doc2#c"), new Uri[] { new Uri("http://test#prop") });

                Assert.Equal(true, b);
            }
        }

        [Fact]
        public async Task EntityCache_FetchNeeded_NoProps()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                bool? b = await entityCache.FetchNeeded(new Uri("http://test/doc2#c"), new Uri[] { });

                // null is a special case, no properties were requested, so it is unknown
                Assert.Equal(null, b);
            }
        }

        [Fact]
        public async Task EntityCache_GetEntity_Root()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                var token = await entityCache.GetEntity(uri);

                Assert.Equal("test", token["name"].ToString());
            }
        }

        [Fact]
        public async Task EntityCache_GetEntity_Child()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                var token = await entityCache.GetEntity(new Uri("http://test/doc#a"));

                Assert.NotNull(token);
                Assert.Equal("childA", token["test:name"].ToString());
            }
        }

        [Fact]
        public async Task EntityCache_GetEntity_NonExist()
        {
            using (EntityCache entityCache = new EntityCache())
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                var token = await entityCache.GetEntity(new Uri("http://test/doc#nonexist"));

                Assert.Null(token);
            }
        }

        [Fact]
        public async Task EntityCache_CleanUp_Basic()
        {
            using (EntityCache entityCache = new EntityCache(TimeSpan.FromSeconds(1)))
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);
                entityCache.WaitForTasks();

                // if this is failing randomly increase the wait
                Thread.Sleep(5000);

                var token = await entityCache.GetEntity(new Uri("http://test/doc"));

                Assert.Null(token);
            }
        }

        [Fact]
        public async Task EntityCache_CleanUp_InUse()
        {
            using (EntityCache entityCache = new EntityCache(TimeSpan.FromSeconds(1)))
            {
                Uri uri = new Uri("http://test/doc");
                entityCache.Add(TestJson.BasicGraph, uri);

                Stopwatch timer = new Stopwatch();
                timer.Start();

                while (timer.Elapsed.TotalSeconds < 5)
                {
                    // the cache should not clear while we are using it
                    var token = await entityCache.GetEntity(new Uri("http://test/doc"));
                    Assert.NotNull(token);
                }

                Thread.Sleep(2000);

                var token2 = await entityCache.GetEntity(new Uri("http://test/doc"));
                Assert.Null(token2);
            }
        }
    }
}
