// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkExpanderTests
    {
        [Fact]
        public void FrameworkExpander_UAPWPA()
        {
            NuGetFramework framework = NuGetFramework.Parse("UAP10.0");
            NuGetFramework indirect = new NuGetFramework("WindowsPhoneApp", new Version(8, 1, 0, 0));

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.True(expanded.Contains(indirect, NuGetFramework.Comparer), String.Join("|", expanded.Select(e => e.ToString())));
        }

        [Fact]
        public void FrameworkExpander_UAPWIN()
        {
            NuGetFramework framework = NuGetFramework.Parse("UAP10.0");
            NuGetFramework indirect = new NuGetFramework("Windows", new Version(8, 1, 0, 0));

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.True(expanded.Contains(indirect, NuGetFramework.Comparer), String.Join("|", expanded.Select(e => e.ToString())));
        }

        [Fact]
        public void FrameworkExpander_UAP()
        {
            NuGetFramework framework = NuGetFramework.Parse("UAP10.0");
            NuGetFramework indirect = new NuGetFramework(".NETCore", new Version(5, 0, 0, 0));

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.True(expanded.Contains(indirect, NuGetFramework.Comparer), String.Join("|", expanded.Select(e => e.ToString())));
        }

        [Fact]
        public void FrameworkExpander_Indirect()
        {
            NuGetFramework framework = NuGetFramework.Parse("win9");
            NuGetFramework indirect = new NuGetFramework(".NETCore", new Version(4, 5));

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.True(expanded.Contains(indirect, NuGetFramework.Comparer), String.Join("|", expanded.Select(e => e.ToString())));
        }

        [Fact]
        public void FrameworkExpander_Basic()
        {
            NuGetFramework framework = NuGetFramework.Parse("net45");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal(5, expanded.Length);
            Assert.Equal(".NETFramework,Version=v4.5,Profile=Client", expanded[0].ToString());
            Assert.Equal(".NETFramework,Version=v4.5,Profile=Full", expanded[1].ToString());
            Assert.Equal(".NETPlatform,Version=v5.0", expanded[2].ToString()); // dotnet5
            Assert.Equal(".NETPlatform,Version=v5.2", expanded[3].ToString());
            Assert.Equal(".NETPlatform,Version=v5.0", expanded[4].ToString());  // dotnet
        }

        [Fact]
        public void FrameworkExpander_Win()
        {
            NuGetFramework framework = NuGetFramework.Parse("win");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal<int>(8, expanded.Length);
        }

        [Fact]
        public void FrameworkExpander_NetCore45()
        {
            NuGetFramework framework = NuGetFramework.Parse("nfcore45");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal(0, expanded.Length);
        }
    }
}
