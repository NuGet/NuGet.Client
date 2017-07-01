// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Xunit;
using NuGet.ProjectModel;
using NuGet.Commands;
using System.Linq;
using NuGet.Configuration;
using NuGet.Test.Utility;
using System.Collections.Generic;
using Xunit.Extensions;

namespace NuGet.PackageManagement.VisualStudio.Test
{
    public class VSRestoreSettingsUtilityTests
    {
        [Theory]
        [MemberData(nameof(VSRestoreSettingsUtilities_RestoreSourceData1))]
        //[MemberData(nameof(VSRestoreSettingsUtilities_RestoreSourceData2))]
        //[MemberData(nameof(VSRestoreSettingsUtilities_RestoreSourceData3))]
        //[MemberData(nameof(VSRestoreSettingsUtilities_RestoreSourceData4))]
        //[MemberData(nameof(VSRestoreSettingsUtilities_RestoreSourceData5))]
        public void VSRestoreSettingsUtilities_RestoreSource(string[] restoreSources, string[] expectedRestoreSources)
        {
            using (var mockBaseDirectory = TestDirectory.Create())
            {

                var spec = new PackageSpec();
                spec.RestoreMetadata = new ProjectRestoreMetadata();
                spec.RestoreMetadata.ProjectPath = @"C:\project\projectPath";
                spec.RestoreMetadata.Sources = restoreSources.Select(e => new PackageSource(e)).ToList();
                var settings = new Settings(mockBaseDirectory);
                var actualSources = VSRestoreSettingsUtilities.GetSources(settings, spec);

                Assert.True(
                       Enumerable.SequenceEqual(expectedRestoreSources.OrderBy(t => t), actualSources.Select(e => e.Source).OrderBy(t => t)),
                       "expected: " + string.Join(",", expectedRestoreSources.ToArray()) + "\nactual: " + string.Join(",", actualSources.Select(e => e.Source).ToArray()));
            }
        }
        public static IEnumerable<object[]> VSRestoreSettingsUtilities_RestoreSourceData1
        {
            get
            {
                yield return new object[] {
                   new object[] { @"C:\source1" },
                   new object[] { @"C:\source1" }
                };
            }
       }

    public static IEnumerable<object[]> VSRestoreSettingsUtilities_RestoreSourceData2
        {
            get
            {
                yield return new object[] { @"Clear" };
                yield return new object[] { };
            }
        }

        public static IEnumerable<object[]> VSRestoreSettingsUtilities_RestoreSourceData3
        {
            get
            {
                yield return new object[] { @"Clear;RestoreAdditionalProjectSources;C:\additionalSource" };
                yield return new object[] { @"C:\additionalSource" };
            }
        }

        public static IEnumerable<object[]> VSRestoreSettingsUtilities_RestoreSourceData4
        {
            get
            {
                yield return new object[] { @"C:\source1;RestoreAdditionalProjectSources;C:\additionalSource" };
                yield return new object[] { @"C:\source1;C:\additionalSource" };
            }
        }

        public static IEnumerable<object[]> VSRestoreSettingsUtilities_RestoreSourceData5
        {
            get
            {
                yield return new object[] { @"RestoreAdditionalProjectSources;C:\additionalSource" };
                yield return new object[] { NuGetConstants.V3FeedUrl + @"; C:\additionalSource" };
            }
        }
    }
}