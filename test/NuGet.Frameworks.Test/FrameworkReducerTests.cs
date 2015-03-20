using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Test
{
    public class FrameworkReducerTests
    {
        [Fact]
        public void FrameworkReducer_GetNearestNetCore()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var win81 = NuGetFramework.Parse("win81");
            var native = NuGetFramework.Parse("native");
            var netcore = NuGetFramework.Parse("netcore");

            var project = NuGetFramework.Parse("net451");

            var all = new NuGetFramework[] { native, netcore };

            var result = reducer.GetNearest(win81, all);

            Assert.Equal(netcore, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWinRT()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var win81 = NuGetFramework.Parse("win81");
            var native = NuGetFramework.Parse("native");
            var winrt45 = NuGetFramework.Parse("winrt45");

            var project = NuGetFramework.Parse("net451");

            var all = new NuGetFramework[] { native, winrt45 };

            var result = reducer.GetNearest(win81, all);

            Assert.Equal(winrt45, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWin()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var win81 = NuGetFramework.Parse("win81");
            var win8 = NuGetFramework.Parse("win8");
            var netcore = NuGetFramework.Parse("netcore");
            var nfcore = NuGetFramework.Parse("nfcore");
            var native = NuGetFramework.Parse("native");
            var netcore451 = NuGetFramework.Parse("netcore451");
            var winrt45 = NuGetFramework.Parse("winrt45");

            var project = NuGetFramework.Parse("net451");

            var all = new NuGetFramework[] { win8, netcore, nfcore, native, netcore451, winrt45 };

            var result = reducer.GetNearest(win81, all);

            Assert.Equal(netcore451, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestAny()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var native = NuGetFramework.Parse("native");
            var win81 = NuGetFramework.Parse("win81");

            var all = new NuGetFramework[] { win81, native };

            var result = reducer.GetNearest(NuGetFramework.AnyFramework, all);

            Assert.Equal(win81, result);
        }

        [Fact]
        public void FrameworkReducer_ReduceToHighest()
        {
            // both frameworks are equivalent
            var fw1 = new NuGetFramework(FrameworkConstants.FrameworkIdentifiers.Windows, FrameworkConstants.EmptyVersion);
            var fw2 = FrameworkConstants.CommonFrameworks.Win8;

            var packageFrameworks = new List<NuGetFramework>()
            {
                fw1,
                fw2
            };

            FrameworkReducer reducer = new FrameworkReducer();

            // the non-zero version should win in both cases
            var upwards = reducer.ReduceUpwards(packageFrameworks).Single();
            var downwards = reducer.ReduceUpwards(packageFrameworks).Single();

            Assert.Equal(fw2, upwards);
            Assert.Equal(fw2, downwards);
        }

        [Fact]
        public void FrameworkReducer_GetNearestPCLtoPCL2()
        {
            var project = NuGetFramework.Parse("portable-net45+sl5+monotouch+monoandroid");

            var packageFrameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("portable-net45+sl5"),
                NuGetFramework.Parse("portable-net40+sl5+monotouch"),
                NuGetFramework.Parse("portable-net40+sl4+monotouch+monoandroid"),
                NuGetFramework.Parse("portable-net40+sl4+monotouch+monoandroid+wp71"),
            };

            FrameworkReducer reducer = new FrameworkReducer();

            var nearest = reducer.GetNearest(project, packageFrameworks);

            Assert.Equal(packageFrameworks[0], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestPCLtoPCL()
        {
            var project = NuGetFramework.Parse("portable-net45+win81");

            var packageFrameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("portable-net45+win8"),
                NuGetFramework.Parse("portable-net40+win8"),
                NuGetFramework.Parse("portable-net40+win81"),
            };

            FrameworkReducer reducer = new FrameworkReducer();

            var nearest = reducer.GetNearest(project, packageFrameworks);

            // net45+win8 is nearest. it beats net40+win81 since it is a known framework
            Assert.Equal(packageFrameworks[0], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestNonPCLtoPCL()
        {
            var project = NuGetFramework.Parse("win9");

            var packageFrameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("portable-net45+win8"),
                NuGetFramework.Parse("portable-net45+win82"),
                NuGetFramework.Parse("portable-net45+win81"),
                NuGetFramework.Parse("portable-net45+win91"),
            };

            FrameworkReducer reducer = new FrameworkReducer();

            var nearest = reducer.GetNearest(project, packageFrameworks);

            // win82 is the best match for win9
            Assert.Equal(packageFrameworks[1], nearest);
        }

        [Fact]
        public void FrameworkReducer_JsonNet701beta1()
        {
            var project = NuGetFramework.Parse("win81");

            var packageFrameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net20"),
                NuGetFramework.Parse("net35"),
                NuGetFramework.Parse("net40"),
                NuGetFramework.Parse("net45"),
                NuGetFramework.Parse("portable-net40%2Bsl5%2Bwp80%2Bwin8%2Bwpa81"),
                NuGetFramework.Parse("portable-net45%2Bwp80%2Bwin8%2Bwpa81%2Baspnetcore50")
            };

            FrameworkReducer reducer = new FrameworkReducer();

            var nearest = reducer.GetNearest(project, packageFrameworks);

            // #4 has the least profile frameworks
            Assert.Equal(packageFrameworks[4], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestChooseFrameworkName()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("nfcore45");

            var project = NuGetFramework.Parse("net451");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(framework1, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestEqual()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("net40-client");

            var project = NuGetFramework.Parse("net40-client");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(framework2, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestEquivalent()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("net40-client");

            var project = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(framework1, result);
        }


        [Fact]
        public void FrameworkReducer_ReduceUpEquivalent()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("net40-client");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceUpwards(all).ToArray();

            Assert.Equal(2, result.Length);
            Assert.Equal(framework1, result.First());
            Assert.Equal(framework2, result.Last());
        }

        [Fact]
        public void FrameworkReducer_ReduceUpEqual()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("net40");
            var framework2 = NuGetFramework.Parse("net40");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceUpwards(all);

            Assert.Equal(framework2, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceDownPCL2()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-net45+win8+wp8");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceDownwards(all);

            Assert.Equal(framework2, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceDownPCL()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-net451+win81");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceDownwards(all);

            Assert.Equal(framework1, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceUpPCL2()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-net45+win8+wp8");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceUpwards(all);

            Assert.Equal(framework1, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceUpPCL()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-net451+win81");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.ReduceUpwards(all);

            Assert.Equal(framework2, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReducePCL()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var framework1 = NuGetFramework.Parse("portable-net45+win8");
            var framework2 = NuGetFramework.Parse("portable-win+net45");

            var all = new NuGetFramework[] { framework1, framework2 };

            var result = reducer.Reduce(all);

            Assert.Equal(framework1, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceNonSingle()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var sl3wp = NuGetFramework.Parse("sl3-wp");
            var wp7 = NuGetFramework.Parse("wp7");
            var win81 = NuGetFramework.Parse("win81");

            var all = new NuGetFramework[] { sl3wp, wp7, win81 };

            var result = reducer.Reduce(all);

            Assert.Equal(win81, result.First());
            Assert.Equal(wp7, result.Skip(1).First());
        }

        [Fact]
        public void FrameworkReducer_ReduceSingle()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var sl3wp = NuGetFramework.Parse("sl3-wp");
            var wp7 = NuGetFramework.Parse("wp7");

            var all = new NuGetFramework[] { sl3wp, wp7 };

            var result = reducer.Reduce(all);

            Assert.Equal(wp7, result.Single());
        }


        [Fact]
        public void FrameworkReducer_ReduceUpwardsNonSingle()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net35 = NuGetFramework.Parse("net35");
            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");
            var net451 = NuGetFramework.Parse("net451");
            var net453 = NuGetFramework.Parse("net453");
            var wp8 = NuGetFramework.Parse("wp8");
            var wp81 = NuGetFramework.Parse("wp81");

            var all = new NuGetFramework[] { net35, net40, net45, net451, net453, wp8, wp81 };

            var result = reducer.ReduceUpwards(all);

            Assert.Equal(net453, result.First());
            Assert.Equal(wp81, result.Skip(1).First());
        }

        [Fact]
        public void FrameworkReducer_ReduceUpwardsBasic()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net35 = NuGetFramework.Parse("net35");
            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");
            var net451 = NuGetFramework.Parse("net451");
            var net453 = NuGetFramework.Parse("net453");

            var all = new NuGetFramework[] { net35, net40, net45, net451, net453 };

            var result = reducer.ReduceUpwards(all);

            Assert.Equal(net453, result.Single());
        }

        [Fact]
        public void FrameworkReducer_ReduceDownwardsBasic()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net35 = NuGetFramework.Parse("net35");
            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");
            var net451 = NuGetFramework.Parse("net451");
            var net453 = NuGetFramework.Parse("net453");

            var all = new NuGetFramework[] { net35, net40, net45, net451, net453 };

            var result = reducer.ReduceDownwards(all);

            Assert.Equal(net35, result.Single());
        }

        [Fact]
        public void FrameworkReducer_GetNearest()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net35 = NuGetFramework.Parse("net35");
            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");
            var net451 = NuGetFramework.Parse("net451");
            var net453 = NuGetFramework.Parse("net453");

            var all = new NuGetFramework[] { net35, net40, net45, net453 };

            var result = reducer.GetNearest(net451, all);

            Assert.Equal(net45, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearest2()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net35 = NuGetFramework.Parse("net35");
            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");
            var net451 = NuGetFramework.Parse("net451");
            var net453 = NuGetFramework.Parse("net453");

            var all = new NuGetFramework[] { net35, net40, net45, net451, net453 };

            var result = reducer.GetNearest(net451, all);

            Assert.Equal(net451, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWithAny()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net40 = NuGetFramework.Parse("net40");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { NuGetFramework.AnyFramework, net40, net45 };

            var result = reducer.GetNearest(net45, all);

            Assert.Equal(net45, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWithUnsupported()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { NuGetFramework.AnyFramework, NuGetFramework.UnsupportedFramework };

            var result = reducer.GetNearest(net45, all);

            Assert.Equal(NuGetFramework.AnyFramework, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWithUnsupported2()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var all = new NuGetFramework[] { NuGetFramework.AnyFramework, NuGetFramework.UnsupportedFramework };

            var result = reducer.GetNearest(NuGetFramework.UnsupportedFramework, all);

            Assert.Equal(NuGetFramework.AnyFramework, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWithUnsupported3()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var all = new NuGetFramework[] { NuGetFramework.UnsupportedFramework };

            var result = reducer.GetNearest(NuGetFramework.UnsupportedFramework, all);

            Assert.Equal(NuGetFramework.UnsupportedFramework, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestWithAnyOnly()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { NuGetFramework.AnyFramework };

            var result = reducer.GetNearest(net45, all);

            Assert.Equal(NuGetFramework.AnyFramework, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestAzureRepro()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            List<NuGetFramework> frameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net40"),
                NuGetFramework.Parse("portable-net45+wp8+win8+wpa"),
                NuGetFramework.Parse("sl4")
            };

            NuGetFramework projectFramework = NuGetFramework.Parse("net45");

            var result = reducer.GetNearest(projectFramework, frameworks);

            Assert.Equal("net4", result.GetShortFolderName());
        }

        [Theory]
        [InlineData("dnx451", "aspnet50")]
        [InlineData("dnx451", "aspnet5")]
        [InlineData("dnx451", "aspnet")]
        [InlineData("dnx", "aspnet50")]
        [InlineData("dnx", "net45")]
        [InlineData("dnx", "portable-net45+win8")]
        [InlineData("dnx", "portable-win8+net45")]
        [InlineData("dnx", "portable-win8+net45+sl4")]
        [InlineData("dnx50", "dnx50")]
        [InlineData("dnx50", "dnx5")]
        [InlineData("dnx50", "dnx")]
        [InlineData("dnx", "net45")]
        [InlineData("dnx", "portable-net45+win8")]
        [InlineData("dnx", "portable-win8+net45")]
        [InlineData("dnx", "portable-win8+net45+sl4")]
        public void FrameworkReducer_GetNearestDnx(string project, string framework)
        {
            FrameworkReducer reducer = new FrameworkReducer();

            List<NuGetFramework> frameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse(framework),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse(project);

            var result = reducer.GetNearest(projectFramework, frameworks);

            Assert.True(NuGetFramework.Parse(framework).Equals(result));
        }

        [Theory]
        [InlineData("aspnet", "aspnetcore")]
        [InlineData("aspnetcore", "net45")]
        [InlineData("aspnetcore", "portable-net403+win8")]
        public void FrameworkReducer_GetNearestAspNeg(string project, string framework)
        {
            FrameworkReducer reducer = new FrameworkReducer();

            List<NuGetFramework> frameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse(framework),
            };

            NuGetFramework projectFramework = NuGetFramework.Parse(project);

            var result = reducer.GetNearest(projectFramework, frameworks);

            Assert.Null(result);
        }
    }
}
