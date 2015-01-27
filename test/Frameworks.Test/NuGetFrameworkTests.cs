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
        [InlineData("net45", "net45")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+monotouch+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("win10.0", "win10.0")]
        [InlineData("net45-client", "net45-client")]
        [InlineData("net45-unknown", "net45-unknown")]
        [InlineData("Any", "any")]
        [InlineData("Unsupported", "unsupported")]
        [InlineData("Agnostic", "agnostic")]
        public void NuGetFramework_ShortName(string input, string expected)
        {
            var fw = NuGetFramework.Parse(input);

            string result = fw.GetShortFolderName();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("net45", "net45")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+monotouch+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("win10.0", "win10.0")]
        [InlineData("net45-client", "net45-client")]
        [InlineData("net45-unknown", "net45-unknown")]
        [InlineData("Any", "any")]
        [InlineData("Unsupported", "unsupported")]
        [InlineData("Agnostic", "agnostic")]
        public void NuGetFramework_Equality(string a, string b)
        {
            var fw1 = NuGetFramework.Parse(a);
            var fw2 = NuGetFramework.Parse(b);
            HashSet<NuGetFramework> hashSet = new HashSet<NuGetFramework>() { fw1, fw2 };

            Assert.True(fw1.Equals(fw2));
            Assert.True(fw2.Equals(fw1));
            Assert.Equal(fw1.GetHashCode(), fw2.GetHashCode());
            Assert.Equal(1, hashSet.Count);
        }
    }
}
