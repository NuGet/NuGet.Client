// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;

namespace NuGet.Test.Utility
{
    /// <summary>
    /// Rerpresents a builder for <c>TextDirectory</c> objects, with
    /// utilities for creating file in the test directory. For testing purposes
    /// </summary>
    public class TestDirectoryBuilder
    {
        public NuspecBuilder NuspecBuilder { get; private set; }
        public string NuspecFile { get; private set; }
        public Dictionary<string, int> Files { get; private set; }
        public Dictionary<string, string> TextFiles { get; private set; }
        public string BaseDir { get; private set; }
        public string NuspecPath => Path.Combine(BaseDir, NuspecFile);

        private TestDirectoryBuilder()
        {
            Files = new Dictionary<string, int>();
            TextFiles = new Dictionary<string, string>();
        }

        /// <summary>
        /// Factory method for creating the builder
        /// </summary>
        /// <returns>An instance of <c>TestDirectoryBuilder</c></returns>
        public static TestDirectoryBuilder Create()
        {
            return new TestDirectoryBuilder();
        }

        public TestDirectoryBuilder WithNuspec(NuspecBuilder nuspecBuilder, string filepath = "Package.nuspec")
        {
            NuspecBuilder = nuspecBuilder;
            NuspecFile = filepath;
            return this;
        }

        public TestDirectoryBuilder WithFile(string filepath, int size)
        {
            Files.Add(filepath, size);
            return this;
        }

        public TestDirectoryBuilder WithFile(string filepath, string content)
        {
            TextFiles.Add(filepath, content);
            return this;
        }

        /// <summary>
        /// Creates the TestDirectory and all the files
        /// </summary>
        /// <returns>A <c>TestDirectory</c> object with the file structure created</returns>
        public TestDirectory Build()
        {
            TestDirectory testDirectory = TestDirectory.Create();
            BaseDir = testDirectory.Path;

            CreateFiles();
            CreateTextFiles();

            if (NuspecBuilder != null)
            {
                File.WriteAllText(NuspecPath, NuspecBuilder.Build().ToString());
            }

            return testDirectory;
        }

        private void CreateFiles()
        {
            foreach (var f in Files)
            {
                var filepath = Path.Combine(BaseDir, f.Key);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                using (var fileStream = File.OpenWrite(Path.Combine(BaseDir, f.Key)))
                {
                    fileStream.SetLength(f.Value);
                }
            }
        }

        private void CreateTextFiles()
        {
            foreach (var f in TextFiles)
            {
                var filepath = Path.Combine(BaseDir, f.Key);
                var dir = Path.GetDirectoryName(filepath);

                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(BaseDir, f.Key), f.Value);
            }
        }
    }
}
