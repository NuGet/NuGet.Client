using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Commands;
using NuGet.Test.Utility;
using Xunit;
using NuGet.Packaging.Rules;
using Moq;
using System.Xml.Linq;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Reflection;

namespace NuGet.Packaging.Test
{
    public class NoRefOrLibFolderInPackageRuleTests
    {
        [Theory]
        [MemberData("WarningRaisedData", MemberType = typeof(FileSource))]
        public void WarningRaisedWhenLibOrRefFolderWithTFMDataIsNotFoundAndBuildFolderIsFound(string[] files)
        {
            files = files.ToArray();
            //Act
            var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
            var issues = rule.Validate(files).ToList();

            // Assert
            Assert.True(issues.Any(p => p.Code == NuGetLogCode.NU5127));
        }

        [Theory]
        [MemberData("WarningNotRaisedData", MemberType = typeof(FileSource))]
        public void WarningNotRaisedWhenLibOrRefFolderWithTFMDataIsFound(string[] files)
        {
            files = files.ToArray();
            //Act
            var rule = new NoRefOrLibFolderInPackageRule(AnalysisResources.NoRefOrLibFolderInPackage);
            var issues = rule.Validate(files).ToList();

            // Assert
            Assert.False(issues.Any(p => p.Code == NuGetLogCode.NU5127));
        }

        public static class FileSource
        {
            public static readonly List<object[]> WarningNotRaisedData
                    = new List<object[]>
                    {
                    new object[] { new string[] {"build/random_tfm/test.props", "ref/random_tfm/test.dll"} },
                    new object[] { new string[] {"build/random_tfm/test.props", "lib/random_tfm/test.dll"} },
                    new object[] { new string[] {"ref/random_tfm/test.dll"} },
                    new object[] { new string[] {"lib/random_tfm/test.dll"} },
                    new object[] { new string[] {"build/any/test.props"} },
                    new object[] { new string[] {"build/native/test.props"} }
                    };

            public static readonly List<object[]> WarningRaisedData
                = new List<object[]>
                {
                new object[] { new string[] {"build/random_tfm/test.props"} },
                new object[] { new string[] {"build/random_tfm/test.props", "build/any/test.props", "build/native/test.props"} },
                new object[] { new string[] {"build/random_tfm/test.props", "content/random_tfm/test.props"} },
                new object[] { new string[] {"build/random_tfm/test.targets"} },
                new object[] { new string[] {"build/random_tfm/test.targets", "build/any/test.targets", "build/native/test.targets"} },
                new object[] { new string[] {"build/random_tfm/test.targets", "content/random_tfm/test.targets"} },
                new object[] { new string[] {"build/random_tfm/test.targets", "test.targets"} }
                };
        }
    }
    


}
