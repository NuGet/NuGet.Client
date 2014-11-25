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
    public class NuGetFrameworkTests
    {

        [Theory]
        [InlineData("portable-net45+win8", ".NETPortable, Version=0.0, Profile=7")]
        [InlineData("portable-win8+net45", ".NETPortable, Version=0.0, Profile=7")]
        [InlineData("portable-win8+net45+monoandroid1+monotouch1", ".NETPortable, Version=0.0, Profile=7")]
        public void NuGetFramework_Portable(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).FullFrameworkName;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45-cf", "CompactFramework")]
        [InlineData("net45-CF", "CompactFramework")]
        [InlineData("net45-Full", "")]
        [InlineData("net45", "")]
        [InlineData("net45-WP71", "WindowsPhone71")]
        [InlineData("net45-WP", "WindowsPhone")]
        public void NuGetFramework_ProfileName(string folder, string expected)
        {
            string actual = NuGetFramework.Parse(folder).Profile;

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("net45", ".NETFramework, Version=4.5")]
        [InlineData("net20", ".NETFramework, Version=2.0")]
        [InlineData("net", ".NETFramework, Version=0.0")]
        [InlineData("net10.1.2.3", ".NETFramework, Version=10.1.2.3")]
        [InlineData("net45-cf", ".NETFramework, Version=4.5, Profile=CompactFramework")]
        public void NuGetFramework_Basic(string folderName, string fullName)
        {
            string output = NuGetFramework.Parse(folderName).FullFrameworkName;

            Assert.Equal(fullName, output);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo45")]
        [InlineData("foo45-client")]
        [InlineData("45")]
        [InlineData("foo.45")]
        [InlineData("foo4.5.1.2.3")]
        [InlineData("")]
        public void NuGetFramework_Unsupported(string folderName)
        {
            Assert.Equal("Unsupported, Version=0.0", NuGetFramework.Parse(folderName).FullFrameworkName);
        }
    }
}
