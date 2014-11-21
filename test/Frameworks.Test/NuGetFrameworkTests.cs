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

        //[Theory]
        //[InlineData("net45", ".NETFramework, Version=4.5")]
        //[InlineData("net20", ".NETFramework, Version=2.0")]
        //[InlineData("net", ".NETFramework")]
        //[InlineData("net10.1.2.3", ".NETFramework, Version=10.1.2.3")]
        //[InlineData("net45-cf", ".NETFramework, Version=4.5, Profile=CompactFramework")]
        //public void NuGetFramework_Basic(string folderName, string fullName)
        //{
        //    Assert.Equal(fullName, NuGetFramework.Parse(folderName).FullFrameworkName);
        //}

        //[Theory]
        //[InlineData("foo")]
        //[InlineData("foo45")]
        //[InlineData("foo45-client")]
        //[InlineData("45")]
        //[InlineData("foo.45")]
        //[InlineData("foo4.5.1.2.3")]
        //[InlineData("")]
        //public void NuGetFramework_Unsupported(string folderName)
        //{
        //    Assert.Equal("Unsupported", NuGetFramework.Parse(folderName).FullFrameworkName);
        //}
    }
}
