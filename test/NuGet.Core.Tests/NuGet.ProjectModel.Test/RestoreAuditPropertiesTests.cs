// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using Xunit;

namespace NuGet.ProjectModel.Test
{
    public class RestoreAuditPropertiesTests
    {
        [Fact]
        public void Equals_WithNull_ReturnsFalse()
        {
            // Arrange
            var properties = new RestoreAuditProperties();

            // Act & Assert
            Assert.False(properties.Equals((RestoreAuditProperties?)null));
            Assert.False(properties.Equals((object?)null));
            Assert.False(properties == null);
            Assert.False(null == properties);
            Assert.True(properties != null);
            Assert.True(null != properties);
        }

        [Fact]
        public void Euqals_WithSameInstance_ReturnsTrue()
        {
            // Arrange
            var properties = new RestoreAuditProperties();

            // Act & Assert
            Assert.True(properties.Equals(properties));
            Assert.True(properties.Equals((object)properties));
#pragma warning disable CS1718 // Comparison made to same variable
            Assert.True(properties == properties);
            Assert.False(properties != properties);
#pragma warning restore CS1718 // Comparison made to same variable
        }

        [Fact]
        public void Equals_InstancesWithSameValues_ReturnsTrue()
        {
            // Arrange
            var properties1 = new RestoreAuditProperties()
            {
                AuditLevel = "moderate",
            };
            var properties2 = new RestoreAuditProperties()
            {
                AuditLevel = properties1.AuditLevel,
            };

            // Act & Assert
            Assert.True(properties1.Equals(properties2));
            Assert.True(properties1.Equals((object)properties2));
            Assert.True(properties1 == properties2);
            Assert.False(properties1 != properties2);
        }

        [Fact]
        public void Equals_InstancesWithDifferentValues_ReturnsFalse()
        {
            // Arrange
            var properties1 = new RestoreAuditProperties()
            {
                AuditLevel = "moderate",
            };
            var properties2 = new RestoreAuditProperties()
            {
                AuditLevel = "high",
            };

            // Act & Assert
            Assert.False(properties1.Equals(properties2));
            Assert.False(properties1.Equals((object)properties2));
            Assert.False(properties1 == properties2);
            Assert.True(properties1 != properties2);
        }

        [Fact]
        public void Clone_ReturnsNewInstanceWithSameValues()
        {
            // Arrange
            var property1 = new RestoreAuditProperties();

            var type = typeof(RestoreAuditProperties);
            var typeProperties = type.GetProperties(System.Reflection.BindingFlags.Public);
            int propertyCount = 0;
            foreach (var property in typeProperties)
            {
                if (property.CanWrite)
                {
                    propertyCount++;
                    property.SetValue(property1, propertyCount.ToString());
                }
            }

            // Act
            var clone = property1.Clone();

            // Assert
            Assert.NotSame(property1, clone);
            Assert.Equal(property1, clone);
        }
    }
}
