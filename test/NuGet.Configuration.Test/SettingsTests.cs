using System;
using System.IO;
using Xunit;
using Xunit.Extensions;

namespace NuGet.Configuration.Test
{
    public class SettingsTests
    {
        [Theory]
        [InlineData(@"D:\", @"C:\Users\SomeUsers\AppData\Roaming\nuget\nuget.config", @"C:\Users\SomeUsers\AppData\Roaming\nuget", @"nuget.config")]
        [InlineData(@"D:\", (string)null, @"D:\", (string)null)]
        [InlineData(@"D:\", "nuget.config", @"D:\", "nuget.config")]
        public void TestGetFileNameAndItsRoot(string root, string settingsPath, string expectedRoot, string expectedFileName)
        {
            // Act
            var tuple = Settings.GetFileNameAndItsRoot(root, settingsPath);

            // Assert
            Assert.Equal(tuple.Item1, expectedFileName);
            Assert.Equal(tuple.Item2, expectedRoot);
        }
    }
}
