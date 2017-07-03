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
        [MemberData(nameof(GetVSRestoreSettingsUtilities_RestoreSourceData))]
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

        public static IEnumerable<object[]> GetVSRestoreSettingsUtilities_RestoreSourceData()
        {
            yield return new object[] {
                new string[] { @"C:\source1" },
                new string[] { @"C:\source1" }
            };

            yield return new object[]
            {
                new string[] { @"Clear" },
                new string[] { }
            };

            yield return new object[]
            {
                new string[] { @"Clear", "RestoreAdditionalProjectSources", @"C:\additionalSource" },
                new string[] { @"C:\additionalSource" }
            };

            yield return new object[] {
                new string[] { @"C:\source1", "RestoreAdditionalProjectSources",@"C:\additionalSource" },
                new string[] { @"C:\source1" ,@"C:\additionalSource" }
            };

            yield return new object[]
            {
                new string[] { "RestoreAdditionalProjectSources", @"C:\additionalSource" },
                new string[] { NuGetConstants.V3FeedUrl, @"C:\additionalSource" }
            };
        }
    }
}