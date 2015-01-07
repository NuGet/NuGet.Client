using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkReducerTests
    {
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
    }
}
