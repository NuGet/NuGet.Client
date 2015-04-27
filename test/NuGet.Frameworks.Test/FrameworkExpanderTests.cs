using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkExpanderTests
    {
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

            Assert.Equal(3, expanded.Length);
            Assert.Equal(".NETFramework,Version=v4.5,Profile=Client", expanded[0].ToString());
            Assert.Equal(".NETFramework,Version=v4.5,Profile=Full", expanded[1].ToString());
            Assert.Equal("NETFrameworkCore,Version=v4.5", expanded[2].ToString());
        }

        [Fact]
        public void FrameworkExpander_Win()
        {
            NuGetFramework framework = NuGetFramework.Parse("win");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal<int>(6, expanded.Length);
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
