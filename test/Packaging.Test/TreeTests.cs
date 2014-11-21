using Newtonsoft.Json.Linq;
using NuGet.Packaging;
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
        //[Fact]
        //public void Tree_SinglePivot()
        //{
        //    TreeBuilder builder = new TreeBuilder();

        //    List<KeyValuePair<string, string>> dataA = new List<KeyValuePair<string, string>>();
        //    dataA.Add(new KeyValuePair<string, string>("path", "lib\\net45\\a.dll"));

        //    List<KeyValuePair<string, string>> dataB = new List<KeyValuePair<string, string>>();
        //    dataB.Add(new KeyValuePair<string, string>("path", "lib\\native\\b.dll"));

        //    builder.Add(new NuGetTreeItem("reference", true, dataA),
        //        new TreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, "net45") });

        //    builder.Add(new NuGetTreeItem("reference", true, dataB),
        //        new TreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, "native") });

        //    var tree = builder.GetTree();

        //    var paths = tree.GetPaths();

        //    Assert.Equal(2, paths.Count());

        //    XElement root = tree.ToXml();

        //    var children = root.Elements(XName.Get("node")).Elements(XName.Get("children"));
        //    var items = children.Elements(XName.Get("node")).Elements(XName.Get("items"));
        //    var item = items.Elements(XName.Get("item"));
        //    var pathValue = item.Elements(XName.Get("path")).First().Value;

        //    Assert.Equal("lib\\net45\\a.dll", pathValue);
        //}

        //[Fact]
        //public void Tree_Basic()
        //{
        //    TreeBuilder builder = new TreeBuilder();

        //    List<KeyValuePair<string, string>> data = new List<KeyValuePair<string,string>>();
        //    data.Add(new KeyValuePair<string, string>("path", "lib\\a.dll"));

        //    builder.Add(new NuGetTreeItem("reference", true, data), 
        //        new TreeProperty[] { new KeyValueTreeProperty(PackagingConstants.TargetFrameworkPropertyKey, PackagingConstants.AnyFramework) });

        //    var tree = builder.GetTree();

        //    var paths = tree.GetPaths();

        //    Assert.Equal(1, paths.Count());

        //    XElement root = tree.ToXml();

        //    var typeValue = root.Elements(XName.Get("node")).Elements(XName.Get("items")).Elements(XName.Get("item")).Attributes(XName.Get("type")).Single().Value;

        //    Assert.Equal("reference", typeValue);
        //}
    }
}
