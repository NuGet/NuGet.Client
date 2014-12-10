using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.PackagingCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace NuGet.Test
{
    public class TreeTests
    {
        [Fact]
        public void Tree_SinglePivot()
        {
            TreeBuilder builder = new TreeBuilder();

            List<KeyValuePair<string, string>> dataA = new List<KeyValuePair<string, string>>();
            dataA.Add(new KeyValuePair<string, string>("path", "lib\\net45\\a.dll"));

            List<KeyValuePair<string, string>> dataB = new List<KeyValuePair<string, string>>();
            dataB.Add(new KeyValuePair<string, string>("path", "lib\\native\\b.dll"));

            builder.Add(new DevTreeItem("reference", true, dataA),
                new KeyValueTreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, "net45") });

            builder.Add(new DevTreeItem("reference", true, dataB),
                new KeyValueTreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, "native") });

            var tree = builder.GetTree();

            var paths = tree.GetPaths();

            Assert.Equal(2, paths.Count());

            Assert.Equal("lib\\net45\\a.dll", paths.First().Items.Select(e => e as DevTreeItem).Single().Data.Where(e => e.Key == "path").Single().Value);
        }

        [Fact]
        public void Tree_Basic()
        {
            TreeBuilder builder = new TreeBuilder();

            List<KeyValuePair<string, string>> data = new List<KeyValuePair<string, string>>();
            data.Add(new KeyValuePair<string, string>("path", "lib\\a.dll"));

            builder.Add(new DevTreeItem("reference", true, data),
                new KeyValueTreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, PackagingConstants.AnyFramework) });

            var tree = builder.GetTree();

            var paths = tree.GetPaths();

            Assert.Equal(1, paths.Count());

            Assert.Equal("reference", paths.First().Items.Select(e => e as DevTreeItem).Single().Type);
        }
    }
}
