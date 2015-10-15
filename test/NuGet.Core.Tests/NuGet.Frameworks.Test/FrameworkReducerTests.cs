// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkReducerTests
    {
        [Theory]
        // Generation fall-through patterns of uap10.0: 
        // 1. lib/uap10, 
        // 2. then lib/win81, 
        // 3. then lib/wpa81, 
        // 4. then lib/dotnet5.3, 
        // 5. then portable-win81+*, etc
        [InlineData("uap10.0", "uap10.0,win81,wpa81,dotnet5.4,portable-win81+net45", "uap10.0")]
        [InlineData("uap10.0", "win81,wpa81,dotnet5.4,portable-win81+net45", "win81")]
        [InlineData("uap10.0", "wpa81,dotnet5.4,portable-win81+net45", "wpa81")]
        [InlineData("uap10.0", "dotnet5.4,portable-win81+net45", "dotnet5.4")]
        [InlineData("uap10.0", "portable-win81+net45", "portable-win81+net45")]
        [InlineData("uap10.0", "dotnet5.5,dotnet5.4,portable-win81+net45", "dotnet5.4")]
        [InlineData("uap10.0", "dotnet5.4,dotnet,portable-win81+net45", "dotnet5.4")]
        [InlineData("uap10.0", "dotnet,portable-win81+net45", "dotnet")]
        [InlineData("uap10.0", "wpa81,dotnet5.3,portable-win81+net45", "wpa81")]
        [InlineData("uap10.0", "dotnet5.2,portable-win81+net45", "dotnet5.2")]
        [InlineData("uap10.0", "wpa81,dotnet5.2,portable-win81+net45", "wpa81")]
        [InlineData("uap10.0", "dotnet5.2,portable-win81+net45", "dotnet5.2")]
        // Take the most specific PCL profile
        [InlineData("uap10.0", "dotnet6.0,portable-win81+net45", "portable-win81+net45")]
        [InlineData("uap10.0", "dotnet6.0,portable-win81+net45+sl5,portable-win81+net45", "portable-win81+net45")]
        [InlineData("uap10.0", "dotnet6.0,portable-win81+net45+sl5,portable-uap11.0", "portable-win81+net45+sl5")]
        // Same TFM wins
        [InlineData("net461", "net462,net461,net46,net45,net4,net2,dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "net461")]
        [InlineData("net461", "net46,net45,net4,net2,dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "net46")]
        [InlineData("net461", "net45,net4,net2,dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "net45")]
        [InlineData("net461", "net4,net2,dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "net4")]
        [InlineData("net461", "net2,dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "net2")]
        // Use a compatible TFM if there are no framework matches
        [InlineData("net461", "dotnet6.0,dotnet5.5,dotnet5.4,dotnet5.3,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "dotnet5.5")]
        [InlineData("net461", "dotnet5.4,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "dotnet5.4")]
        [InlineData("net461", "dotnet5.4.9,dotnet,portable-net45+win8,portable-net45+win8+wpa81", "dotnet5.4.9")]
        [InlineData("net461", "dotnet,portable-net45+win8,portable-net45+win8+wpa81", "dotnet")]
        [InlineData("net461", "portable-net45+win8,portable-net45+win8+wpa81", "portable-net45+win8")]
        [InlineData("net461", "portable-net45+win8+wpa81,native", "portable-net45+win8+wpa81")]
        [InlineData("net7", "dotnet6.0,dotnet5.5,dotnet5.4,portable-net45+win8", "dotnet5.5")]
        // Additional tests
        [InlineData("dotnet5.5", "dotnet6.0,dotnet5.4,portable-net45+win8", "dotnet5.4")]
        [InlineData("dotnet7", "dotnet6.0,dotnet5.4,portable-net45+win8", "dotnet6.0")]
        [InlineData("dnxcore50", "dotnet6.0,dotnet5.5,portable-net45+win8", "dotnet5.5")]
        [InlineData("dotnet", "dotnet5.1,native,uap10.1", null)]
        [InlineData("uap10.0", "uap10.1,dotnet5.5,dotnet5.4.0.1,dotnet6.0,dnxcore50,native", null)]
        [InlineData("dotnet", "", null)]
        [InlineData("dotnet5.4", "", null)]
        [InlineData("uap10.0", "", null)]
        [InlineData("net461", "", null)]
        public void FrameworkReducer_GetNearestWithGenerations(
            string projectFramework,
            string packageFrameworks,
            string expected)
        {
            // Arrange
            var project = NuGetFramework.Parse(projectFramework);

            var frameworks = packageFrameworks.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => NuGetFramework.Parse(s)).ToList();

            var expectedFramework = expected == null ? null : NuGetFramework.Parse(expected);

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, frameworks);

            // Assert
            Assert.Equal(expectedFramework, nearest);
        }

        [Theory]
        [InlineData("uap10.0", "portable-aspnetcore50+net45+win8+wp8+wpa81")]
        [InlineData("netcore50", "portable-aspnetcore50+net45+win8+wp8+wpa81")]
        [InlineData("dnx452", "net45")]
        [InlineData("dnxcore50", "portable-aspnetcore50+net45+win8+wp8+wpa81")]
        [InlineData("net20", "net20")]
        [InlineData("net451", "net45")]
        [InlineData("sl5", "portable-net40+sl5+win8+wp8+wpa81")]
        [InlineData("wp81", "portable-aspnetcore50+net45+win8+wp8+wpa81")]
        [InlineData("win81", "portable-aspnetcore50+net45+win8+wp8+wpa81")]
        public void FrameworkReducer_JsonNetGetNearestLibGroup(string projectFramework, string expectedFramework)
        {
            // Arrange

            // Get nearest lib group for newtonsoft.json 7.0.1-beta2
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse(projectFramework);

            List<NuGetFramework> frameworks = new List<NuGetFramework>()
            {
                NuGetFramework.Parse("net20"),
                NuGetFramework.Parse("net35"),
                NuGetFramework.Parse("net40"),
                NuGetFramework.Parse("net45"),
                NuGetFramework.Parse("portable-net40+wp80+win8+wpa81+sl5"),
                NuGetFramework.Parse("portable-net45+wp80+win8+wpa81+aspnetcore50")
            };

            // Act
            var result = reducer.GetNearest(project, frameworks);

            // Assert
            Assert.Equal(expectedFramework, result.GetShortFolderName());
        }

        [Fact]
        public void FrameworkReducer_GetNearestUAPTie()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("UAP10.0");

            var win81 = NuGetFramework.Parse("win81");
            var wpa81 = NuGetFramework.Parse("wpa81");
            var native = NuGetFramework.Parse("native");
            var netcore = NuGetFramework.Parse("netcore");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { native, netcore, win81, net45, wpa81 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(win81, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestUAPTie2()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("UAP10.0");

            var win = NuGetFramework.Parse("win");
            var wpa81 = NuGetFramework.Parse("wpa81");
            var native = NuGetFramework.Parse("native");
            var netcore = NuGetFramework.Parse("netcore");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { native, netcore, win, net45, wpa81 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(netcore, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestUAP()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("UAP10.0");

            var win81 = NuGetFramework.Parse("win81");
            var win = NuGetFramework.Parse("win");
            var native = NuGetFramework.Parse("native");
            var netcore = NuGetFramework.Parse("netcore");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { native, netcore, win81, net45, win };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(win81, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestUAPCore()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("UAP10.0");

            var native = NuGetFramework.Parse("native");
            var dotnet = NuGetFramework.Parse("dotnet");
            var dnx451 = NuGetFramework.Parse("dnx451");
            var dnxcore50 = NuGetFramework.Parse("dnxcore50");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { native, dotnet, dnx451, dnxcore50, net45 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(dotnet, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestUAPCore50()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("UAP10.0");

            var dotnet = NuGetFramework.Parse("dotnet");
            var dnx451 = NuGetFramework.Parse("dnx451");
            var dnxcore50 = NuGetFramework.Parse("dnxcore50");
            var net45 = NuGetFramework.Parse("net45");

            var all = new NuGetFramework[] { dotnet, dnx451, dnxcore50, net45 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(dotnet, result);
        }

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

        [Theory]
        [InlineData("dnx452", "aspnet50")]
        [InlineData("dnx451", "aspnet50")]
        [InlineData("dnx45", "aspnet50")]
        [InlineData("net45", "net40")]
        [InlineData("dnxcore5", "aspnetcore50")]
        [InlineData("win8", "portable-net40+sl5+win8+wp8")]
        [InlineData("MonoAndroid40", "monoandroid")]
        [InlineData("win81", "win81")]
        [InlineData("wpa81", "wpa81")]
        [InlineData("sl5", "sl5")]
        public void FrameworkReducer_AutoMapperGetNearestLibGroup(string projectFramework, string expectedFramework)
        {
            // Arrange

            // Get nearest lib group for AutoMapper 4.0.0-ci1026
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse(projectFramework);

            List<NuGetFramework> frameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("aspnet50"),
                    NuGetFramework.Parse("aspnetcore50"),
                    NuGetFramework.Parse("MonoAndroid"),
                    NuGetFramework.Parse("MonoTouch"),
                    NuGetFramework.Parse("net40"),
                    NuGetFramework.Parse("portable-windows8%2Bnet40%2Bwp8%2Bsl5%2BMonoAndroid%2BMonoTouch"),
                    NuGetFramework.Parse("portable-windows8%2Bnet40%2Bwp8%2Bwpa81%2Bsl5%2BMonoAndroid%2BMonoTouch"),
                    NuGetFramework.Parse("sl5"),
                    NuGetFramework.Parse("windows81"),
                    NuGetFramework.Parse("wpa81"),
                    NuGetFramework.Parse("Xamarin.iOS10")
                };

            // Act
            var result = reducer.GetNearest(project, frameworks);

            // Assert
            Assert.Equal(expectedFramework, result.GetShortFolderName());
        }

        [Fact]
        public void FrameworkReducer_GetNearestDuplicatePCL()
        {
            // Verify duplicate PCLs in a folder are reduced correctly
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("wp8");

            var net35 = NuGetFramework.Parse("net35");
            var pcl1 = NuGetFramework.Parse("portable-net403%2Bsl5%2Bnetcore45%2Bwp8");
            var pcl2 = NuGetFramework.Parse("portable-net403%2Bsl5%2Bnetcore45%2Bwp8%2BMonoAndroid1%2BMonoTouch1");

            var all = new NuGetFramework[] { net35, pcl1, pcl2 };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(pcl1, result);
        }

        [Fact]
        public void FrameworkReducer_GetNearestProfile()
        {
            FrameworkReducer reducer = new FrameworkReducer();

            var project = NuGetFramework.Parse("net45-client");

            var net40 = NuGetFramework.Parse("net40");
            var net40client = NuGetFramework.Parse("net40-client");

            var all = new NuGetFramework[] { net40, net40client };

            var result = reducer.GetNearest(project, all);

            Assert.Equal(net40client, result);
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

            var dnxcore50 = NuGetFramework.Parse("dnxcore50");
            var dotnet = NuGetFramework.Parse("dotnet");

            var all = new NuGetFramework[] { dnxcore50, dotnet };

            var result = reducer.GetNearest(NuGetFramework.AnyFramework, all);

            Assert.Equal(dnxcore50, result);
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
        public void FrameworkReducer_GetNearestPackagesBasedWithPCL()
        {
            // Arrange
            var project = NuGetFramework.Parse("net46");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("portable-net45+win8"),
                    NuGetFramework.Parse("dotnet"),
                };

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, packageFrameworks);

            // Assert
            Assert.Equal(packageFrameworks[1], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestPackagesBasedWithFullFramework()
        {
            // Arrange
            var project = NuGetFramework.Parse("uap10.0");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("win8"),
                    NuGetFramework.Parse("dotnet5.3"),
                };

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, packageFrameworks);

            // Assert
            Assert.Equal(packageFrameworks[0], nearest);
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
            // Arrange
            var project = NuGetFramework.Parse("portable-net45+win81");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("portable-net45+win8"),
                    NuGetFramework.Parse("portable-net40+win8"),
                    NuGetFramework.Parse("portable-net40+win81"),
                };

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, packageFrameworks);

            // Assert
            // net45+win8 is nearest since net45 wins over net40
            Assert.Equal(packageFrameworks[0], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestPCLtoPCLVersions()
        {
            var project = NuGetFramework.Parse("portable-net45+win81");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("portable-net40+win81+sl5"),
                    NuGetFramework.Parse("portable-net45+win8+sl5"),
                    NuGetFramework.Parse("portable-net45+win81+wpa81+monotouch+monoandroid"),
                };

            FrameworkReducer reducer = new FrameworkReducer();

            var nearest = reducer.GetNearest(project, packageFrameworks);

            // portable-net45+win81+wpa81+monotouch+monoandroid is nearest to the original
            Assert.Equal(packageFrameworks[2], nearest);
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
        public void FrameworkReducer_GetNearestNonPCLtoPCLBasedOnOtherVersions()
        {
            // Arrange
            var project = NuGetFramework.Parse("win8");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("portable-net45+win8+wpa81"),
                    NuGetFramework.Parse("portable-net45+win8+wpa82"),
                    NuGetFramework.Parse("portable-net45+win8+wpa9"),
                    NuGetFramework.Parse("portable-net45+win8+wpa10.0"),
                    NuGetFramework.Parse("portable-net45+win8+wpa11.0+sl5")
                };

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, packageFrameworks);

            // Assert
            // portable-net45+win8+wpa10.0 is the best match since it has the highest
            // version of WPA, and the least frameworks
            Assert.Equal(packageFrameworks[3], nearest);
        }

        [Fact]
        public void FrameworkReducer_GetNearestNonPCLtoPCLUncertain()
        {
            // Arrange
            var project = NuGetFramework.Parse("win8");

            var packageFrameworks = new List<NuGetFramework>()
                {
                    NuGetFramework.Parse("portable-net45+win8+sl6"),
                    NuGetFramework.Parse("portable-net45+win8+dnxcore50"),
                    NuGetFramework.Parse("portable-net45+win8+native"),
                };

            FrameworkReducer reducer = new FrameworkReducer();

            // Act
            var nearest = reducer.GetNearest(project, packageFrameworks);

            // Assert
            // There is no certain way to relate these frameworks to each other, but the same one
            // should always come back from this compare.
            Assert.Equal(packageFrameworks[1], nearest);
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

            Assert.Equal("net40", result.GetShortFolderName());
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
