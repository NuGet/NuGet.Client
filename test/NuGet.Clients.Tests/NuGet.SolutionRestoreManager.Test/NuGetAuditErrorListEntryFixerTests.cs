// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Utilities;
using Moq;
using NuGet.SolutionRestoreManager.ErrorListFixers;
using NuGet.VisualStudio;
using Test.Utility.Threading;
using Xunit;

namespace NuGet.SolutionRestoreManager.Test
{
    [Collection(DispatcherThreadCollection.CollectionName)]
    public class NuGetAuditErrorListEntryFixerTests
    {
        public NuGetAuditErrorListEntryFixerTests(DispatcherThreadFixture fixture)
        {
            Assumes.Present(fixture);

            NuGetUIThreadHelper.SetCustomJoinableTaskFactory(fixture.JoinableTaskFactory);
        }

        [Theory]
        [InlineData("NU1901")]
        [InlineData("NU1902")]
        [InlineData("NU1903")]
        [InlineData("NU1904")]
        public void CanFix_WithSupportedNuGetAuditCode_ReturnsTrue(string code)
        {
            // Arrange
            NuGetAuditErrorListEntryFixer fixer = new();
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry(code);

            // Act
            bool result = fixer.CanFix(entry);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TryFix_WithNonMatchingEntry_ReturnsFalse()
        {
            // Arrange
            NuGetAuditErrorListEntryFixer fixer = new();
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1605");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryFix_WithMatchingEntryAndMissingService_ReturnsFalse()
        {
            // Arrange
            NuGetAuditErrorListEntryFixer fixer = new();
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1904");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void TryFix_WithMatchingEntryAndAvailableService_ReturnsTrue()
        {
            // Arrange
            Mock<IFixVulnerabilitiesService> service = new();
            service
                .Setup(s => s.LaunchFixVulnerabilitiesAsync(It.IsAny<FixVulnerabilitiesSource>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            NuGetAuditErrorListEntryFixer fixer = new()
            {
                FixVulnerabilitiesService = new Lazy<IFixVulnerabilitiesService>(() => service.Object),
            };
            ITableEntryHandle entry = ErrorListEntryTestUtility.CreateEntry("NU1903");

            // Act
            bool result = fixer.TryFix(entry);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void TypeMetadata_DefinesExpectedNameAndOrder()
        {
            // Arrange
            var attributes = typeof(NuGetAuditErrorListEntryFixer).GetCustomAttributes(inherit: false);

            // Assert
            Assert.Equal(1, attributes.Count(a => a is ExportAttribute));
            Assert.Contains(attributes, a => a is DataSourceAttribute);

            NameAttribute nameAttribute = Assert.IsType<NameAttribute>(attributes.Single(a => a is NameAttribute));
            Assert.Equal(NuGetErrorListFixerConstants.AuditFixerName, nameAttribute.Name);

            OrderAttribute orderAttribute = Assert.IsType<OrderAttribute>(attributes.Single(a => a is OrderAttribute));
            Assert.Equal(NuGetErrorListFixerConstants.CopilotFixerName, orderAttribute.Before);
        }
    }
}
