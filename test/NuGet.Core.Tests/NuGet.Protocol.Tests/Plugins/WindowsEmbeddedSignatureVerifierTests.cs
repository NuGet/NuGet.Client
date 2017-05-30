// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Protocol.Plugins.Tests
{
    public class WindowsEmbeddedSignatureVerifierTests
    {
        private readonly WindowsEmbeddedSignatureVerifier _verifier;

        public WindowsEmbeddedSignatureVerifierTests()
        {
            _verifier = new WindowsEmbeddedSignatureVerifier();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsValid_ThrowsForNullOrEmpty(string filePath)
        {
            var expectedMessage = $"Argument cannot be null or empty.{Environment.NewLine}Parameter name: filePath";
            var exception = Assert.Throws<ArgumentException>(() => _verifier.IsValid(filePath));

            Assert.Equal(expectedMessage, exception.Message);
        }

        [PlatformFact(Platform.Windows)]
        public void IsValid_ReturnsTrueForValidFile()
        {
            var filePath = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "System32", "wintrust.dll");

            var actualResult = _verifier.IsValid(filePath);

            Assert.True(actualResult);
        }

        [PlatformFact(Platform.Windows)]
        public void IsValid_ReturnsFalseForNonExistentFilePath()
        {
            var filePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var actualResult = _verifier.IsValid(filePath);

            Assert.False(actualResult);
        }

        [PlatformFact(Platform.Windows)]
        public void IsValid_ReturnsFalseForInvalidFile()
        {
            using (var testDirectory = TestDirectory.Create())
            {
                // This is the build output from a default class library project.
                // There is nothing special about it; any unsigned DLL would work just as well.
                // This is a better test than an arbitrary file (e.g.:  file.txt) because
                // this is a portable executable (PE) file.  Authenticode signatures are
                // expected in PE files.
                var fileName = "DefaultClassLibrary.dll";
                var resourceName = $"NuGet.Protocol.Tests.compiler.resources.{fileName}";
                var filePath = Path.Combine(testDirectory.Path, fileName);

                ResourceTestUtility.CopyResourceToFile(resourceName, GetType(), filePath);

                var actualResult = _verifier.IsValid(filePath);

                Assert.False(actualResult);
            }
        }
    }
}