// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using Xunit;

namespace NuGet.Test
{
    public class NuGetFrameworkTests
    {
        [Theory]
        [InlineData("net45", "net45")]
        [InlineData("portable-net45+win8+monoandroid", "portable-net45+win8")]
        [InlineData("portable-net45+win8+xamarin.ios", "portable-net45+win8")]
        [InlineData("portable-net45+win8", "portable-net45+win8")]
        [InlineData("portable-win8+net45+monoandroid+monotouch", "portable-net45+win8")]
        [InlineData("portable-win8+net45+monoandroid+monotouch+xamarin.ios", "portable-net45+win8")]
        [InlineData("portable-win8+net45", "portable-net45+win8")]
        [InlineData("portable-monoandroid+monotouch+win8+net45", "portable-net45+win8")]
        [InlineData("portable-monoandroid+xamarin.ios+monotouch+win8+net45", "portable-net45+win8")]
        [InlineData("portable-monoandroid+win8+net45", "portable-net45+win8")]
        [InlineData("win10.0", "win10.0")]
        [InlineData("net45-client", "net45-client")]
        [InlineData("net45-unknown", "net45-unknown")]
        [InlineData("Any", "any")]
        [InlineData("Unsupported", "unsupported")]
        [InlineData("Agnostic", "agnostic")]
        [InlineData("portable-win8+net45+monoandroid+monotouch+xamarin.ios+xamarin.watchos", "portable-net45+win8")]
        [InlineData("portable-monoandroid+xamarin.ios+xamarin.watchos+monotouch+win8+net45", "portable-net45+win8")]
        [InlineData("portable-win8+net45+monoandroid+monotouch+xamarin.ios+xamarin.tvos", "portable-net45+win8")]
        [InlineData("portable-monoandroid+xamarin.ios+xamarin.tvos+monotouch+win8+net45", "portable-net45+win8")]
        public void NuGetFramework_ShortName(string input, string expected)
        {
            var fw = NuGetFramework.Parse(input);

            string result = fw.GetShortFolderName();

            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("net45", "net45")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch+xamarin.ios")]
        [InlineData("portable-net45+win8", "portable-net45+win8+xamarin.ios")]
        [InlineData("portable-win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+monotouch+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("portable-monoandroid+win8+net45", "portable-net45+win8+monoandroid+monotouch")]
        [InlineData("win10.0", "win10.0")]
        [InlineData("net45-client", "net45-client")]
        [InlineData("net45-unknown", "net45-unknown")]
        [InlineData("Any", "any")]
        [InlineData("Unsupported", "unsupported")]
        [InlineData("Agnostic", "agnostic")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch+xamarin.ios+xamarin.watchos")]
        [InlineData("portable-net45+win8", "portable-net45+win8+xamarin.ios+xamarin.watchos")]
        [InlineData("portable-net45+win8", "portable-net45+win8+monoandroid+monotouch+xamarin.ios+xamarin.tvos")]
        [InlineData("portable-net45+win8", "portable-net45+win8+xamarin.ios+xamarin.tvos")]
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

        [Theory]
        [InlineData("net45", "net450")]
        [InlineData("net45", "net4.5.0")]
        [InlineData("aspnetcore5", "aspnetcore500")]
        [InlineData(".NETFramework, Version=v4.5", "net45")]
        [InlineData("NETFramework, Version=v4.5", "net45")]
        [InlineData("NETFramework, Version=v4.5", "net450")]
        public void NuGetFramework_EqualityNormalization(string a, string b)
        {
            var fw1 = NuGetFramework.Parse(a);
            var fw2 = NuGetFramework.Parse(b);
            HashSet<NuGetFramework> hashSet = new HashSet<NuGetFramework>() { fw1, fw2 };

            Assert.True(fw1.Equals(fw2));
            Assert.True(fw2.Equals(fw1));
            Assert.Equal(fw1.GetHashCode(), fw2.GetHashCode());
            Assert.Equal(1, hashSet.Count);
        }

        [Fact]
        public void NuGetFramework_EqualityMixed()
        {
            List<NuGetFramework> frameworks = new List<NuGetFramework>();
            frameworks.Add(NuGetFramework.Parse("net45"));
            frameworks.Add(NuGetFramework.Parse("net450"));
            frameworks.Add(NuGetFramework.Parse("net4.5"));
            frameworks.Add(NuGetFramework.Parse(".NETFramework, Version=v4.5"));
            frameworks.Add(NuGetFramework.Parse(".NETFramework, Version=4.5"));
            frameworks.Add(NuGetFramework.Parse("NETFramework, Version=v4.5"));
            frameworks.Add(NuGetFramework.Parse("NETFramework, Version=v4.5"));

            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5)));
            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5), string.Empty));

            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5, 0)));
            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5, 0), string.Empty));

            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5, 0, 0)));
            frameworks.Add(new NuGetFramework(".nETframework", new Version(4, 5, 0, 0), string.Empty));

            foreach (var fw1 in frameworks)
            {
                foreach (var fw2 in frameworks)
                {
                    Assert.True(fw1.Equals(fw2), fw1.ToString() + " " + fw2.ToString());
                    Assert.True(fw2.Equals(fw1), fw2.ToString() + " " + fw1.ToString());
                    Assert.True(Object.Equals(fw1, fw2));
                    Assert.True(Object.Equals(fw2, fw1));
                }
            }
        }

        [Fact]
        public void NuGetFramework_EqualityMixed2()
        {
            List<NuGetFramework> frameworks = new List<NuGetFramework>();

            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5), null));
            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5), string.Empty));

            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5, 0), null));
            frameworks.Add(new NuGetFramework(".NETFramework", new Version(4, 5, 0), string.Empty));

            foreach (var fw1 in frameworks)
            {
                foreach (var fw2 in frameworks)
                {
                    Assert.True(fw1.Equals(fw2), fw1.ToString() + " " + fw2.ToString());
                    Assert.True(fw2.Equals(fw1), fw2.ToString() + " " + fw1.ToString());
                    Assert.True(Object.Equals(fw1, fw2));
                    Assert.True(Object.Equals(fw2, fw1));
                }
            }
        }
    }
}
